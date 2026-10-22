using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GymFlow.Application.DTOs.Migrations;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Api.Services;

public sealed class MigrationService(AppDbContext db, IGymTimeZoneProvider timeZones, TimeProvider clock)
{
    private const int MaxRows = 5000;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DateFormats = ["yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyyMMdd"];

    public async Task<MigrationSession> CreateSessionAsync(Guid gymId, Guid actorId,
        CreateMigrationSessionRequest request, CancellationToken token)
    {
        var name = request.Name?.Trim();
        var source = request.SourceNamespace?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 150)
            throw new ArgumentException("Informe um nome de sessão de até 150 caracteres.");
        if (string.IsNullOrWhiteSpace(source) || source.Length > 120 || source.Any(char.IsControl))
            throw new ArgumentException("Informe um identificador curto para o sistema de origem.");
        var session = new MigrationSession
        {
            Id = Guid.NewGuid(), GymId = gymId, CreatedByUserId = actorId,
            Name = name, SourceNamespace = source, Status = MigrationSessionStatus.Preparing,
            CreatedAt = Now(), ExpiresAt = Now().Add(SessionLifetime)
        };
        db.MigrationSessions.Add(session);
        await db.SaveChangesAsync(token);
        return session;
    }

    public async Task<MigrationBatchPreviewResponse> PreviewAsync(Guid gymId, Guid sessionId,
        PreviewMigrationBatchRequest request, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var session = await LockPreparingSessionAsync(gymId, sessionId, token);
        if (!Enum.IsDefined(request.EntityType))
            throw new ArgumentException("Tipo de arquivo de migração inválido.");
        if (request.Rows is null || request.Rows.Count is 0 or > MaxRows)
            throw new ArgumentException($"O arquivo deve conter de 1 a {MaxRows} registros.");
        if (request.Rows.Any(row => row.RowNumber < 1 || row.RowNumber > 1_000_000) ||
            request.Rows.Select(row => row.RowNumber).Distinct().Count() != request.Rows.Count)
            throw new ArgumentException("A numeração das linhas do arquivo é inválida.");
        if (request.Rows.Any(row => new[] { row.ExternalId, row.Name, row.Email, row.Phone, row.BirthDate,
                row.Price, row.DurationMonths, row.BillingCycle, row.StudentExternalId, row.PlanExternalId,
                row.StartDate, row.EndDate, row.ChargeState, row.ChargeDueDate, row.EnrollmentExternalId,
                row.ChargeExternalId }.Any(value => value?.Length > 500)))
            throw new ArgumentException("Um campo excede o limite seguro de tamanho.");

        var records = request.Rows.Select(Normalize).ToList();
        var duplicateKeys = records.Where(row => row.ExternalId is not null)
            .GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var fingerprint = Fingerprint(request.EntityType, records);
        var existingBatch = await db.MigrationBatches.AsNoTracking().SingleOrDefaultAsync(batch =>
            batch.MigrationSessionId == sessionId && batch.EntityType == request.EntityType && batch.Fingerprint == fingerprint, token);
        if (existingBatch is not null)
        {
            if (existingBatch.Status == MigrationBatchStatus.Discarded)
                throw new InvalidOperationException("Este arquivo já foi descartado nesta sessão e não pode ser reutilizado como o mesmo lote.");
            if (existingBatch.Status == MigrationBatchStatus.Previewed && existingBatch.EntityType == MigrationEntityType.Students)
            {
                var tracked = await db.MigrationBatches.SingleAsync(batch => batch.Id == existingBatch.Id && batch.GymId == gymId, token);
                var refreshed = await RefreshStudentPreviewAsync(session, tracked, token);
                await transaction.CommitAsync(token);
                return new(tracked.Id, tracked.EntityType, refreshed, true,
                    Deserialize<List<MigrationRowDecision>>(tracked.DecisionsJson), ReviewFingerprint(tracked));
            }
            if (existingBatch.Status == MigrationBatchStatus.Previewed && existingBatch.EntityType == MigrationEntityType.Plans)
            {
                var planRows = Deserialize<List<NormalizedMigrationRow>>(existingBatch.PayloadJson);
                var refreshed = await BuildPreviewAsync(session, existingBatch.EntityType, planRows,
                    planRows.Where(row => row.ExternalId is not null).GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
                        .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal),
                    new Dictionary<int, IReadOnlyList<string>>(), token, existingBatch.Id);
                var previewJson = Serialize(refreshed);
                if (!JsonEquals(previewJson, existingBatch.PreviewJson))
                {
                    var tracked = await db.MigrationBatches.SingleAsync(batch => batch.Id == existingBatch.Id &&
                        batch.GymId == gymId && batch.Status == MigrationBatchStatus.Previewed, token);
                    tracked.PreviewJson = previewJson;
                    tracked.DecisionsJson = "[]";
                    await db.SaveChangesAsync(token);
                    await transaction.CommitAsync(token);
                    return new(tracked.Id, tracked.EntityType, refreshed, true,
                        Deserialize<List<MigrationRowDecision>>(tracked.DecisionsJson), ReviewFingerprint(tracked));
                }
            }
            if (existingBatch.Status == MigrationBatchStatus.Previewed &&
                (request.EntityType == MigrationEntityType.Enrollments && HasStaleEnrollmentSnapshotErrors(existingBatch) ||
                 request.EntityType == MigrationEntityType.Charges))
            {
                List<NormalizedMigrationRow> refreshedPayloadRows;
                IReadOnlyDictionary<int, IReadOnlyList<string>> refreshedErrors;
                if (request.EntityType == MigrationEntityType.Enrollments)
                {
                    var materialized = await MaterializeEnrollmentSnapshotsAsync(session, records, token);
                    refreshedPayloadRows = materialized.Rows;
                    refreshedErrors = materialized.Errors;
                }
                else
                {
                    var materialized = await MaterializeChargeRowsAsync(session, records, token);
                    refreshedPayloadRows = materialized.Rows;
                    refreshedErrors = materialized.Errors;
                }
                var refreshedRows = await BuildPreviewAsync(session, request.EntityType, refreshedPayloadRows,
                    duplicateKeys, refreshedErrors, token);
                var refreshedPayloadJson = Serialize(refreshedPayloadRows);
                var refreshedPreviewJson = Serialize(refreshedRows);
                if (!JsonEquals(refreshedPayloadJson, existingBatch.PayloadJson) ||
                    !JsonEquals(refreshedPreviewJson, existingBatch.PreviewJson))
                {
                    var trackedBatch = await db.MigrationBatches.SingleOrDefaultAsync(batch =>
                        batch.Id == existingBatch.Id && batch.GymId == gymId &&
                        batch.MigrationSessionId == sessionId && batch.Status == MigrationBatchStatus.Previewed, token);
                    if (trackedBatch is not null)
                    {
                        trackedBatch.PayloadJson = refreshedPayloadJson;
                        trackedBatch.PreviewJson = refreshedPreviewJson;
                        trackedBatch.DecisionsJson = "[]";
                        await db.SaveChangesAsync(token);
                        await transaction.CommitAsync(token);
                        return new(trackedBatch.Id, trackedBatch.EntityType, refreshedRows, true,
                            Deserialize<List<MigrationRowDecision>>(trackedBatch.DecisionsJson), ReviewFingerprint(trackedBatch));
                    }
                }
            }
            await transaction.CommitAsync(token);
            return new(existingBatch.Id, existingBatch.EntityType,
                Deserialize<List<MigrationPreviewRow>>(existingBatch.PreviewJson), true,
                Deserialize<List<MigrationRowDecision>>(existingBatch.DecisionsJson), ReviewFingerprint(existingBatch));
        }

        IReadOnlyDictionary<int, IReadOnlyList<string>> contextErrors = new Dictionary<int, IReadOnlyList<string>>();
        if (request.EntityType == MigrationEntityType.Enrollments)
        {
            var materialized = await MaterializeEnrollmentSnapshotsAsync(session, records, token);
            records = materialized.Rows;
            contextErrors = materialized.Errors;
        }
        else if (request.EntityType == MigrationEntityType.Charges)
        {
            var materialized = await MaterializeChargeRowsAsync(session, records, token);
            records = materialized.Rows;
            contextErrors = materialized.Errors;
        }
        var rows = await BuildPreviewAsync(session, request.EntityType, records, duplicateKeys, contextErrors, token);
        var batch = new MigrationBatch
        {
            Id = Guid.NewGuid(), MigrationSessionId = sessionId, GymId = gymId,
            EntityType = request.EntityType, Status = MigrationBatchStatus.Previewed,
            Fingerprint = fingerprint, PayloadJson = Serialize(records), PreviewJson = Serialize(rows),
            CreatedAt = Now()
        };
        db.MigrationBatches.Add(batch);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(batch.Id, batch.EntityType, rows, false, [], ReviewFingerprint(batch));
    }

    public async Task ConfirmBatchAsync(Guid gymId, Guid sessionId, Guid batchId,
        ReviewMigrationBatchRequest request, CancellationToken token, Guid? actorId = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await LockPreparingSessionAsync(gymId, sessionId, token);
        var batch = await db.MigrationBatches.SingleOrDefaultAsync(item =>
            item.Id == batchId && item.GymId == gymId && item.MigrationSessionId == sessionId, token)
            ?? throw new KeyNotFoundException("Lote de migração não encontrado.");
        if (batch.Status == MigrationBatchStatus.Confirmed)
        {
            var openSession = await GetPreparingSessionAsync(gymId, sessionId, token);
            EnsureExpectedFingerprint(batch, request.Fingerprint);
            if (request.Decisions is null) throw new ArgumentException("Revise as decisões de cada linha.");
            var priorPreview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson);
            var ignored = request.Decisions.Where(decision => decision.Action == "Ignore").Select(decision => decision.RowNumber).ToHashSet();
            var latestPreview = await RefreshReviewPreviewAsync(openSession, batch, token,
                ignored);
            if (!string.Equals(request.Fingerprint, ReviewFingerprint(batch), StringComparison.Ordinal) &&
                !(batch.EntityType == MigrationEntityType.Charges && !HasRelevantFinancialPreviewChange(priorPreview, latestPreview, ignored)))
            {
                batch.DecisionsJson = "[]";
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                throw new InvalidOperationException("A prévia mudou desde a revisão. Reabra o lote e confira as decisões atualizadas.");
            }
            var validated = ValidateDecisions(request.Decisions, latestPreview, batch.EntityType);
            await ValidateDecisionTargetsAsync(gymId, sessionId, batch, validated, token);
            var revisedDecisions = Serialize(StampDecisions(validated, actorId));
            if (!JsonEquals(revisedDecisions, batch.DecisionsJson))
                await SaveConfirmedDecisionsAsync(gymId, sessionId, batch, revisedDecisions, token);
            await transaction.CommitAsync(token);
            return;
        }
        if (batch.Status != MigrationBatchStatus.Previewed)
            throw new InvalidOperationException("Este lote não pode mais ser revisado.");
        EnsureExpectedFingerprint(batch, request.Fingerprint);
        if (request.Decisions is null) throw new ArgumentException("Revise as decisões de cada linha.");
        var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson);
        if (batch.EntityType == MigrationEntityType.Charges)
        {
            var records = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var priorPreview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson)
                .ToDictionary(row => row.RowNumber);
            var ignoredRows = request.Decisions.Where(decision => decision.Action == "Ignore")
                .Select(decision => decision.RowNumber).ToHashSet();
            var materialized = await MaterializeChargeRowsAsync(
                await GetPreparingSessionAsync(gymId, sessionId, token), records, token, ignoredRows: ignoredRows);
            var duplicateKeys = records.Where(row => row.ExternalId is not null)
                .GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            preview = await BuildPreviewAsync(await GetPreparingSessionAsync(gymId, sessionId, token), batch.EntityType,
                materialized.Rows, duplicateKeys, materialized.Errors, token);
            var refreshedPayloadJson = Serialize(materialized.Rows);
            var refreshedPreviewJson = Serialize(preview);
            var relevantPreviewChanged = HasRelevantFinancialPreviewChange(priorPreview.Values.ToList(), preview, ignoredRows);
            if (relevantPreviewChanged)
            {
                batch.PayloadJson = refreshedPayloadJson;
                batch.PreviewJson = refreshedPreviewJson;
                batch.DecisionsJson = "[]";
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                throw new InvalidOperationException("A prévia financeira mudou desde a revisão. Reabra o lote e confira as decisões atualizadas.");
            }
            batch.PayloadJson = refreshedPayloadJson;
            batch.PreviewJson = refreshedPreviewJson;
            await db.SaveChangesAsync(token);
        }
        if (batch.EntityType == MigrationEntityType.Students)
            preview = await RefreshStudentPreviewAsync(await GetPreparingSessionAsync(gymId, sessionId, token), batch, token);
        if (batch.EntityType == MigrationEntityType.Enrollments)
            preview = await RefreshReviewPreviewAsync(await GetPreparingSessionAsync(gymId, sessionId, token), batch, token);
        if (batch.EntityType == MigrationEntityType.Plans)
        {
            preview = await RefreshPlanPreviewAsync(
                await GetPreparingSessionAsync(gymId, sessionId, token), batch, token);
        }
        if (batch.EntityType != MigrationEntityType.Charges &&
            !string.Equals(request.Fingerprint, ReviewFingerprint(batch), StringComparison.Ordinal))
        {
            batch.DecisionsJson = "[]";
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            throw new InvalidOperationException("A prévia mudou desde a revisão. Reabra o lote e confira as decisões atualizadas.");
        }
        var decisions = ValidateDecisions(request.Decisions, preview, batch.EntityType);
        await ValidateDecisionTargetsAsync(gymId, sessionId, batch, decisions, token);
        batch.DecisionsJson = Serialize(StampDecisions(decisions, actorId));
        batch.Status = MigrationBatchStatus.Confirmed;
        batch.ConfirmedAt = Now();
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task<MigrationBatchPreviewResponse> GetBatchReviewAsync(Guid gymId, Guid sessionId, Guid batchId,
        CancellationToken token)
    {
        await GetPreparingSessionAsync(gymId, sessionId, token);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await LockPreparingSessionAsync(gymId, sessionId, token);
        var batch = await db.MigrationBatches.SingleOrDefaultAsync(item =>
            item.Id == batchId && item.GymId == gymId && item.MigrationSessionId == sessionId, token)
            ?? throw new KeyNotFoundException("Lote de migração não encontrado.");
        if (batch.Status is not (MigrationBatchStatus.Previewed or MigrationBatchStatus.Confirmed))
            throw new InvalidOperationException("Somente lotes pendentes ou confirmados podem ser reabertos antes da liberação.");
        var session = await GetPreparingSessionAsync(gymId, sessionId, token);
        var preview = await RefreshReviewPreviewAsync(session, batch, token);
        await transaction.CommitAsync(token);
        return new(batch.Id, batch.EntityType, preview, true,
            Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson), ReviewFingerprint(batch));
    }

    public async Task SaveBatchReviewAsync(Guid gymId, Guid sessionId, Guid batchId,
        ReviewMigrationBatchRequest request, CancellationToken token, Guid? actorId = null)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await LockPreparingSessionAsync(gymId, sessionId, token);
        var batch = await db.MigrationBatches.SingleOrDefaultAsync(item =>
            item.Id == batchId && item.GymId == gymId && item.MigrationSessionId == sessionId, token)
            ?? throw new KeyNotFoundException("Lote de migração não encontrado.");
        var confirmedBatch = batch.Status == MigrationBatchStatus.Confirmed;
        if (batch.Status != MigrationBatchStatus.Previewed && !confirmedBatch)
            throw new InvalidOperationException("Somente lotes pendentes ou confirmados podem salvar decisões antes da liberação.");
        EnsureExpectedFingerprint(batch, request.Fingerprint);
        if (request.Decisions is null) throw new ArgumentException("Revise as decisões de cada linha.");
        var priorRows = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson);
        var ignoredRows = request.Decisions.Where(decision => decision.Action == "Ignore").Select(decision => decision.RowNumber).ToHashSet();
        var rows = await RefreshReviewPreviewAsync(await GetPreparingSessionAsync(gymId, sessionId, token), batch, token,
            ignoredRows);
        if (!string.Equals(request.Fingerprint, ReviewFingerprint(batch), StringComparison.Ordinal) &&
            !(batch.EntityType == MigrationEntityType.Charges && !HasRelevantFinancialPreviewChange(priorRows, rows, ignoredRows)))
        {
            batch.DecisionsJson = "[]";
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            throw new InvalidOperationException("A prévia mudou desde a revisão. Reabra o lote e confira as decisões atualizadas.");
        }
        var decisions = ValidateDecisions(request.Decisions, rows, batch.EntityType);
        await ValidateDecisionTargetsAsync(gymId, sessionId, batch, decisions, token);
        var decisionsJson = Serialize(StampDecisions(decisions, actorId));
        if (confirmedBatch)
            await SaveConfirmedDecisionsAsync(gymId, sessionId, batch, decisionsJson, token);
        else
        {
            batch.DecisionsJson = decisionsJson;
            await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
    }

    private async Task SaveConfirmedDecisionsAsync(Guid gymId, Guid sessionId, MigrationBatch batch,
        string decisionsJson, CancellationToken token)
    {
        var originalDecisions = batch.DecisionsJson;
        var changed = await db.MigrationBatches.Where(item => item.Id == batch.Id && item.GymId == gymId &&
                item.MigrationSessionId == sessionId && item.EntityType == batch.EntityType &&
                item.Status == MigrationBatchStatus.Confirmed && item.DecisionsJson == originalDecisions &&
                db.MigrationSessions.Any(session => session.Id == sessionId && session.GymId == gymId &&
                    session.Status == MigrationSessionStatus.Preparing && session.ExpiresAt > Now()))
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DecisionsJson, decisionsJson), token);
        if (changed != 1)
            throw new InvalidOperationException("O lote ou a sessão mudou durante a revisão. Recarregue a sessão antes de tentar novamente.");
        batch.DecisionsJson = decisionsJson;
    }

    public async Task DiscardBatchAsync(Guid gymId, Guid sessionId, Guid batchId, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        await LockPreparingSessionAsync(gymId, sessionId, token);

        var batch = await db.MigrationBatches.SingleOrDefaultAsync(item =>
            item.Id == batchId && item.GymId == gymId && item.MigrationSessionId == sessionId, token)
            ?? throw new KeyNotFoundException("Lote de migração não encontrado.");
        if (batch.Status == MigrationBatchStatus.Discarded) { await transaction.CommitAsync(token); return; }
        if (batch.Status != MigrationBatchStatus.Previewed)
            throw new InvalidOperationException("Somente lotes ainda não confirmados podem ser descartados.");

        var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson)
            .Select(row => row with { Values = new Dictionary<string, string?>(), Candidates = [] })
            .ToList();
        batch.Status = MigrationBatchStatus.Discarded;
        batch.PayloadJson = "[]";
        batch.PreviewJson = Serialize(preview);
        batch.DecisionsJson = "[]";
        batch.ResultJson = "[]";
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }

    public async Task<MigrationSessionResponse> GetSessionAsync(Guid gymId, Guid sessionId, CancellationToken token)
    {
        var session = await db.MigrationSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sessionId && item.GymId == gymId, token)
            ?? throw new KeyNotFoundException("Sessão de migração não encontrada.");
        var batches = await db.MigrationBatches.AsNoTracking()
            .Where(item => item.MigrationSessionId == sessionId && item.GymId == gymId)
            .OrderBy(item => item.CreatedAt).ToListAsync(token);
        var blockers = await GetReleaseBlockersAsync(session, batches, token);
        var resultDocuments = batches.Where(batch => batch.Status == MigrationBatchStatus.Released)
            .ToDictionary(batch => batch.Id, batch => DeserializeResultDocument(batch.ResultJson));
        var results = batches.Where(batch => batch.Status == MigrationBatchStatus.Released)
            .SelectMany(batch => resultDocuments[batch.Id].Rows).ToList();
        return new(session.Id, session.Name, session.SourceNamespace, session.Status, session.ExpiresAt,
            batches.Select(batch =>
            {
                var rows = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson);
                var resultDocument = resultDocuments.GetValueOrDefault(batch.Id);
                return new MigrationBatchSummary(batch.Id, batch.EntityType,
                    resultDocument?.Rows.Count ?? rows.Count,
                    resultDocument?.Totals.ErrorCount ?? rows.Count(row => !row.IsValid),
                    rows.Count(row => row.Candidates.Count != 0),
                    batch.Status.ToString(), ReviewFingerprint(batch), resultDocument?.Totals);
            }).ToList(),
            session.Status == MigrationSessionStatus.Preparing && batches.Count > 0 && blockers.Count == 0,
            blockers, results);
    }

    public Task<List<MigrationSessionListItem>> ListSessionsAsync(Guid gymId, CancellationToken token) =>
        db.MigrationSessions.AsNoTracking()
            .Where(session => session.GymId == gymId)
            .OrderByDescending(session => session.CreatedAt)
            .Select(session => new MigrationSessionListItem(
                session.Id,
                session.Name,
                session.SourceNamespace,
                session.Status,
                session.CreatedAt,
                session.ExpiresAt,
                db.MigrationBatches.Count(batch => batch.GymId == gymId && batch.MigrationSessionId == session.Id)))
            .ToListAsync(token);

    public async Task<MigrationSessionResponse> ReleaseAsync(Guid gymId, Guid sessionId, Guid actorId, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
        var now = Now();
        var claimed = await db.MigrationSessions.Where(item => item.Id == sessionId && item.GymId == gymId &&
                item.Status == MigrationSessionStatus.Preparing && item.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Status, MigrationSessionStatus.Releasing), token);
        if (claimed != 1)
        {
            var current = await db.MigrationSessions.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == sessionId && item.GymId == gymId, token)
                ?? throw new KeyNotFoundException("Sessão de migração não encontrada.");
            if (current.Status == MigrationSessionStatus.Released)
            {
                await transaction.CommitAsync(token);
                return await GetSessionAsync(gymId, sessionId, token);
            }
            throw new InvalidOperationException("A sessão expirou ou já está sendo liberada.");
        }
        var session = await db.MigrationSessions.SingleAsync(item => item.Id == sessionId && item.GymId == gymId, token);
        var batches = await db.MigrationBatches.Where(item => item.MigrationSessionId == sessionId && item.GymId == gymId)
            .OrderBy(item => item.EntityType).ThenBy(item => item.CreatedAt).ToListAsync(token);
        var blockers = await GetReleaseBlockersAsync(session, batches, token);
        if (blockers.Count != 0) throw new InvalidOperationException(string.Join(" ", blockers));

        var referenceRows = new List<MigrationExternalReference>();
        var batchResults = new Dictionary<Guid, List<MigrationBatchResult>>();
        var existingRefs = await db.MigrationExternalReferences.AsNoTracking()
            .Where(reference => reference.GymId == gymId && reference.SourceNamespace == session.SourceNamespace)
            .ToDictionaryAsync(reference => (reference.EntityType, reference.ExternalId), reference => reference.TargetEntityId, token);
        var targetIds = new Dictionary<(MigrationEntityType, string), Guid>(existingRefs);
        var sessionResults = new List<MigrationBatchResult>();
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Now(), timeZones.GetTimeZone(gymId)));

        foreach (var batch in batches.OrderBy(item => item.EntityType).ThenBy(item => item.CreatedAt))
        {
            if (batch.Status == MigrationBatchStatus.Discarded) continue;
            var records = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            var previews = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(item => item.RowNumber);
            var rows = new List<MigrationBatchResult>();
            foreach (var row in records)
            {
                var action = decisions[row.RowNumber];
                if (!previews.TryGetValue(row.RowNumber, out var preview))
                    throw new InvalidOperationException($"A prévia da linha {row.RowNumber} não está disponível para consolidar o resultado.");
                var requiredHumanReview = MigrationBatchResultTotalsCalculator.RequiresHumanReview(preview);
                if (action.Action == "Ignore")
                {
                    rows.Add(Result("Ignored", "Linha ignorada pelo Admin.", null));
                    continue;
                }
                var refKey = (batch.EntityType, row.ExternalId!);
                if (targetIds.TryGetValue(refKey, out var priorTarget))
                {
                    if (action.Action == "LinkExisting" && action.TargetId != priorTarget)
                        throw new InvalidOperationException($"O alias '{row.ExternalId}' já está vinculado a outro destino; revise a decisão antes de liberar.");
                    ValidateLinkedTarget(batch.EntityType, priorTarget, gymId);
                    await ValidateResolvedOwnershipAsync(gymId, batch.EntityType, row, priorTarget, targetIds, token);
                    rows.Add(Result("AlreadyLinked", "Identificador externo já vinculado; nenhum dado foi sobrescrito.", priorTarget));
                    continue;
                }

                var targetId = action.Action == "LinkExisting"
                    ? action.TargetId!.Value
                    : await CreateTargetAsync(gymId, batch.EntityType, row, action, targetIds, localToday, token);
                ValidateLinkedTarget(batch.EntityType, targetId, gymId);
                await ValidateResolvedOwnershipAsync(gymId, batch.EntityType, row, targetId, targetIds, token);
                targetIds.Add(refKey, targetId);
                referenceRows.Add(new MigrationExternalReference
                {
                    Id = Guid.NewGuid(), GymId = gymId, MigrationSessionId = sessionId,
                    SourceNamespace = session.SourceNamespace, EntityType = batch.EntityType,
                    ExternalId = row.ExternalId!, TargetEntityId = targetId, CreatedAt = Now()
                });
                rows.Add(Result(action.Action == "LinkExisting" ? "Linked" : "Imported",
                    action.Action == "LinkExisting" ? "Registro vinculado sem alterar dados existentes." : "Registro importado após liberação.", targetId));

                MigrationBatchResult Result(string status, string message, Guid? id) => new(batch.EntityType,
                    row.RowNumber, status, message, id, row.ExternalId, action.Action, action.FinancialStateResolution,
                    action.ReviewedByUserId, action.ReviewedAt,
                    batch.EntityType == MigrationEntityType.Students ? id :
                    row.StudentExternalId is not null && targetIds.TryGetValue((MigrationEntityType.Students, row.StudentExternalId), out var owner)
                        ? owner : null, requiredHumanReview);
            }
            batchResults[batch.Id] = rows;
            sessionResults.AddRange(rows);
        }

        db.MigrationExternalReferences.AddRange(referenceRows);
        await db.SaveChangesAsync(token);
        session.Status = MigrationSessionStatus.Released;
        session.ReleasedAt = Now();
        session.ReleasedByUserId = actorId;
        foreach (var batch in batches)
        {
            if (batch.Status == MigrationBatchStatus.Discarded) continue;
            batch.Status = MigrationBatchStatus.Released;
            batch.ReleasedAt = Now();
            batch.PayloadJson = "[]";
            batch.PreviewJson = "[]";
            batch.DecisionsJson = "[]";
            var resultRows = batchResults[batch.Id];
            var totals = MigrationBatchResultTotalsCalculator.Calculate(resultRows);
            batch.ResultJson = Serialize(new MigrationBatchResultDocument(resultRows, totals));
        }
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return await GetSessionAsync(gymId, sessionId, token);
    }

    public Task<int> RemoveExpiredSessionsAsync(CancellationToken token) => db.MigrationSessions
        .Where(session => session.Status == MigrationSessionStatus.Preparing && session.ExpiresAt <= Now())
        .ExecuteDeleteAsync(token);

    private async Task<(List<NormalizedMigrationRow> Rows, IReadOnlyDictionary<int, IReadOnlyList<string>> Errors)>
        MaterializeEnrollmentSnapshotsAsync(MigrationSession session, List<NormalizedMigrationRow> records, CancellationToken token)
    {
        var studentIdentities = await ResolveStudentIdentitiesAsync(session, token);
        var terms = new Dictionary<string, PlanTerms>(StringComparer.Ordinal);
        var priorReferences = await db.MigrationExternalReferences.AsNoTracking()
            .Where(reference => reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace &&
                reference.EntityType == MigrationEntityType.Plans)
            .Join(db.Plans.AsNoTracking().Where(plan => plan.GymId == session.GymId),
                reference => reference.TargetEntityId, plan => plan.Id,
                (reference, plan) => new { reference.ExternalId, Terms = PlanTermsFromEntity(plan) })
            .ToListAsync(token);
        foreach (var reference in priorReferences) terms.TryAdd(reference.ExternalId, reference.Terms);

        var planBatches = await db.MigrationBatches.AsNoTracking()
            .Where(batch => batch.GymId == session.GymId && batch.MigrationSessionId == session.Id &&
                batch.EntityType == MigrationEntityType.Plans && batch.Status == MigrationBatchStatus.Confirmed)
            .OrderBy(batch => batch.CreatedAt).ToListAsync(token);
        var linkedPlanIds = new List<Guid>();
        var staged = new List<(NormalizedMigrationRow Row, MigrationRowDecision Decision)>();
        foreach (var batch in planBatches)
        {
            var batchRows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(row => row.RowNumber);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(decision => decision.RowNumber);
            foreach (var row in batchRows)
            {
                if (row.ExternalId is null || !preview.TryGetValue(row.RowNumber, out var previewRow) || !previewRow.IsValid ||
                    !decisions.TryGetValue(row.RowNumber, out var decision) || decision.Action == "Ignore" ||
                    terms.ContainsKey(row.ExternalId)) continue;
                staged.Add((row, decision));
                if (decision.Action == "LinkExisting" && decision.TargetId is { } targetId) linkedPlanIds.Add(targetId);
            }
        }

        var linkedPlans = linkedPlanIds.Count == 0
            ? new Dictionary<Guid, PlanTerms>()
            : await db.Plans.AsNoTracking().Where(plan => plan.GymId == session.GymId && linkedPlanIds.Contains(plan.Id))
                .ToDictionaryAsync(plan => plan.Id, PlanTermsFromEntity, token);
        foreach (var (row, decision) in staged)
        {
            if (row.ExternalId is null || terms.ContainsKey(row.ExternalId)) continue;
            if (decision.Action == "ImportNew" && row.Name is not null && row.Price is not null &&
                row.DurationMonths is not null && row.BillingCycle is not null)
                terms.TryAdd(row.ExternalId, new(row.Name, row.Price, row.DurationMonths, row.BillingCycle));
            else if (decision.Action == "LinkExisting" && decision.TargetId is { } targetId && linkedPlans.TryGetValue(targetId, out var linkedPlan))
                terms.TryAdd(row.ExternalId, linkedPlan);
        }

        var errors = new Dictionary<int, IReadOnlyList<string>>();
        var materializedRows = records.Select(row =>
        {
            var rowErrors = new List<string>();
            var studentKey = row.StudentExternalId is not null && studentIdentities.TryGetValue(row.StudentExternalId, out var identity)
                ? identity : null;
            if (studentKey is null)
                rowErrors.Add("Aluno externo não localizado em vínculo anterior ou lote confirmado desta academia e origem.");
            var resolvedRow = row with { ResolvedStudentKey = studentKey };
            if (row.PlanExternalId is null || !terms.TryGetValue(row.PlanExternalId, out var planTerms))
            {
                rowErrors.Add("Plano externo não localizado em vínculo anterior ou lote confirmado desta academia e origem.");
                errors[row.RowNumber] = rowErrors;
                return resolvedRow;
            }
            if (rowErrors.Count != 0) errors[row.RowNumber] = rowErrors;
            return resolvedRow with
            {
                Name = row.Name ?? planTerms.Name,
                Price = row.Price ?? planTerms.Price,
                DurationMonths = row.DurationMonths ?? planTerms.DurationMonths,
                BillingCycle = row.BillingCycle ?? planTerms.BillingCycle
            };
        }).ToList();
        return (materializedRows, errors);
    }

    private static PlanTerms PlanTermsFromEntity(Plan plan) => new(plan.Name,
        plan.Price.ToString("0.00", CultureInfo.InvariantCulture),
        plan.DurationMonths.ToString(CultureInfo.InvariantCulture), plan.BillingCycle.ToString());

    // External IDs are opaque, case-sensitive keys within the session's tenant/source.
    // A staged creation has no StudentId yet; it must not merge with another creation.
    private static string StudentKey(Guid studentId) => $"student:{studentId:D}";

    private async Task<Dictionary<string, string>> ResolveStudentIdentitiesAsync(MigrationSession session,
        CancellationToken token)
    {
        var references = await db.MigrationExternalReferences.AsNoTracking()
            .Where(reference => reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace &&
                reference.EntityType == MigrationEntityType.Students)
            .Join(db.Students.AsNoTracking().Where(student => student.GymId == session.GymId),
                reference => reference.TargetEntityId, student => student.Id,
                (reference, student) => new { reference.ExternalId, student.Id }).ToListAsync(token);
        var identities = references.ToDictionary(reference => reference.ExternalId,
            reference => StudentKey(reference.Id), StringComparer.Ordinal);
        var persistedAliases = identities.Keys.ToHashSet(StringComparer.Ordinal);
        var batches = await db.MigrationBatches.AsNoTracking().Where(batch => batch.GymId == session.GymId &&
                batch.MigrationSessionId == session.Id && batch.EntityType == MigrationEntityType.Students &&
                batch.Status == MigrationBatchStatus.Confirmed).ToListAsync(token);
        var students = await db.Students.AsNoTracking().Where(student => student.GymId == session.GymId)
            .Select(student => student.Id).ToListAsync(token);
        var validTargets = students.ToHashSet();
        foreach (var batch in batches)
        {
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(item => item.RowNumber);
            foreach (var row in Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson))
            {
                if (row.ExternalId is null || !decisions.TryGetValue(row.RowNumber, out var decision) ||
                    decision.Action == "Ignore" || !preview.TryGetValue(row.RowNumber, out var reviewed) || !reviewed.IsValid) continue;
                var key = decision.Action == "LinkExisting" && decision.TargetId is { } targetId && validTargets.Contains(targetId)
                    ? StudentKey(targetId) : decision.Action == "ImportNew" ? $"staged-student:{row.ExternalId}" : null;
                if (key is null)
                    throw new InvalidOperationException($"Aluno externo '{row.ExternalId}' na linha {row.RowNumber}: destino inexistente ou fora desta academia.");
                if (identities.TryGetValue(row.ExternalId, out var known))
                {
                    if (known != key && (decision.Action == "LinkExisting" || !persistedAliases.Contains(row.ExternalId)))
                        throw new InvalidOperationException($"O alias de aluno '{row.ExternalId}' possui decisões contraditórias. Revise os lotes antes de continuar.");
                    continue; // ImportNew for an already released alias is an idempotent no-op.
                }
                identities.Add(row.ExternalId, key);
            }
        }
        return identities;
    }

    private async Task<(List<NormalizedMigrationRow> Rows, IReadOnlyDictionary<int, IReadOnlyList<string>> Errors,
        IReadOnlyDictionary<int, string> TargetKeys)>
        MaterializeChargeRowsAsync(MigrationSession session, List<NormalizedMigrationRow> records, CancellationToken token,
            bool checkConfirmedFinancialBatches = true, IReadOnlySet<int>? ignoredRows = null, Guid? excludedBatchId = null)
    {
        var studentIdentities = await ResolveStudentIdentitiesAsync(session, token);
        var bindings = new Dictionary<string, ChargeBinding>(StringComparer.Ordinal);
        var refs = await db.MigrationExternalReferences.AsNoTracking()
            .Where(reference => reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace &&
                reference.EntityType == MigrationEntityType.Enrollments)
            .Join(db.Enrollments.AsNoTracking().Where(enrollment => enrollment.Student.GymId == session.GymId && enrollment.Plan.GymId == session.GymId),
                reference => reference.TargetEntityId, enrollment => enrollment.Id,
                (reference, enrollment) => new { reference.ExternalId, enrollment.Id, enrollment.StudentId, enrollment.Status, enrollment.Charge })
            .ToListAsync(token);
        var financialTargets = refs.GroupBy(item => item.Id).ToDictionary(group => group.Key,
            group => new FinancialTarget(group.Key, group.First().StudentId, group.First().Status, group.First().Charge));
        foreach (var item in refs)
        {
            bindings.TryAdd(item.ExternalId, new(item.Id, StudentKey(item.StudentId),
                item.Charge is null ? null : ChargeStateName(item.Charge.Status, item.Charge.VerificationStatus), item.Charge?.Id));
        }

        var confirmedEnrollmentBatches = await db.MigrationBatches.AsNoTracking()
            .Where(batch => batch.GymId == session.GymId && batch.MigrationSessionId == session.Id &&
                batch.EntityType == MigrationEntityType.Enrollments && batch.Status == MigrationBatchStatus.Confirmed)
            .OrderBy(batch => batch.CreatedAt).ToListAsync(token);
        foreach (var batch in confirmedEnrollmentBatches)
        {
            var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(row => row.RowNumber);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(decision => decision.RowNumber);
            foreach (var enrollmentRow in Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson))
            {
                if (enrollmentRow.ExternalId is null || !preview.TryGetValue(enrollmentRow.RowNumber, out var p) || !p.IsValid ||
                    !decisions.TryGetValue(enrollmentRow.RowNumber, out var d) || d.Action == "Ignore" ||
                    bindings.ContainsKey(enrollmentRow.ExternalId)) continue;
                if (enrollmentRow.StudentExternalId is null || !studentIdentities.TryGetValue(enrollmentRow.StudentExternalId, out var studentIdentity))
                    continue;
                if (d.Action == "ImportNew")
                    bindings.TryAdd(enrollmentRow.ExternalId, new(null, studentIdentity, enrollmentRow.ChargeState, null));
                else if (d.Action == "LinkExisting" && d.TargetId is { } targetId)
                {
                    var linked = await db.Enrollments.AsNoTracking()
                        .Where(enrollment => enrollment.Id == targetId && enrollment.Student.GymId == session.GymId && enrollment.Plan.GymId == session.GymId)
                        .Select(enrollment => new { enrollment.Id, enrollment.StudentId, enrollment.Status, enrollment.Charge })
                        .SingleOrDefaultAsync(token);
                    if (linked is null) continue;
                    financialTargets.TryAdd(linked.Id, new(linked.Id, linked.StudentId, linked.Status, linked.Charge));
                    bindings.TryAdd(enrollmentRow.ExternalId, new(linked.Id, StudentKey(linked.StudentId),
                        linked.Charge is null ? enrollmentRow.ChargeState : ChargeStateName(linked.Charge.Status, linked.Charge.VerificationStatus),
                        linked.Charge?.Id));
                }
            }
        }

        var chargeRefs = await db.MigrationExternalReferences.AsNoTracking()
            .Where(reference => reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace &&
                reference.EntityType == MigrationEntityType.Charges)
            .ToDictionaryAsync(reference => reference.ExternalId, reference => reference.TargetEntityId, token);
        var requestedChargeIds = records.Where(row => row.ChargeExternalId is not null && chargeRefs.ContainsKey(row.ChargeExternalId))
            .Select(row => chargeRefs[row.ChargeExternalId!]).Distinct().ToArray();
        var chargeTargets = await db.Charges.AsNoTracking().Where(charge => requestedChargeIds.Contains(charge.Id) &&
                charge.Enrollment.Student.GymId == session.GymId && charge.Enrollment.Plan.GymId == session.GymId)
            .Select(charge => new FinancialTarget(charge.EnrollmentId, charge.Enrollment.StudentId, charge.Enrollment.Status, charge))
            .ToListAsync(token);
        var chargesById = chargeTargets.ToDictionary(target => target.Charge!.Id);
        foreach (var target in chargeTargets) financialTargets.TryAdd(target.EnrollmentId, target);
        var errors = new Dictionary<int, IReadOnlyList<string>>();
        var rows = new List<NormalizedMigrationRow>(records.Count);
        var resolvedTargets = new List<(int RowNumber, string TargetKey)>();
        foreach (var row in records)
        {
            var messages = new List<string>();
            ChargeBinding? binding = null;
            var studentIdentity = row.StudentExternalId is not null && studentIdentities.TryGetValue(row.StudentExternalId, out var resolvedStudent)
                ? resolvedStudent : null;
            if (studentIdentity is null)
                messages.Add("Aluno externo não localizado em lote confirmado ou vínculo desta academia e origem.");
            if (row.EnrollmentExternalId is not null && !bindings.TryGetValue(row.EnrollmentExternalId, out binding))
                messages.Add("Matrícula externa não localizada em lote confirmado ou vínculo desta academia e origem.");
            ChargeBinding? chargeBinding = null;
            if (row.ChargeExternalId is not null)
            {
                if (!chargeRefs.TryGetValue(row.ChargeExternalId, out var chargeId))
                    messages.Add("Cobrança externa não localizada em vínculo desta academia e origem.");
                else
                {
                    if (!chargesById.TryGetValue(chargeId, out var linkedCharge)) messages.Add("A cobrança vinculada não pertence à academia desta sessão.");
                    else
                    {
                        chargeBinding = new(linkedCharge.EnrollmentId, StudentKey(linkedCharge.StudentId),
                            ChargeStateName(linkedCharge.Charge!.Status, linkedCharge.Charge.VerificationStatus), chargeId);
                    }
                }
            }
            if (binding is not null && chargeBinding is not null && binding.EnrollmentId != chargeBinding.EnrollmentId)
                messages.Add("Os identificadores de matrícula e cobrança apontam para matrículas diferentes.");
            binding ??= chargeBinding;
            var knownCharge = row.ExternalId is not null && chargeRefs.TryGetValue(row.ExternalId, out var priorCharge)
                ? (Guid?)priorCharge : null;
            var isNoOp = knownCharge is not null && knownCharge == binding?.ChargeId;
            if (knownCharge is not null && !isNoOp)
                messages.Add("O identificador externo desta cobrança já está vinculado a outra matrícula/cobrança. O alias não pode ser redirecionado.");
            if (binding is not null && GetFinancialTargetKey(binding, row) is { } targetKey)
                resolvedTargets.Add((row.RowNumber, targetKey));
            if (!isNoOp && binding?.EnrollmentId is { } targetEnrollmentId)
            {
                if (financialTargets.TryGetValue(targetEnrollmentId, out var financialTarget) && financialTarget.Charge is { } targetCharge &&
                    GetFinancialMutationBlockReason(financialTarget.Status, targetCharge.Origin, targetCharge.PaymentOrigin,
                        targetCharge.PaidAmount, targetCharge.PaidAt, targetCharge.PaymentMethod, targetCharge.DiscountAmount) is { } blockReason)
                    messages.Add(blockReason);
            }
            if (row.EnrollmentExternalId is null && row.ChargeExternalId is null)
            {
                var count = bindings.Where(candidate => candidate.Value.StudentKey == studentIdentity)
                    .Select(candidate => candidate.Value.EnrollmentId is { } id ? $"enrollment:{id:D}" : $"external-enrollment:{candidate.Key}")
                    .Distinct(StringComparer.Ordinal).Count();
                messages.Add(count > 1
                    ? "Aluno possui múltiplas matrículas; informe o ID externo da matrícula ou da cobrança para resolver a ambiguidade."
                    : "Informe o ID externo da matrícula ou da cobrança; o vínculo não será presumido pelo aluno.");
            }
            if (binding is not null && binding.StudentKey != studentIdentity)
                messages.Add("O aluno externo informado não corresponde à matrícula/cobrança vinculada.");
            if (!isNoOp && binding?.ChargeState is { } previousState && row.ChargeState is { } importedState &&
                TryChargeState(previousState, out var prior) && TryChargeState(importedState, out var incoming) && prior != incoming)
                messages.Add($"Conflito de situação financeira: matrícula/cobrança={previousState}; arquivo={importedState}. Escolha explicitamente qual estado manter.");
            var materialized = row with
            {
                EnrollmentChargeState = isNoOp ? null : binding?.ChargeState,
                ResolvedStudentKey = studentIdentity,
                ResolvedFinancialTargetKey = binding is null ? null : GetFinancialTargetKey(binding, row),
                HasFinancialStateConflict = messages.Any(message => message.StartsWith("Conflito de situação financeira:", StringComparison.Ordinal))
            };
            rows.Add(materialized);
            if (messages.Count != 0) errors[row.RowNumber] = messages;
        }

        var currentTargets = resolvedTargets.Where(item => ignoredRows?.Contains(item.RowNumber) != true)
            .GroupBy(item => item.TargetKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).ToDictionary(group => group.Key,
                group => group.Select(item => item.RowNumber).ToHashSet(), StringComparer.Ordinal);
        foreach (var duplicate in currentTargets.Values.SelectMany(rowNumbers => rowNumbers.Select(rowNumber => (rowNumber, rowNumbers))))
            AddRowError(errors, duplicate.rowNumber,
                $"Mais de uma linha financeira deste arquivo aponta para a mesma matrícula/cobrança (linhas {string.Join(", ", duplicate.rowNumbers.Order())}). Mantenha uma única linha por cobrança.");

        if (checkConfirmedFinancialBatches)
        {
            var confirmedBatches = await db.MigrationBatches.AsNoTracking()
                .Where(batch => batch.GymId == session.GymId && batch.MigrationSessionId == session.Id &&
                    batch.EntityType == MigrationEntityType.Charges && batch.Status == MigrationBatchStatus.Confirmed &&
                    (excludedBatchId == null || batch.Id != excludedBatchId))
                .ToListAsync(token);
            var confirmedTargets = new HashSet<string>(StringComparer.Ordinal);
            foreach (var batch in confirmedBatches)
            {
                var priorRows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
                var prior = await MaterializeChargeRowsAsync(session, priorRows, token, checkConfirmedFinancialBatches: false);
                var priorDecisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
                foreach (var row in prior.Rows)
                {
                    if (!priorDecisions.TryGetValue(row.RowNumber, out var decision) || decision.Action == "Ignore") continue;
                    if (prior.TargetKeys.TryGetValue(row.RowNumber, out var target)) confirmedTargets.Add(target);
                }
            }
            foreach (var (rowNumber, targetKey) in resolvedTargets
                         .Where(item => ignoredRows?.Contains(item.RowNumber) != true && confirmedTargets.Contains(item.TargetKey)))
                AddRowError(errors, rowNumber,
                    "Outra linha financeira já confirmada nesta sessão aponta para a mesma matrícula/cobrança. Ignore esta linha ou mantenha somente um lote financeiro para esse alvo.");
        }
        return (rows, errors, resolvedTargets.GroupBy(item => item.RowNumber)
            .ToDictionary(group => group.Key, group => group.First().TargetKey));
    }

    private static void AddRowError(Dictionary<int, IReadOnlyList<string>> errors, int rowNumber, string message)
    {
        var messages = errors.TryGetValue(rowNumber, out var existing) ? existing.ToList() : [];
        if (!messages.Contains(message, StringComparer.Ordinal)) messages.Add(message);
        errors[rowNumber] = messages;
    }

    private static string? GetFinancialTargetKey(ChargeBinding binding, NormalizedMigrationRow row) =>
        binding.EnrollmentId is { } enrollmentId ? $"enrollment:{enrollmentId:D}" :
        binding.ChargeId is { } chargeId ? $"charge:{chargeId:D}" :
        row.EnrollmentExternalId is { Length: > 0 } externalId ? $"external-enrollment:{externalId}" : null;

    private static string ChargeStateName(ChargeStatus status, ChargeVerificationStatus verificationStatus) =>
        status == ChargeStatus.Paid ? "paid" : verificationStatus == ChargeVerificationStatus.Unverified ? "unverified" : "pending";

    private static string? GetFinancialMutationBlockReason(EnrollmentStatus enrollmentStatus, ChargeOrigin origin,
        PaymentOrigin paymentOrigin, decimal? paidAmount, DateTime? paidAt, PaymentMethod? paymentMethod, decimal discountAmount)
    {
        if (enrollmentStatus == EnrollmentStatus.Cancelled)
            return "A matrícula está cancelada; a importação financeira não pode alterar ou reativar esse vínculo.";
        if (origin != ChargeOrigin.Migration || paymentOrigin == PaymentOrigin.Avelri || paidAmount is not null ||
            paidAt is not null || paymentMethod is not null || discountAmount != 0m)
            return "A cobrança contém estado operacional ou recebimento do Avelri e não pode ser sobrescrita por uma migração.";
        return null;
    }

    private sealed record ChargeBinding(Guid? EnrollmentId, string StudentKey, string? ChargeState, Guid? ChargeId);
    private sealed record FinancialTarget(Guid EnrollmentId, Guid StudentId, EnrollmentStatus Status, Charge? Charge);

    private static bool HasStaleEnrollmentSnapshotErrors(MigrationBatch batch) =>
        Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).Any(row => row.Errors.Any(error =>
            error.Contains("Plano externo não localizado", StringComparison.Ordinal) ||
            error.Contains("Aluno externo não localizado", StringComparison.Ordinal) ||
            error.Contains("Preço/valor contratado", StringComparison.Ordinal) ||
            error.Contains("Duração em meses inválida", StringComparison.Ordinal) ||
            error.Contains("Ciclo de cobrança inválido", StringComparison.Ordinal) ||
            error.Contains("Nome do plano no snapshot contratual obrigatório", StringComparison.Ordinal)));

    private async Task<List<MigrationPreviewRow>> BuildPreviewAsync(MigrationSession session,
        MigrationEntityType entityType, List<NormalizedMigrationRow> records, HashSet<string> duplicateKeys,
        IReadOnlyDictionary<int, IReadOnlyList<string>> contextErrors, CancellationToken token,
        Guid? excludedBatchId = null)
    {
        var linked = await db.MigrationExternalReferences.AsNoTracking().Where(reference =>
                reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace && reference.EntityType == entityType)
            .ToDictionaryAsync(reference => reference.ExternalId, token);
        var existingStudents = entityType == MigrationEntityType.Students
            ? await db.Students.AsNoTracking().Where(student => student.GymId == session.GymId)
                .OrderBy(student => student.Id)
                .Select(student => new StudentCandidate(student.Id, student.Name, student.Email, student.Phone, student.ArchivedAt != null,
                    student.IsActive, student.AppAccessEnabled, student.UserId, student.User == null ? null : (bool?)student.User.IsActive)).ToListAsync(token)
            : [];
        var planNames = entityType == MigrationEntityType.Plans
            ? records.Where(row => !string.IsNullOrWhiteSpace(row.Name)).Select(row => row.Name!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        var existingPlans = entityType == MigrationEntityType.Plans
            ? await GetExistingPlansMatchingNamesAsync(session.GymId, planNames, token)
            : [];
        var priorPlanNames = entityType == MigrationEntityType.Plans
            ? await GetOtherStagedPlanNamesAsync(session, excludedBatchId, token)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<MigrationPreviewRow>(records.Count);
        foreach (var row in records)
        {
            var errors = ValidateRow(entityType, row);
            if (contextErrors.TryGetValue(row.RowNumber, out var rowContextErrors)) errors.AddRange(rowContextErrors);
            if (row.ExternalId is not null && duplicateKeys.Contains(row.ExternalId)) errors.Add("Identificador externo repetido neste arquivo.");
            var candidates = new List<MigrationCandidate>();
            if (row.ExternalId is not null && linked.TryGetValue(row.ExternalId, out var prior))
            {
                candidates.Add(new(prior.TargetEntityId, null, "Vínculo existente", "Este identificador já foi liberado anteriormente.", false));
                if (entityType == MigrationEntityType.Students && existingStudents.All(student => student.Id != prior.TargetEntityId))
                    errors.Add("O alias de aluno possui destino inexistente ou fora desta academia. Corrija o vínculo antes de continuar.");
            }
            if (errors.Count == 0 && entityType == MigrationEntityType.Students)
            {
                foreach (var student in existingStudents)
                {
                    var email = row.Email is not null && string.Equals(row.Email, student.Email, StringComparison.OrdinalIgnoreCase);
                    var phone = Digits(row.Phone).Length >= 10 && Digits(row.Phone) == Digits(student.Phone);
                    if (email || phone) candidates.Add(new(student.Id, null, student.Name,
                        email && phone ? "E-mail e telefone" : email ? "E-mail" : "Telefone", student.IsArchived));
                }
                foreach (var other in records.Where(other => other.RowNumber != row.RowNumber && row.Email is not null &&
                    string.Equals(row.Email, other.Email, StringComparison.OrdinalIgnoreCase) ||
                    other.RowNumber != row.RowNumber && Digits(row.Phone).Length >= 10 && Digits(row.Phone) == Digits(other.Phone)))
                    candidates.Add(new(null, other.RowNumber, other.Name ?? "", "Possível repetição no arquivo", false));
            }
            if (errors.Count == 0 && entityType == MigrationEntityType.Plans)
            {
                foreach (var plan in existingPlans.Where(plan => string.Equals(plan.Name, row.Name, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(new(plan.Id, null, plan.Name, $"Nome existente · {plan.Price:0.00} · {plan.DurationMonths} meses", false));
                foreach (var other in records.Where(other => other.RowNumber != row.RowNumber &&
                    string.Equals(other.Name, row.Name, StringComparison.OrdinalIgnoreCase)))
                    candidates.Add(new(null, other.RowNumber, other.Name ?? "", "Nome repetido no arquivo", false));
                if (row.Name is { } planName && priorPlanNames.Contains(planName))
                    errors.Add("Já existe um plano com este nome em outro lote da sessão. Vincule a um plano existente ou ignore/corrija este registro antes de confirmar.");
            }
            var values = new Dictionary<string, string?>(DisplayValues(row));
            if (entityType == MigrationEntityType.Students)
            {
                var candidateIds = candidates.Where(candidate => candidate.TargetId is not null).Select(candidate => candidate.TargetId!.Value).ToHashSet();
                values["studentIdentityContext"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                    Serialize(existingStudents.Where(student => candidateIds.Contains(student.Id))))));
            }
            results.Add(new(row.RowNumber, errors.Count == 0, errors, candidates.DistinctBy(candidate =>
                $"{candidate.TargetId}:{candidate.SourceRowNumber}:{candidate.Description}").ToList(), values,
                row.HasFinancialStateConflict, row.ExternalId is not null && linked.TryGetValue(row.ExternalId, out var known) ? known.TargetEntityId : null));
        }
        return results;
    }

    private static List<string> ValidateRow(MigrationEntityType type, NormalizedMigrationRow row)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(row.ExternalId)) errors.Add("ID externo obrigatório para relacionar arquivos.");
        if (row.ExternalId?.Length > 200) errors.Add("ID externo excede o limite de 200 caracteres do vínculo externo.");
        if (new[] { row.StudentExternalId, row.PlanExternalId, row.EnrollmentExternalId, row.ChargeExternalId }
            .Any(value => value?.Length > 200))
            errors.Add("Um identificador externo relacionado excede o limite de 200 caracteres.");
        if (type is MigrationEntityType.Students or MigrationEntityType.Plans)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) errors.Add(type == MigrationEntityType.Students ? "Nome obrigatório." : "Nome do plano obrigatório.");
            if (row.Name?.Length > 150) errors.Add("Nome excede 150 caracteres.");
        }
        if (type == MigrationEntityType.Students)
        {
            if (row.Email is not null && !new EmailAddressAttribute().IsValid(row.Email)) errors.Add("E-mail inválido.");
            if (row.Email?.Length > 200) errors.Add("E-mail excede 200 caracteres.");
            if (row.Phone?.Length > 20) errors.Add("Telefone excede 20 caracteres.");
            if (row.BirthDate is not null && !DateOnly.TryParseExact(row.BirthDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("Nascimento inválido.");
        }
        if (type == MigrationEntityType.Plans || type == MigrationEntityType.Enrollments)
        {
            if (!TryParseMoney(row.Price, out var price) || price <= 0 || decimal.Round(price, 2) != price)
                errors.Add("Preço/valor contratado deve ser positivo, com até duas casas decimais.");
            else if (price > 99_999_999.99m)
                errors.Add("Preço/valor contratado excede o limite de R$ 99.999.999,99 do banco (numeric(10,2)).");
            if (!int.TryParse(row.DurationMonths, out var duration) || duration <= 0) errors.Add("Duração em meses inválida.");
            if (!TryBillingCycle(row.BillingCycle, out _)) errors.Add("Ciclo de cobrança inválido; use mensal, trimestral, semestral ou anual.");
        }
        if (type == MigrationEntityType.Enrollments)
        {
            if (string.IsNullOrWhiteSpace(row.Name)) errors.Add("Nome do plano no snapshot contratual obrigatório.");
            if (row.Name?.Length > 150) errors.Add("Nome do plano no snapshot excede 150 caracteres.");
            if (string.IsNullOrWhiteSpace(row.StudentExternalId)) errors.Add("ID externo do aluno obrigatório.");
            if (string.IsNullOrWhiteSpace(row.PlanExternalId)) errors.Add("ID externo do plano obrigatório.");
            if (!DateOnly.TryParseExact(row.StartDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) errors.Add("Data de início inválida.");
            if (!DateOnly.TryParseExact(row.EndDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end)) errors.Add("Data final inválida.");
            else if (DateOnly.TryParseExact(row.StartDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out start) && end <= start)
                errors.Add("A data final deve ser posterior à inicial.");
            if (!TryChargeState(row.ChargeState, out _)) errors.Add("Situação da cobrança inválida; use Paid, Pending, Unverified ou deixe em branco.");
            if (row.ChargeDueDate is not null && !DateOnly.TryParseExact(row.ChargeDueDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                errors.Add("Data de vencimento inválida.");
        }
        if (type == MigrationEntityType.Charges)
        {
            if (string.IsNullOrWhiteSpace(row.StudentExternalId)) errors.Add("ID externo do aluno obrigatório.");
            if (row.EnrollmentExternalId is null && row.ChargeExternalId is null)
                errors.Add("Informe o ID externo da matrícula ou da cobrança.");
            if (row.ChargeState is null || !TryChargeState(row.ChargeState, out _))
                errors.Add("Situação financeira atual obrigatória; use Paid, Pending ou Unverified.");
        }
        return errors;
    }

    private async Task<List<string>> GetReleaseBlockersAsync(MigrationSession session, List<MigrationBatch> batches, CancellationToken token)
    {
        var blockers = new List<string>();
        if (session.Status == MigrationSessionStatus.Released) return blockers;
        if (batches.Count == 0) blockers.Add("Adicione pelo menos um arquivo.");
        if (batches.Count > 0 && batches.All(batch => batch.Status == MigrationBatchStatus.Discarded))
            blockers.Add("Confirme ao menos um lote antes da liberação operacional.");
        if (batches.Any(batch => batch.Status is not (MigrationBatchStatus.Confirmed or MigrationBatchStatus.Discarded)))
            blockers.Add("Confirme ou descarte os lotes ainda pendentes.");
        var staged = new Dictionary<(MigrationEntityType, string), string>();
        foreach (var batch in batches)
        {
            if (batch.Status == MigrationBatchStatus.Discarded) continue;
            var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            var preview = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(item => item.RowNumber);
            foreach (var row in rows)
            {
                if (!decisions.TryGetValue(row.RowNumber, out var decision))
                {
                    if (batch.Status == MigrationBatchStatus.Confirmed)
                        blockers.Add($"Lote {batch.Id}, linha {row.RowNumber}: a revisão mudou ou não possui decisão. Reabra e confirme novamente.");
                    continue;
                }
                if (decision.Action == "Ignore") continue;
                if (!preview.TryGetValue(row.RowNumber, out var previewRow) || !CanProceed(previewRow, decision))
                {
                    blockers.Add($"Lote {batch.Id}, linha {row.RowNumber}: a decisão precisa de revisão antes da liberação.");
                    continue;
                }
                if (!staged.TryAdd((batch.EntityType, row.ExternalId ?? ""), decision.Action))
                    blockers.Add($"Há identificadores externos repetidos para {batch.EntityType}; mantenha uma única linha.");
            }
        }
        var refs = await db.MigrationExternalReferences.AsNoTracking().Where(reference =>
                reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace)
            .Select(reference => new { reference.EntityType, reference.ExternalId }).ToListAsync(token);
        var linked = refs.Select(reference => (reference.EntityType, reference.ExternalId)).ToHashSet();
        var studentIdentities = batches.Any(batch => batch.EntityType == MigrationEntityType.Enrollments && batch.Status != MigrationBatchStatus.Discarded)
            ? await ResolveStudentIdentitiesAsync(session, token) : new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var batch in batches.Where(batch => batch.Status != MigrationBatchStatus.Discarded && batch.EntityType == MigrationEntityType.Enrollments))
        {
            var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            foreach (var row in rows.Where(row => decisions.TryGetValue(row.RowNumber, out var decision) && decision.Action != "Ignore"))
            {
                if (!staged.ContainsKey((MigrationEntityType.Students, row.StudentExternalId ?? "")) && !linked.Contains((MigrationEntityType.Students, row.StudentExternalId ?? "")))
                    blockers.Add($"Matrícula na linha {row.RowNumber}: aluno externo não foi importado ou vinculado.");
                if (row.StudentExternalId is not null && studentIdentities.TryGetValue(row.StudentExternalId, out var identity) &&
                    row.ResolvedStudentKey != identity)
                    blockers.Add($"Matrícula na linha {row.RowNumber} (lote {batch.Id}): o aluno canônico mudou desde a revisão. Reabra e confirme a matrícula novamente.");
                if (!staged.ContainsKey((MigrationEntityType.Plans, row.PlanExternalId ?? "")) && !linked.Contains((MigrationEntityType.Plans, row.PlanExternalId ?? "")))
                    blockers.Add($"Matrícula na linha {row.RowNumber}: plano externo não foi importado ou vinculado.");
            }
        }
        foreach (var batch in batches.Where(batch => batch.Status != MigrationBatchStatus.Discarded && batch.EntityType == MigrationEntityType.Charges))
        {
            var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            foreach (var row in rows.Where(row => decisions.TryGetValue(row.RowNumber, out var decision) && decision.Action != "Ignore"))
            {
                if (!staged.ContainsKey((MigrationEntityType.Students, row.StudentExternalId ?? "")) && !linked.Contains((MigrationEntityType.Students, row.StudentExternalId ?? "")))
                    blockers.Add($"Situação financeira na linha {row.RowNumber}: aluno externo não foi importado ou vinculado.");
                if (row.EnrollmentExternalId is not null && !staged.ContainsKey((MigrationEntityType.Enrollments, row.EnrollmentExternalId)) &&
                    !linked.Contains((MigrationEntityType.Enrollments, row.EnrollmentExternalId)))
                    blockers.Add($"Situação financeira na linha {row.RowNumber}: matrícula externa não foi importada ou vinculada.");
                if (row.ChargeExternalId is not null && !linked.Contains((MigrationEntityType.Charges, row.ChargeExternalId)))
                    blockers.Add($"Situação financeira na linha {row.RowNumber}: cobrança externa não foi vinculada.");
            }
        }
        blockers.AddRange(await GetPlanNameReleaseBlockersAsync(session, batches, token));
        foreach (var batch in batches.Where(batch => batch.EntityType == MigrationEntityType.Students && batch.Status == MigrationBatchStatus.Confirmed))
        {
            var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var duplicates = rows.Where(row => row.ExternalId is not null).GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            var refreshed = await BuildPreviewAsync(session, batch.EntityType, rows, duplicates,
                new Dictionary<int, IReadOnlyList<string>>(), token, batch.Id);
            var prior = Deserialize<List<MigrationPreviewRow>>(batch.PreviewJson).ToDictionary(row => row.RowNumber);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(row => row.RowNumber);
            foreach (var row in refreshed.Where(row => decisions.TryGetValue(row.RowNumber, out var decision) && decision.Action != "Ignore"))
                if (!prior.TryGetValue(row.RowNumber, out var reviewed) || !JsonEquals(Serialize(row), Serialize(reviewed)))
                    blockers.Add($"Aluno na linha {row.RowNumber} (lote {batch.Id}): a prévia de identidade mudou desde a revisão. Reabra o lote e confirme a decisão atualizada.");
        }
        blockers.AddRange(await GetFinancialTargetBlockersAsync(session, batches, token));
        return blockers.Distinct().ToList();
    }

    private async Task<List<string>> GetPlanNameReleaseBlockersAsync(MigrationSession session,
        IReadOnlyCollection<MigrationBatch> batches, CancellationToken token)
    {
        var imports = new List<(Guid BatchId, int RowNumber, string Name)>();
        foreach (var batch in batches.Where(item => item.EntityType == MigrationEntityType.Plans &&
                     item.Status == MigrationBatchStatus.Confirmed))
        {
            var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            imports.AddRange(rows.Where(row => decisions.TryGetValue(row.RowNumber, out var decision) &&
                    decision.Action == "ImportNew" && !string.IsNullOrWhiteSpace(row.Name))
                .Select(row => (batch.Id, row.RowNumber, row.Name!)));
        }

        var blockers = new List<string>();
        var knownAliases = await db.MigrationExternalReferences.AsNoTracking().Where(reference =>
                reference.GymId == session.GymId && reference.SourceNamespace == session.SourceNamespace &&
                reference.EntityType == MigrationEntityType.Plans)
            .Select(reference => reference.ExternalId).ToListAsync(token);
        var knownAliasSet = knownAliases.ToHashSet(StringComparer.Ordinal);
        var recordsByBatch = batches.ToDictionary(batch => batch.Id,
            batch => Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson).ToDictionary(row => row.RowNumber));
        imports.RemoveAll(item => recordsByBatch[item.BatchId].TryGetValue(item.RowNumber, out var row) &&
            row.ExternalId is not null && knownAliasSet.Contains(row.ExternalId));
        var existing = await GetExistingPlansMatchingNamesAsync(session.GymId,
            imports.Select(item => item.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), token);
        foreach (var item in imports.Where(item => existing.Any(plan =>
                     string.Equals(plan.Name, item.Name, StringComparison.OrdinalIgnoreCase))))
            blockers.Add($"Plano na linha {item.RowNumber} (lote {item.BatchId}): outro plano com nome equivalente já existe nesta academia. Revise o lote antes da liberação.");
        foreach (var duplicate in imports.GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            blockers.Add($"Conflito de nome de plano entre registros confirmados da sessão ({string.Join(", ", duplicate.Select(item => $"linha {item.RowNumber}, lote {item.BatchId}"))}). A liberação foi bloqueada.");
        return blockers;
    }

    private async Task<List<PlanCandidate>> GetExistingPlansMatchingNamesAsync(Guid gymId, string[] names,
        CancellationToken token)
    {
        if (names.Length == 0) return [];
        var query = db.Plans.AsNoTracking().Where(plan => plan.GymId == gymId);
        if (db.Database.IsNpgsql())
            query = query.Where(plan => names.Contains(plan.Name)); // Uses PostgreSQL citext equality from the mapped column.
        return await query.Select(plan => new PlanCandidate(plan.Id, plan.Name, plan.Price,
            plan.DurationMonths, plan.IsActive)).ToListAsync(token);
    }

    private async Task<List<MigrationPreviewRow>> RefreshPlanPreviewAsync(MigrationSession session,
        MigrationBatch batch, CancellationToken token)
    {
        var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
        var duplicateKeys = rows.Where(row => row.ExternalId is not null)
            .GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var preview = await BuildPreviewAsync(session, MigrationEntityType.Plans, rows, duplicateKeys,
            new Dictionary<int, IReadOnlyList<string>>(), token, batch.Id);
        var json = Serialize(preview);
        if (!JsonEquals(json, batch.PreviewJson))
        {
            batch.PreviewJson = json;
            batch.DecisionsJson = "[]";
            await db.SaveChangesAsync(token);
        }
        return preview;
    }

    private async Task<List<MigrationPreviewRow>> RefreshReviewPreviewAsync(MigrationSession session, MigrationBatch batch,
        CancellationToken token, IReadOnlySet<int>? ignoredRows = null)
    {
        if (batch.EntityType == MigrationEntityType.Students) return await RefreshStudentPreviewAsync(session, batch, token);
        if (batch.EntityType == MigrationEntityType.Plans) return await RefreshPlanPreviewAsync(session, batch, token);
        var records = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
        List<NormalizedMigrationRow> materialized;
        IReadOnlyDictionary<int, IReadOnlyList<string>> errors;
        if (batch.EntityType == MigrationEntityType.Charges)
        {
            ignoredRows ??= Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson)
                .Where(decision => decision.Action == "Ignore").Select(decision => decision.RowNumber).ToHashSet();
            var refreshed = await MaterializeChargeRowsAsync(session, records, token, ignoredRows: ignoredRows, excludedBatchId: batch.Id);
            materialized = refreshed.Rows;
            errors = refreshed.Errors;
        }
        else
        {
            var refreshed = await MaterializeEnrollmentSnapshotsAsync(session, records, token);
            materialized = refreshed.Rows;
            errors = refreshed.Errors;
        }
        var duplicates = records.Where(row => row.ExternalId is not null).GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var preview = await BuildPreviewAsync(session, batch.EntityType, materialized, duplicates, errors, token, batch.Id);
        var payloadJson = Serialize(materialized);
        var previewJson = Serialize(preview);
        if (!JsonEquals(payloadJson, batch.PayloadJson) || !JsonEquals(previewJson, batch.PreviewJson))
        {
            batch.PayloadJson = payloadJson;
            batch.PreviewJson = previewJson;
            batch.DecisionsJson = "[]";
            await db.SaveChangesAsync(token);
        }
        return preview;
    }

    private async Task<List<MigrationPreviewRow>> RefreshStudentPreviewAsync(MigrationSession session,
        MigrationBatch batch, CancellationToken token)
    {
        var rows = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
        var duplicates = rows.Where(row => row.ExternalId is not null).GroupBy(row => row.ExternalId!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var preview = await BuildPreviewAsync(session, MigrationEntityType.Students, rows, duplicates,
            new Dictionary<int, IReadOnlyList<string>>(), token, batch.Id);
        var json = Serialize(preview);
        if (!JsonEquals(json, batch.PreviewJson))
        {
            batch.PreviewJson = json;
            batch.DecisionsJson = "[]";
            await db.SaveChangesAsync(token);
        }
        return preview;
    }

    private List<MigrationRowDecision> StampDecisions(List<MigrationRowDecision> decisions, Guid? actorId) =>
        decisions.Select(decision => decision with { ReviewedByUserId = actorId, ReviewedAt = Now() }).ToList();

    private async Task ValidateDecisionTargetsAsync(Guid gymId, Guid sessionId, MigrationBatch batch,
        List<MigrationRowDecision> decisions, CancellationToken token)
    {
        var session = await db.MigrationSessions.AsNoTracking().SingleAsync(item => item.Id == sessionId && item.GymId == gymId, token);
        var known = await db.MigrationExternalReferences.AsNoTracking().Where(reference => reference.GymId == gymId &&
                reference.SourceNamespace == session.SourceNamespace && reference.EntityType == batch.EntityType)
            .ToDictionaryAsync(reference => reference.ExternalId, reference => reference.TargetEntityId, token);
        var records = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson).ToDictionary(row => row.RowNumber);
        foreach (var decision in decisions.Where(item => item.Action != "Ignore"))
        {
            var row = records[decision.RowNumber];
            if (row.ExternalId is not null && known.TryGetValue(row.ExternalId, out var priorTarget))
            {
                if (decision.Action == "LinkExisting" && decision.TargetId != priorTarget)
                    throw new ArgumentException($"O alias '{row.ExternalId}' já está vinculado a outro destino. O vínculo não pode ser redirecionado por uma migração.");
                ValidateLinkedTarget(batch.EntityType, priorTarget, gymId);
            }
            if (decision.Action == "LinkExisting" && decision.TargetId is { } targetId)
                ValidateLinkedTarget(batch.EntityType, targetId, gymId);
        }
    }

    private async Task<HashSet<string>> GetOtherStagedPlanNamesAsync(MigrationSession session,
        Guid? excludedBatchId, CancellationToken token)
    {
        var batches = await db.MigrationBatches.AsNoTracking().Where(batch =>
                batch.GymId == session.GymId && batch.MigrationSessionId == session.Id &&
                batch.EntityType == MigrationEntityType.Plans &&
                (batch.Status == MigrationBatchStatus.Previewed || batch.Status == MigrationBatchStatus.Confirmed) &&
                (excludedBatchId == null || batch.Id != excludedBatchId))
            .ToListAsync(token);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in batches)
        {
            var ignored = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson)
                .Where(decision => decision.Action == "Ignore").Select(decision => decision.RowNumber).ToHashSet();
            foreach (var row in Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson))
                if (!ignored.Contains(row.RowNumber) && !string.IsNullOrWhiteSpace(row.Name)) names.Add(row.Name);
        }
        return names;
    }

    private async Task<List<string>> GetFinancialTargetBlockersAsync(MigrationSession session,
        List<MigrationBatch> batches, CancellationToken token)
    {
        var blockers = new List<string>();
        var targets = new List<(string Key, int RowNumber, Guid BatchId)>();
        foreach (var batch in batches.Where(batch => batch.GymId == session.GymId && batch.MigrationSessionId == session.Id &&
                     batch.EntityType == MigrationEntityType.Charges && batch.Status == MigrationBatchStatus.Confirmed))
        {
            var records = Deserialize<List<NormalizedMigrationRow>>(batch.PayloadJson);
            var decisions = Deserialize<List<MigrationRowDecision>>(batch.DecisionsJson).ToDictionary(item => item.RowNumber);
            var materialized = await MaterializeChargeRowsAsync(session, records, token, checkConfirmedFinancialBatches: false,
                ignoredRows: decisions.Values.Where(decision => decision.Action == "Ignore")
                    .Select(decision => decision.RowNumber).ToHashSet());
            var refreshedByRow = materialized.Rows.ToDictionary(row => row.RowNumber);
            foreach (var row in records)
            {
                if (!decisions.TryGetValue(row.RowNumber, out var decision) || decision.Action == "Ignore") continue;
                var refreshed = refreshedByRow[row.RowNumber];
                if (materialized.Errors.TryGetValue(row.RowNumber, out var errors))
                    blockers.AddRange(errors.Where(error => !error.StartsWith("Conflito de situação financeira:", StringComparison.Ordinal))
                        .Select(error => $"Situação financeira na linha {row.RowNumber} (lote {batch.Id}): {error}"));
                if (row.ResolvedStudentKey != refreshed.ResolvedStudentKey || row.ResolvedFinancialTargetKey != refreshed.ResolvedFinancialTargetKey ||
                    row.EnrollmentChargeState != refreshed.EnrollmentChargeState)
                    blockers.Add($"Situação financeira na linha {row.RowNumber} (lote {batch.Id}): o vínculo ou estado financeiro mudou desde a revisão; gere uma nova prévia antes de liberar.");
                if (!materialized.TargetKeys.TryGetValue(row.RowNumber, out var targetKey))
                {
                    blockers.Add($"Situação financeira na linha {row.RowNumber}: não foi possível resolver novamente a cobrança dentro desta academia e sessão.");
                    continue;
                }
                targets.Add((targetKey, row.RowNumber, batch.Id));
            }
        }
        foreach (var duplicate in targets.GroupBy(item => item.Key, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            var rows = duplicate.Select(item => $"{item.RowNumber} (lote {item.BatchId})");
            blockers.Add($"Conflito financeiro: várias linhas confirmadas apontam para a mesma matrícula/cobrança ({string.Join(", ", rows)}). A liberação foi bloqueada; mantenha uma única instrução por cobrança.");
        }
        return blockers;
    }

    private async Task<Guid> CreateTargetAsync(Guid gymId, MigrationEntityType type, NormalizedMigrationRow row, MigrationRowDecision decision,
        Dictionary<(MigrationEntityType, string), Guid> refs, DateOnly today, CancellationToken token)
    {
        switch (type)
        {
            case MigrationEntityType.Students:
                var student = new Student
                {
                    Id = Guid.NewGuid(), GymId = gymId, Name = row.Name!, Email = row.Email, Phone = row.Phone,
                    BirthDate = row.BirthDate is null ? null : ParseDate(row.BirthDate), IsActive = true,
                    AppAccessEnabled = false, UserId = null, NoValidEnrollmentSince = today, CreatedAt = Now()
                };
                db.Students.Add(student);
                await db.SaveChangesAsync(token);
                return student.Id;
            case MigrationEntityType.Plans:
                var plan = new Plan
                {
                    Id = Guid.NewGuid(), GymId = gymId, Name = row.Name!,
                    Price = ParseMoney(row.Price!),
                    DurationMonths = int.Parse(row.DurationMonths!, CultureInfo.InvariantCulture),
                    BillingCycle = ParseBillingCycle(row.BillingCycle), IsActive = true, CreatedAt = Now()
                };
                db.Plans.Add(plan);
                await db.SaveChangesAsync(token);
                return plan.Id;
            case MigrationEntityType.Enrollments:
                if (!refs.TryGetValue((MigrationEntityType.Students, row.StudentExternalId!), out var studentId) ||
                    !refs.TryGetValue((MigrationEntityType.Plans, row.PlanExternalId!), out var planId))
                    throw new InvalidOperationException("Uma matrícula aponta para aluno ou plano não resolvido.");
                var studentRecord = await db.Students.SingleOrDefaultAsync(item => item.Id == studentId && item.GymId == gymId, token)
                    ?? throw new InvalidOperationException("O aluno relacionado não pertence à academia desta sessão.");
                if (studentRecord.ArchivedAt is not null) throw new InvalidOperationException("Não é possível liberar matrícula de aluno arquivado.");
                var planRecord = await db.Plans.SingleOrDefaultAsync(item => item.Id == planId && item.GymId == gymId, token)
                    ?? throw new InvalidOperationException("O plano relacionado não pertence à academia desta sessão.");
                var start = ParseDate(row.StartDate!);
                var end = ParseDate(row.EndDate!);
                var status = TryChargeState(row.ChargeState, out var chargeState) && chargeState == ImportedChargeState.Paid
                    ? EnrollmentStatus.Active : EnrollmentStatus.PendingPayment;
                if (end <= today) status = EnrollmentStatus.Expired;
                var overlap = await db.Enrollments.AnyAsync(item => item.StudentId == studentId &&
                    (item.Status == EnrollmentStatus.Active || item.Status == EnrollmentStatus.PendingPayment) &&
                    start < item.EndDate && end > item.StartDate, token);
                if (overlap) throw new InvalidOperationException("Uma matrícula importada conflita com outra vigente. Revise a linha antes de liberar.");
                if (await db.Enrollments.AnyAsync(item => item.StudentId == studentId && item.PlanId == planId &&
                        item.StartDate == start && item.EndDate == end, token))
                    throw new InvalidOperationException("Já existe uma matrícula deste aluno canônico, plano e período, inclusive histórica ou cancelada. Revise o identificador externo; uma nova cobrança não será criada.");
                var contractPrice = ParseMoney(row.Price!);
                var enrollment = new Enrollment
                {
                    Id = Guid.NewGuid(), StudentId = studentId, PlanId = planId, StartDate = start, EndDate = end,
                    Status = status, PlanName = row.Name!, PlanPrice = contractPrice,
                    PlanDurationMonths = int.Parse(row.DurationMonths!, CultureInfo.InvariantCulture),
                    PlanBillingCycle = ParseBillingCycle(row.BillingCycle), CreatedAt = Now()
                };
                var dueDate = row.ChargeDueDate is null ? start : ParseDate(row.ChargeDueDate);
                var isPaid = TryChargeState(row.ChargeState, out chargeState) && chargeState == ImportedChargeState.Paid;
                var charge = new Charge
                {
                    Id = Guid.NewGuid(), EnrollmentId = enrollment.Id, Amount = contractPrice,
                    DiscountAmount = 0, DueDate = dueDate, Status = isPaid ? ChargeStatus.Paid : ChargeStatus.Pending,
                    Origin = ChargeOrigin.Migration, PaymentOrigin = isPaid ? PaymentOrigin.Migration : PaymentOrigin.None,
                    VerificationStatus = chargeState == ImportedChargeState.Unknown
                        ? ChargeVerificationStatus.Unverified : ChargeVerificationStatus.Verified,
                    CreatedAt = Now()
                };
                enrollment.Charge = charge;
                db.Enrollments.Add(enrollment);
                await db.SaveChangesAsync(token);
                _ = planRecord;
                return enrollment.Id;
            case MigrationEntityType.Charges:
                if (!refs.TryGetValue((MigrationEntityType.Students, row.StudentExternalId!), out var chargeStudentId))
                    throw new InvalidOperationException("O aluno da situação financeira não foi resolvido.");
                Enrollment linkedEnrollment;
                if (row.EnrollmentExternalId is { } enrollmentExternalId)
                {
                    if (!refs.TryGetValue((MigrationEntityType.Enrollments, enrollmentExternalId), out var linkedEnrollmentId))
                        throw new InvalidOperationException("A matrícula externa da situação financeira não foi resolvida.");
                    linkedEnrollment = await db.Enrollments.Include(item => item.Charge)
                        .SingleOrDefaultAsync(item => item.Id == linkedEnrollmentId && item.StudentId == chargeStudentId &&
                            item.Student.GymId == gymId && item.Plan.GymId == gymId, token)
                        ?? throw new InvalidOperationException("A matrícula não pertence ao aluno ou à academia desta sessão.");
                }
                else
                {
                    if (!refs.TryGetValue((MigrationEntityType.Charges, row.ChargeExternalId!), out var linkedChargeId))
                        throw new InvalidOperationException("A cobrança externa da situação financeira não foi resolvida.");
                    var linkedCharge = await db.Charges.Include(item => item.Enrollment)
                        .SingleOrDefaultAsync(item => item.Id == linkedChargeId && item.Enrollment.StudentId == chargeStudentId &&
                            item.Enrollment.Student.GymId == gymId && item.Enrollment.Plan.GymId == gymId, token)
                        ?? throw new InvalidOperationException("A cobrança não pertence ao aluno ou à academia desta sessão.");
                    linkedEnrollment = linkedCharge.Enrollment;
                    if (linkedEnrollment.Charge is null) linkedEnrollment.Charge = linkedCharge;
                }
                if (row.EnrollmentExternalId is not null && row.ChargeExternalId is not null &&
                    (!refs.TryGetValue((MigrationEntityType.Charges, row.ChargeExternalId), out var sameChargeId) ||
                     linkedEnrollment.Charge?.Id != sameChargeId))
                    throw new InvalidOperationException("Os identificadores de matrícula e cobrança apontam para registros diferentes.");
                var chargeToUpdate = linkedEnrollment.Charge
                    ?? throw new InvalidOperationException("A matrícula relacionada ainda não possui cobrança.");
                if (!TryChargeState(row.ChargeState, out var importedState))
                    throw new InvalidOperationException("A situação financeira atual é inválida.");
                if (decision.FinancialStateResolution != "UseEnrollmentState")
                {
                    var blockReason = GetFinancialMutationBlockReason(linkedEnrollment.Status, chargeToUpdate.Origin,
                        chargeToUpdate.PaymentOrigin, chargeToUpdate.PaidAmount, chargeToUpdate.PaidAt,
                        chargeToUpdate.PaymentMethod, chargeToUpdate.DiscountAmount);
                    if (blockReason is not null) throw new InvalidOperationException(blockReason);
                    var isImportedPaid = importedState == ImportedChargeState.Paid;
                    chargeToUpdate.Status = isImportedPaid ? ChargeStatus.Paid : ChargeStatus.Pending;
                    chargeToUpdate.PaymentOrigin = isImportedPaid ? PaymentOrigin.Migration : PaymentOrigin.None;
                    chargeToUpdate.Origin = ChargeOrigin.Migration;
                    chargeToUpdate.VerificationStatus = importedState == ImportedChargeState.Unknown
                        ? ChargeVerificationStatus.Unverified : ChargeVerificationStatus.Verified;
                    chargeToUpdate.PaidAmount = null;
                    chargeToUpdate.PaidAt = null;
                    chargeToUpdate.UpdatedAt = Now();
                    linkedEnrollment.Status = linkedEnrollment.EndDate <= today ? EnrollmentStatus.Expired
                        : isImportedPaid ? EnrollmentStatus.Active : EnrollmentStatus.PendingPayment;
                    linkedEnrollment.UpdatedAt = Now();
                    await db.SaveChangesAsync(token);
                }
                return chargeToUpdate.Id;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private async Task ValidateResolvedOwnershipAsync(Guid gymId, MigrationEntityType type, NormalizedMigrationRow row,
        Guid targetId, Dictionary<(MigrationEntityType, string), Guid> references, CancellationToken token)
    {
        if (type is not (MigrationEntityType.Enrollments or MigrationEntityType.Charges)) return;
        if (row.StudentExternalId is null || !references.TryGetValue((MigrationEntityType.Students, row.StudentExternalId), out var studentId))
            throw new InvalidOperationException($"Linha {row.RowNumber}: o aluno externo do vínculo não foi resolvido nesta academia e origem.");
        var enrollment = type == MigrationEntityType.Enrollments
            ? await db.Enrollments.AsNoTracking().Where(item => item.Id == targetId && item.Student.GymId == gymId && item.Plan.GymId == gymId)
                .Select(item => new { item.Id, item.StudentId, item.PlanId, ChargeId = item.Charge == null ? (Guid?)null : item.Charge.Id }).SingleOrDefaultAsync(token)
            : await db.Charges.AsNoTracking().Where(item => item.Id == targetId && item.Enrollment.Student.GymId == gymId && item.Enrollment.Plan.GymId == gymId)
                .Select(item => new { Id = item.EnrollmentId, item.Enrollment.StudentId, item.Enrollment.PlanId, ChargeId = (Guid?)item.Id }).SingleOrDefaultAsync(token);
        if (enrollment is null || enrollment.StudentId != studentId)
            throw new InvalidOperationException($"Linha {row.RowNumber}: o aluno canônico não corresponde à matrícula/cobrança vinculada.");
        if (type == MigrationEntityType.Enrollments && (row.PlanExternalId is null ||
                !references.TryGetValue((MigrationEntityType.Plans, row.PlanExternalId), out var planId) || enrollment.PlanId != planId))
            throw new InvalidOperationException($"Linha {row.RowNumber}: o plano externo não corresponde à matrícula vinculada.");
        if (type == MigrationEntityType.Charges &&
            (row.EnrollmentExternalId is not null && (!references.TryGetValue((MigrationEntityType.Enrollments, row.EnrollmentExternalId), out var enrollmentId) || enrollment.Id != enrollmentId) ||
             row.ChargeExternalId is not null && (!references.TryGetValue((MigrationEntityType.Charges, row.ChargeExternalId), out var chargeId) || enrollment.ChargeId != chargeId)))
            throw new InvalidOperationException($"Linha {row.RowNumber}: os identificadores externos apontam para matrículas/cobranças diferentes.");
    }

    private void ValidateLinkedTarget(MigrationEntityType type, Guid targetId, Guid gymId)
    {
        var valid = type switch
        {
            MigrationEntityType.Students => db.Students.Any(student => student.Id == targetId && student.GymId == gymId),
            MigrationEntityType.Plans => db.Plans.Any(plan => plan.Id == targetId && plan.GymId == gymId),
            MigrationEntityType.Enrollments => db.Enrollments.Any(enrollment => enrollment.Id == targetId && enrollment.Student.GymId == gymId && enrollment.Plan.GymId == gymId),
            MigrationEntityType.Charges => db.Charges.Any(charge => charge.Id == targetId &&
                charge.Enrollment.Student.GymId == gymId && charge.Enrollment.Plan.GymId == gymId),
            _ => false
        };
        if (!valid) throw new InvalidOperationException("O vínculo selecionado não pertence à academia autenticada.");
    }

    private static void EnsureExpectedFingerprint(MigrationBatch batch, string? expectedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(expectedFingerprint) ||
            !string.Equals(expectedFingerprint, ReviewFingerprint(batch), StringComparison.Ordinal))
            throw new InvalidOperationException("A prévia mudou desde a revisão. Reabra o lote e revise os dados atualizados.");
    }

    private static string ReviewFingerprint(MigrationBatch batch) =>
        ReviewFingerprint(batch.Fingerprint, batch.PayloadJson, batch.PreviewJson);

    private static string ReviewFingerprint(string fingerprint, string payloadJson, string previewJson) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            fingerprint + "\n" + CanonicalJson(payloadJson) + "\n" + CanonicalJson(previewJson))));

    // PostgreSQL stores these columns as jsonb and returns normalized JSON text
    // (different whitespace and object-property ordering from System.Text.Json).
    // Revision tokens must represent the JSON value, not provider-specific text.
    private static bool JsonEquals(string left, string right) =>
        string.Equals(CanonicalJson(left), CanonicalJson(right), StringComparison.Ordinal);

    private static string CanonicalJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonicalJson(writer, document.RootElement);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalJson(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonicalJson(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var integer)) writer.WriteNumberValue(integer);
                else if (element.TryGetDecimal(out var number))
                    writer.WriteRawValue(number.ToString("G29", CultureInfo.InvariantCulture));
                else writer.WriteNumberValue(element.GetDouble());
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static List<MigrationRowDecision> ValidateDecisions(List<MigrationRowDecision> decisions,
        List<MigrationPreviewRow> rows, MigrationEntityType entityType)
    {
        if (decisions.Count > MaxRows)
            throw new ArgumentException($"A revisão aceita no máximo {MaxRows} decisões.");
        var map = decisions.ToDictionary(decision => decision.RowNumber);
        if (map.Count != decisions.Count) throw new ArgumentException("Há decisões repetidas para uma linha.");
        foreach (var row in rows)
        {
            if (!map.TryGetValue(row.RowNumber, out var decision)) throw new ArgumentException("Decida cada linha antes de confirmar o lote.");
            if (!row.IsValid)
            {
                if (decision.Action == "Ignore") continue;
                if (!CanProceed(row, decision)) throw new ArgumentException("Linhas com erro só podem ser ignoradas, exceto conflitos financeiros que exigem resolução explícita.");
                continue;
            }
            if (decision.FinancialStateResolution is not null)
                throw new ArgumentException("Só é possível escolher resolução financeira quando a prévia indicar conflito.");
            if (decision.Action == "Ignore") continue;
            if (row.ExistingTargetId is { } priorTarget && decision.Action == "LinkExisting" && decision.TargetId != priorTarget)
                throw new ArgumentException("Este identificador externo já está vinculado a outro destino e não pode ser redirecionado.");
            if (decision.Action == "ImportNew")
            {
                if (decision.TargetId is not null) throw new ArgumentException("A decisão de criar novo não aceita ID de destino.");
                if (entityType == MigrationEntityType.Students && row.ExistingTargetId is { } knownStudent &&
                    row.Candidates.Any(candidate => candidate.TargetId is { } candidateId && candidateId != knownStudent))
                    throw new ArgumentException("O alias conhecido possui evidências de outro aluno. Confirme explicitamente o vínculo original ou ignore a linha; a migração não escolherá outro destino.");
                if (entityType == MigrationEntityType.Plans && row.ExistingTargetId is null && row.Candidates.Any(candidate => candidate.TargetId is not null))
                    throw new ArgumentException("Já existe um plano com nome equivalente nesta academia. Vincule explicitamente o plano existente ou ignore a linha.");
                if (entityType == MigrationEntityType.Plans && row.Candidates.Any(candidate => candidate.SourceRowNumber is { } otherRow &&
                        map.TryGetValue(otherRow, out var otherDecision) && otherDecision.Action != "Ignore"))
                    throw new ArgumentException("Há outro plano com nome equivalente neste arquivo. Ignore os registros excedentes ou corrija o nome antes de confirmar.");
                continue;
            }
            if (decision.Action == "LinkExisting" && decision.TargetId is { } target &&
                (entityType == MigrationEntityType.Students || row.Candidates.Any(candidate => candidate.TargetId == target)))
                continue;
            throw new ArgumentException("Escolha criar novo, ignorar ou vincular a um candidato explícito.");
        }
        if (map.Keys.Except(rows.Select(row => row.RowNumber)).Any()) throw new ArgumentException("A decisão aponta para uma linha inexistente.");
        return rows.Select(row => map[row.RowNumber]).ToList();
    }

    private static bool CanProceed(MigrationPreviewRow row, MigrationRowDecision decision) =>
        row.IsValid || (row.RequiresFinancialResolution && row.Errors.Count > 0 &&
            row.Errors.All(error => error.StartsWith("Conflito de situação financeira:", StringComparison.Ordinal)) &&
            decision.Action == "ImportNew" && decision.TargetId is null &&
            decision.FinancialStateResolution is "UseEnrollmentState" or "UseFinancialFile");

    private static bool HasRelevantFinancialPreviewChange(IReadOnlyList<MigrationPreviewRow> prior,
        IReadOnlyList<MigrationPreviewRow> current, IReadOnlySet<int> ignoredRows)
    {
        var priorByRow = prior.ToDictionary(row => row.RowNumber);
        foreach (var row in current.Where(row => !ignoredRows.Contains(row.RowNumber)))
        {
            if (!priorByRow.TryGetValue(row.RowNumber, out var old)) return true;
            if (JsonEquals(Serialize(row), Serialize(old))) continue;
            // Ignoring an excess instruction can remove a duplicate-target error from
            // the retained row. It cannot authorize any change to its identity or state.
            var errors = old.Errors.Where(error => !error.Contains("aponta para a mesma matrícula/cobrança", StringComparison.OrdinalIgnoreCase)).ToList();
            if (errors.Count == old.Errors.Count ||
                !JsonEquals(Serialize(row), Serialize(old with { Errors = errors, IsValid = errors.Count == 0 }))) return true;
        }
        return false;
    }

    private async Task<MigrationSession> GetPreparingSessionAsync(Guid gymId, Guid sessionId, CancellationToken token) =>
        await db.MigrationSessions.SingleOrDefaultAsync(session => session.Id == sessionId && session.GymId == gymId, token)
        is { Status: MigrationSessionStatus.Preparing } session && session.ExpiresAt > Now()
            ? session : throw new KeyNotFoundException("Sessão de migração não encontrada, expirada ou já liberada.");

    private async Task<MigrationSession> LockPreparingSessionAsync(Guid gymId, Guid sessionId, CancellationToken token)
    {
        var now = Now();
        var locked = await db.MigrationSessions.Where(session => session.Id == sessionId && session.GymId == gymId &&
                session.Status == MigrationSessionStatus.Preparing && session.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.Status,
                MigrationSessionStatus.Preparing), token);
        if (locked != 1)
        {
            var current = await db.MigrationSessions.AsNoTracking().SingleOrDefaultAsync(
                session => session.Id == sessionId && session.GymId == gymId, token);
            if (current is null) throw new KeyNotFoundException("Sessão de migração não encontrada.");
            throw new InvalidOperationException("A sessão expirou ou já foi liberada; nenhuma alteração foi aplicada.");
        }
        return await db.MigrationSessions.SingleAsync(session => session.Id == sessionId && session.GymId == gymId, token);
    }

    private static NormalizedMigrationRow Normalize(MigrationRowInput row) => new(row.RowNumber,
        Empty(row.ExternalId), Empty(row.Name), Empty(row.Email)?.ToLowerInvariant(), Empty(row.Phone), Empty(row.BirthDate),
        Empty(row.Price), Empty(row.DurationMonths), Empty(row.BillingCycle), Empty(row.StudentExternalId),
        Empty(row.PlanExternalId), Empty(row.StartDate), Empty(row.EndDate), Empty(row.ChargeState)?.ToLowerInvariant(), Empty(row.ChargeDueDate),
        Empty(row.EnrollmentExternalId), Empty(row.ChargeExternalId));
    private static string Fingerprint(MigrationEntityType type, List<NormalizedMigrationRow> rows) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(type + "\n" + string.Join("\n", rows.OrderBy(row => row.RowNumber).Select(Serialize)))));
    private static bool TryBillingCycle(string? value, out PlanBillingCycle cycle)
    {
        cycle = value?.Trim().ToLowerInvariant() switch
        {
            "1" or "mensal" or "monthly" => PlanBillingCycle.Monthly,
            "3" or "trimestral" or "quarterly" => PlanBillingCycle.Quarterly,
            "6" or "semestral" or "semiannual" => PlanBillingCycle.Semiannual,
            "12" or "anual" or "annual" => PlanBillingCycle.Annual,
            _ => 0
        };
        return Enum.IsDefined(cycle);
    }
    private static PlanBillingCycle ParseBillingCycle(string? value) => TryBillingCycle(value, out var cycle) ? cycle : throw new ArgumentException("Ciclo inválido.");
    private static decimal ParseMoney(string value) => TryParseMoney(value, out var amount)
        ? amount : throw new ArgumentException("Valor monetário inválido.");
    private static bool TryParseMoney(string? value, out decimal amount)
    {
        amount = 0;
        var normalized = (value ?? string.Empty).Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        var comma = normalized.LastIndexOf(',');
        var dot = normalized.LastIndexOf('.');
        if (comma >= 0 && dot >= 0)
        {
            normalized = comma > dot
                ? normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (comma >= 0)
        {
            var decimals = normalized.Length - comma - 1;
            normalized = decimals is 1 or 2
                ? normalized.Replace(',', '.')
                : decimals == 3 ? normalized.Replace(",", string.Empty, StringComparison.Ordinal) : normalized;
        }
        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out amount);
    }
    private static bool TryChargeState(string? value, out ImportedChargeState state)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        state = normalized switch
        {
            "paid" or "pago" or "paga" or "quitado" or "quitada" => ImportedChargeState.Paid,
            "pending" or "pendente" or "unpaid" or "aberta" => ImportedChargeState.Pending,
            null or "" or "unknown" or "unverified" or "não verificada" or "nao verificada" or "desconhecida" => ImportedChargeState.Unknown,
            _ => ImportedChargeState.Unknown
        };
        return normalized is null or "" or "paid" or "pago" or "paga" or "quitado" or "quitada" or
            "pending" or "pendente" or "unpaid" or "aberta" or "unknown" or "unverified" or
            "não verificada" or "nao verificada" or "desconhecida";
    }
    private static DateOnly ParseDate(string value) => DateOnly.ParseExact(value, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
    private static string Digits(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());
    private static IReadOnlyDictionary<string, string?> DisplayValues(NormalizedMigrationRow row) => new Dictionary<string, string?>
    {
        ["externalId"] = row.ExternalId, ["name"] = row.Name, ["email"] = row.Email,
        ["phone"] = row.Phone, ["birthDate"] = row.BirthDate, ["price"] = row.Price,
        ["durationMonths"] = row.DurationMonths, ["billingCycle"] = row.BillingCycle,
        ["studentExternalId"] = row.StudentExternalId, ["planExternalId"] = row.PlanExternalId,
        ["enrollmentExternalId"] = row.EnrollmentExternalId, ["chargeExternalId"] = row.ChargeExternalId,
        ["startDate"] = row.StartDate, ["endDate"] = row.EndDate,
        ["chargeState"] = row.ChargeState, ["chargeDueDate"] = row.ChargeDueDate,
        ["enrollmentChargeState"] = row.EnrollmentChargeState,
        ["resolvedStudentKey"] = row.ResolvedStudentKey,
        ["resolvedFinancialTargetKey"] = row.ResolvedFinancialTargetKey
    };
    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, JsonOptions)!;

    private static MigrationBatchResultDocument DeserializeResultDocument(string value)
    {
        using var json = JsonDocument.Parse(value);
        if (json.RootElement.ValueKind == JsonValueKind.Array)
        {
            var legacyRows = Deserialize<List<MigrationBatchResult>>(value);
            return new(legacyRows, MigrationBatchResultTotalsCalculator.Calculate(legacyRows));
        }
        if (json.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("O resultado final persistido da migração possui formato inválido.");

        var document = Deserialize<MigrationBatchResultDocument>(value);
        if (document.Rows is null || document.Totals is null)
            throw new InvalidOperationException("O resultado final persistido da migração está incompleto.");
        var calculatedTotals = MigrationBatchResultTotalsCalculator.Calculate(document.Rows);
        if (calculatedTotals != document.Totals)
            throw new InvalidOperationException("Os totais persistidos não correspondem às linhas finais do lote.");
        return document;
    }

    private sealed record NormalizedMigrationRow(int RowNumber, string? ExternalId, string? Name, string? Email, string? Phone,
        string? BirthDate, string? Price, string? DurationMonths, string? BillingCycle, string? StudentExternalId,
        string? PlanExternalId, string? StartDate, string? EndDate, string? ChargeState, string? ChargeDueDate,
        string? EnrollmentExternalId = null, string? ChargeExternalId = null,
        string? EnrollmentChargeState = null, bool HasFinancialStateConflict = false,
        string? ResolvedStudentKey = null, string? ResolvedFinancialTargetKey = null);
    private sealed record StudentCandidate(Guid Id, string Name, string? Email, string? Phone, bool IsArchived,
        bool IsActive, bool AppAccessEnabled, Guid? UserId, bool? UserActive);
    private sealed record PlanCandidate(Guid Id, string Name, decimal Price, int DurationMonths, bool IsActive);
    private sealed record PlanTerms(string Name, string Price, string DurationMonths, string BillingCycle);
    private enum ImportedChargeState { Pending, Paid, Unknown }
}
