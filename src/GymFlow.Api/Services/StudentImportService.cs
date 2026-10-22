using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Api.Services;

public sealed class StudentImportService(
    AppDbContext db,
    IGymTimeZoneProvider timeZoneProvider,
    TimeProvider timeProvider)
{
    private const int MaxRows = 5000;
    private const int MaxFieldLength = 10_000;
    private static readonly TimeSpan PreviewLifetime = TimeSpan.FromHours(24);
    private static readonly string[] DateFormats =
    ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyyMMdd"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StudentImportPreviewResponse> PreviewAsync(
        Guid gymId,
        PreviewStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        await RemoveExpiredPreviewsAsync(cancellationToken);
        if (request.Rows is null || request.Rows.Count is 0 or > MaxRows)
            throw new ArgumentException($"O arquivo deve conter de 1 a {MaxRows} registros.");
        if (request.Rows.Any(row => row.RowNumber < 1 || row.RowNumber > 1_000_000) ||
            request.Rows.Select(row => row.RowNumber).Distinct().Count() != request.Rows.Count)
            throw new ArgumentException("A numeração das linhas do arquivo é inválida.");
        if (request.Rows.Any(row => new[] { row.Name, row.Email, row.Phone, row.BirthDate }
                .Any(value => value?.Length > MaxFieldLength)))
            throw new ArgumentException("Um campo excede o limite seguro de tamanho.");

        var records = request.Rows.Select(Normalize).ToList();
        var fingerprint = Fingerprint(records);
        var existing = await db.StudentImportBatches.SingleOrDefaultAsync(
            batch => batch.GymId == gymId && batch.Fingerprint == fingerprint,
            cancellationToken);

        if (existing?.State == (int)BatchState.Completed)
            return CompletedPreview(existing);
        if (existing is not null && existing.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("Esta prévia expirou. Selecione o arquivo novamente.");

        var previewRows = await BuildPreviewAsync(gymId, records, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batch = existing ?? new StudentImportBatch
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Fingerprint = fingerprint,
            State = (int)BatchState.Previewed,
            CreatedAt = now,
            ExpiresAt = now.Add(PreviewLifetime)
        };
        batch.PayloadJson = JsonSerializer.Serialize(records, JsonOptions);
        batch.PreviewJson = JsonSerializer.Serialize(previewRows, JsonOptions);
        batch.State = (int)BatchState.Previewed;

        if (existing is null) db.StudentImportBatches.Add(batch);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (existing is not null) throw;
            db.Entry(batch).State = EntityState.Detached;
            var raced = await db.StudentImportBatches.AsNoTracking().SingleOrDefaultAsync(
                item => item.GymId == gymId && item.Fingerprint == fingerprint,
                cancellationToken);
            if (raced is null) throw;
            if (raced.State == (int)BatchState.Completed) return CompletedPreview(raced);
            if (raced.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
                throw new InvalidOperationException("Esta prévia expirou. Selecione o arquivo novamente.");
            return await PreviewAsync(gymId, request, cancellationToken);
        }

        return new StudentImportPreviewResponse(
            batch.Id, false, batch.ExpiresAt, previewRows, []);
    }

    public async Task<StudentImportResultResponse> ConfirmAsync(
        Guid gymId,
        Guid batchId,
        ConfirmStudentImportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Decisions is null)
            throw new ArgumentException("Revise as decisões para cada registro válido.");
        if (request.Decisions.Count > MaxRows)
            throw new ArgumentException($"A confirmação aceita no máximo {MaxRows} decisões.");

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var claimed = await db.StudentImportBatches
            .Where(item => item.Id == batchId && item.GymId == gymId &&
                           item.State == (int)BatchState.Previewed &&
                           item.ExpiresAt > timeProvider.GetUtcNow().UtcDateTime)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                item => item.State, (int)BatchState.Processing), cancellationToken);
        if (claimed != 1)
        {
            var latest = await db.StudentImportBatches.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == batchId && item.GymId == gymId, cancellationToken);
            if (latest is null) throw new KeyNotFoundException("Prévia de importação não encontrada.");
            if (latest.State == (int)BatchState.Completed)
            {
                await transaction.CommitAsync(cancellationToken);
                return CompletedResult(latest);
            }
            throw new InvalidOperationException("Esta prévia expirou ou já está em processamento. Gere uma nova prévia.");
        }

        var batch = await db.StudentImportBatches.SingleOrDefaultAsync(
            item => item.Id == batchId && item.GymId == gymId,
            cancellationToken);
        if (batch is null) throw new KeyNotFoundException("Prévia de importação não encontrada.");
        if (batch.State == (int)BatchState.Completed)
        {
            await transaction.CommitAsync(cancellationToken);
            return CompletedResult(batch);
        }
        if (batch.State != (int)BatchState.Processing || batch.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
            throw new InvalidOperationException("Esta prévia expirou ou já está em processamento. Gere uma nova prévia.");

        var records = JsonSerializer.Deserialize<List<NormalizedInputRow>>(batch.PayloadJson ?? "[]", JsonOptions) ?? [];
        var savedPreview = JsonSerializer.Deserialize<List<StudentImportPreviewRow>>(batch.PreviewJson ?? "[]", JsonOptions) ?? [];
        if (records.Count == 0 || savedPreview.Count != records.Count)
            throw new InvalidOperationException("A prévia não está mais disponível. Selecione o arquivo novamente.");

        var currentPreview = await BuildPreviewAsync(gymId, records, cancellationToken);
        if (!SameCandidates(savedPreview, currentPreview))
            throw new StudentImportReviewChangedException(currentPreview);

        var decisions = ValidateDecisions(request.Decisions, currentPreview);
        var rowsToCreate = currentPreview
            .Where(row => row.IsValid && decisions[row.RowNumber] != "Ignore")
            .ToList();
        if (rowsToCreate.Count > 0 && request.NewStudentsActive is null)
            throw new ArgumentException("Escolha a situação administrativa dos novos cadastros.");

        var gymTimeZone = timeZoneProvider.GetTimeZone(gymId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, gymTimeZone);
        var createdStudents = new List<Student>(rowsToCreate.Count);
        var results = new List<StudentImportResultRow>(records.Count);
        var rowByNumber = records.ToDictionary(row => row.RowNumber);
        foreach (var preview in currentPreview)
        {
            if (!preview.IsValid)
            {
                results.Add(new(preview.RowNumber, "Error", string.Join(" ", preview.Errors)));
                continue;
            }

            var action = decisions[preview.RowNumber];
            if (action == "Ignore")
            {
                results.Add(new(preview.RowNumber, "Ignored", "Registro ignorado pelo Admin."));
                continue;
            }

            var row = rowByNumber[preview.RowNumber];
            createdStudents.Add(new Student
            {
                Id = Guid.NewGuid(),
                GymId = gymId,
                Name = row.Name!,
                Email = NullIfEmpty(row.Email),
                Phone = NullIfEmpty(row.Phone),
                BirthDate = ParseBirthDate(row.BirthDate),
                IsActive = request.NewStudentsActive!.Value,
                AppAccessEnabled = false,
                UserId = null,
                NoValidEnrollmentSince = DateOnly.FromDateTime(localNow),
                CreatedAt = timeProvider.GetUtcNow().UtcDateTime
            });
            results.Add(new(preview.RowNumber, "Imported", "Aluno cadastrado sem conta do aplicativo."));
        }

        db.Students.AddRange(createdStudents);
        batch.State = (int)BatchState.Completed;
        batch.CompletedAt = timeProvider.GetUtcNow().UtcDateTime;
        batch.PayloadJson = null;
        batch.PreviewJson = null;
        batch.ResultJson = JsonSerializer.Serialize(results, JsonOptions);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new StudentImportResultResponse(batch.Id, false, results);
    }

    public async Task<int> RemoveExpiredPreviewsAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await db.StudentImportBatches
            .Where(batch => batch.State == (int)BatchState.Previewed && batch.ExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<List<StudentImportPreviewRow>> BuildPreviewAsync(
        Guid gymId,
        IReadOnlyList<NormalizedInputRow> rows,
        CancellationToken cancellationToken)
    {
        var existing = await db.Students.AsNoTracking()
            .Where(student => student.GymId == gymId)
            .Select(student => new ExistingStudent(
                student.Id, student.Name, student.Email, student.Phone, student.ArchivedAt != null))
            .ToListAsync(cancellationToken);
        var validRows = rows.Select(ValidateRow).ToDictionary(row => row.RowNumber);
        var results = new List<StudentImportPreviewRow>(rows.Count);

        foreach (var row in rows)
        {
            var validation = validRows[row.RowNumber];
            var candidates = new Dictionary<string, StudentImportDuplicateCandidate>(StringComparer.Ordinal);
            if (validation.Errors.Count == 0)
            {
                foreach (var student in existing)
                {
                    var matchedBy = MatchBy(row, student.Email, student.Phone);
                    if (matchedBy is not null)
                    {
                        candidates[$"student:{student.Id}"] = new(
                            student.Id, null, student.Name, student.Email, student.Phone,
                            student.IsArchived, matchedBy);
                    }
                }

                foreach (var other in rows)
                {
                    if (other.RowNumber == row.RowNumber || validRows[other.RowNumber].Errors.Count != 0)
                        continue;
                    var matchedBy = MatchBy(row, other.Email, other.Phone);
                    if (matchedBy is not null)
                    {
                        candidates[$"row:{other.RowNumber}"] = new(
                            null, other.RowNumber, other.Name ?? "", other.Email, other.Phone,
                            false, matchedBy);
                    }
                }
            }

            results.Add(new(row.RowNumber, validation.Errors.Count == 0,
                validation.Errors, candidates.Values.OrderBy(candidate => candidate.SourceRowNumber ?? 0)
                    .ThenBy(candidate => candidate.StudentId).ToList()));
        }
        return results;
    }

    private static ValidationResultRow ValidateRow(NormalizedInputRow row)
    {
        var errors = new List<string>();
        var name = row.Name?.Trim();
        var email = NullIfEmpty(row.Email);
        var phone = NullIfEmpty(row.Phone);
        if (string.IsNullOrWhiteSpace(name)) errors.Add("Nome obrigatório.");
        else if (name.Length > 150) errors.Add("Nome excede 150 caracteres.");
        if (email is not null)
        {
            if (email.Length > 200) errors.Add("E-mail excede 200 caracteres.");
            else if (!new EmailAddressAttribute().IsValid(email)) errors.Add("E-mail inválido.");
        }
        if (phone is not null && phone.Length > 20) errors.Add("Telefone excede 20 caracteres.");
        if (row.BirthDate is not null && ParseBirthDate(row.BirthDate) is null)
            errors.Add("Nascimento inválido; use dia/mês/ano ou ano-mês-dia.");

        if (new[] { name, email, row.BirthDate }.Any(value => IsPotentialFormula(value)) ||
            IsPotentialFormula(phone, allowFormattedPhone: true))
            errors.Add("Fórmula ou conteúdo executável detectado; revise a célula no arquivo.");
        return new(row.RowNumber, errors);
    }

    private static bool IsPotentialFormula(string? value, bool allowFormattedPhone = false)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.TrimStart();
        if (trimmed.StartsWith('=') || trimmed.StartsWith('@')) return true;
        if (trimmed.StartsWith('+') || trimmed.StartsWith('-'))
        {
            var remainder = trimmed[1..];
            if (allowFormattedPhone && trimmed[0] == '+' &&
                remainder.All(character => char.IsDigit(character) ||
                    char.IsWhiteSpace(character) || character is '(' or ')' or '-' or '.'))
                return false;
            return remainder.Any(character => char.IsLetter(character)) ||
                   remainder.Any(character => character is '=' or '@' or '+' or '*' or '/');
        }
        return false;
    }

    private static string? MatchBy(NormalizedInputRow row, string? candidateEmail, string? candidatePhone)
    {
        var emailMatch = !string.IsNullOrWhiteSpace(row.Email) &&
            string.Equals(row.Email, candidateEmail?.Trim(), StringComparison.OrdinalIgnoreCase);
        var phone = NormalizePhone(row.Phone);
        var phoneMatch = phone.Length >= 10 && phone == NormalizePhone(candidatePhone);
        if (emailMatch && phoneMatch) return "E-mail e telefone";
        if (emailMatch) return "E-mail";
        if (phoneMatch) return "Telefone";
        return null;
    }

    private static Dictionary<int, string> ValidateDecisions(
        IReadOnlyList<StudentImportDecision> decisions,
        IReadOnlyList<StudentImportPreviewRow> preview)
    {
        var byRow = new Dictionary<int, string>();
        foreach (var decision in decisions)
        {
            if (!byRow.TryAdd(decision.RowNumber, decision.Action))
                throw new ArgumentException("Há decisões repetidas para uma linha.");
        }

        foreach (var row in preview.Where(row => row.IsValid))
        {
            if (!byRow.TryGetValue(row.RowNumber, out var action))
                throw new ArgumentException("Decida se cada registro válido será importado ou ignorado.");
            var allowed = row.Candidates.Count == 0
                ? action is "Import" or "Ignore"
                : action is "ImportAsNew" or "Ignore";
            if (!allowed)
                throw new ArgumentException(row.Candidates.Count == 0
                    ? "A decisão de um registro sem duplicidade é inválida."
                    : "Registros com possíveis duplicidades exigem decisão explícita: criar novo ou ignorar.");
        }

        if (byRow.Keys.Except(preview.Where(row => row.IsValid).Select(row => row.RowNumber)).Any())
            throw new ArgumentException("A decisão inclui uma linha inválida ou inexistente.");
        return byRow;
    }

    private static bool SameCandidates(
        IReadOnlyList<StudentImportPreviewRow> first,
        IReadOnlyList<StudentImportPreviewRow> second)
    {
        if (first.Count != second.Count) return false;
        var secondByRow = second.ToDictionary(row => row.RowNumber);
        foreach (var row in first)
        {
            if (!secondByRow.TryGetValue(row.RowNumber, out var other)) return false;
            var left = CandidateKeys(row);
            var right = CandidateKeys(other);
            if (!left.SequenceEqual(right, StringComparer.Ordinal)) return false;
        }
        return true;
    }

    private static IEnumerable<string> CandidateKeys(StudentImportPreviewRow row) =>
        row.Candidates.Select(candidate =>
            $"{candidate.StudentId}:{candidate.SourceRowNumber}:{candidate.MatchBy}")
            .OrderBy(value => value, StringComparer.Ordinal);

    private static StudentImportPreviewResponse CompletedPreview(StudentImportBatch batch)
    {
        var results = DeserializeResults(batch.ResultJson);
        var rows = results.Select(result => new StudentImportPreviewRow(
            result.RowNumber, result.Status != "Error", [], [])).ToList();
        return new(batch.Id, true, batch.ExpiresAt, rows, results);
    }

    private static StudentImportResultResponse CompletedResult(StudentImportBatch batch) =>
        new(batch.Id, true, DeserializeResults(batch.ResultJson));

    private static List<StudentImportResultRow> DeserializeResults(string? json) =>
        JsonSerializer.Deserialize<List<StudentImportResultRow>>(json ?? "[]", JsonOptions) ?? [];

    private static NormalizedInputRow Normalize(StudentImportInputRow row) => new(
        row.RowNumber,
        NullIfEmpty(row.Name),
        NullIfEmpty(row.Email)?.ToLowerInvariant(),
        NullIfEmpty(row.Phone),
        NullIfEmpty(row.BirthDate));

    private static string Fingerprint(IReadOnlyList<NormalizedInputRow> rows)
    {
        var canonical = rows
            .OrderBy(row => row.RowNumber)
            .Select(row => JsonSerializer.Serialize(row, JsonOptions));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", canonical)));
        return Convert.ToHexString(bytes);
    }

    private static DateOnly? ParseBirthDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateOnly.TryParseExact(value.Trim(), DateFormats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;
    }

    private static string NormalizePhone(string? phone) =>
        new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    private static string? NullIfEmpty(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private enum BatchState { Previewed, Processing, Completed }
    private sealed record NormalizedInputRow(int RowNumber, string? Name, string? Email, string? Phone, string? BirthDate);
    private sealed record ExistingStudent(Guid Id, string Name, string? Email, string? Phone, bool IsArchived);
    private sealed record ValidationResultRow(int RowNumber, List<string> Errors);

}

public sealed class StudentImportReviewChangedException(
    IReadOnlyList<StudentImportPreviewRow> rows) : Exception("A lista de possíveis duplicidades foi alterada.")
{
    public IReadOnlyList<StudentImportPreviewRow> Rows { get; } = rows;
}
