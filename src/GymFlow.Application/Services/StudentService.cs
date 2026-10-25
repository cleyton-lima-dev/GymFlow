using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.Interfaces.Time;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;


namespace GymFlow.Application.Services;

public class StudentService
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IGymTimeZoneProvider _gymTimeZoneProvider;

    public StudentService(
    IUserRepository userRepository,
    IStudentRepository studentRepository,
    IPasswordHasher passwordHasher,
    IEnrollmentRepository enrollmentRepository,
    IGymTimeZoneProvider gymTimeZoneProvider)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _passwordHasher = passwordHasher;
        _enrollmentRepository = enrollmentRepository;
        _gymTimeZoneProvider = gymTimeZoneProvider;
    }

    public async Task<bool> CreateAsync(
        Guid gymId,
        CreateStudentRequest request)
    {
        if (!string.IsNullOrEmpty(request.Password))
            throw new ArgumentException("A senha do aplicativo deve ser definida pelo aluno no convite.");
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null : request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("O nome é obrigatório.");
        if (normalizedEmail is not null && !new EmailAddressAttribute().IsValid(normalizedEmail))
            throw new ArgumentException("O e-mail é inválido.");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Name,
            PersistenceTextPolicy.UserNameMaxLength,
            "O nome");

        PersistenceTextPolicy.ValidateMaxLength(
            normalizedEmail,
            PersistenceTextPolicy.EmailMaxLength,
            "O e-mail");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Phone,
            PersistenceTextPolicy.PhoneMaxLength,
            "O telefone");

        // The administrative record never creates a login or accepts a password.
        var timeZone = _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow,
            timeZone);

        var today = DateOnly.FromDateTime(localNow);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            IsActive = true,
            Phone = request.Phone?.Trim(),
            BirthDate = request.BirthDate,
            NoValidEnrollmentSince = today,
            CreatedAt = DateTime.UtcNow
        };

        await _studentRepository.AddAsync(student);

        return true;
    }

    public async Task<PagedResponse<StudentResponse>> GetAllAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    StudentEnrollmentFilter? enrollmentFilter,
    StudentArchiveFilter archiveFilter,
    int page,
    int pageSize,
    bool includeEnrollmentMetadata = true,
    StudentAssessmentFilter? assessmentFilter = null)
    {
        if (page < 1)
        {
            throw new ArgumentException(
                "A página deve ser maior ou igual a 1.");
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");
        }

        if (!includeEnrollmentMetadata && enrollmentFilter.HasValue)
            throw new ArgumentException("O filtro de matrícula exige acesso ao ERP.");

        var timeZone =
            _gymTimeZoneProvider.GetTimeZone(gymId);

        var localNow =
            TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                timeZone);

        var today =
            DateOnly.FromDateTime(localNow);

        var skip = (page - 1) * pageSize;

        var (students, totalCount) =
            await _studentRepository.GetPagedByGymIdAsync(
                gymId,
                search,
                isActive,
                enrollmentFilter,
                archiveFilter,
                today,
                skip,
                pageSize,
                assessmentFilter);

        var studentIds = students
            .Select(student => student.Id)
            .ToArray();

        var enrollments = includeEnrollmentMetadata
            ? await _enrollmentRepository.GetByStudentIdsAsync(
                studentIds,
                gymId)
            : new List<Enrollment>();

        var enrollmentByStudentId = enrollments
            .GroupBy(enrollment => enrollment.StudentId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var ordered = group
                        .OrderByDescending(
                            enrollment =>
                                enrollment.Status ==
                                EnrollmentStatus.Active &&
                                enrollment.StartDate <= today &&
                                enrollment.EndDate > today)
                        .ThenByDescending(
                            enrollment => enrollment.StartDate)
                        .ThenByDescending(
                            enrollment => enrollment.CreatedAt)
                        .ToList();

                    return ordered.First();
                });

        var items = students
            .Select(student =>
            {
                enrollmentByStudentId.TryGetValue(
                    student.Id,
                    out var enrollment);

                EnrollmentStatus? enrollmentStatus = null;

                if (enrollment is not null)
                {
                    enrollmentStatus =
                        enrollment.Status == EnrollmentStatus.Active &&
                        enrollment.EndDate <= today
                            ? EnrollmentStatus.Expired
                            : enrollment.Status;
                }

                return new StudentResponse
                {
                    Id = student.Id,
                    Name = student.Name,
                    Email = student.Email,
                    UserId = student.UserId,
                    AppAccessEnabled = student.AppAccessEnabled,
                    Phone = student.Phone,
                    BirthDate = student.BirthDate,
                    IsActive = student.IsActive,
                    EnrollmentStatus = enrollmentStatus,
                    PlanName = enrollment?.PlanName,
                    EnrollmentEndDate = enrollment?.EndDate,
                    ArchivedAt = student.ArchivedAt,
                    CreatedAt = student.CreatedAt
                };
            })
            .ToList();

        return new PagedResponse<StudentResponse>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<StudentResponse?> GetByIdAsync(
        Guid studentId,
        Guid gymId)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            return null;

        return new StudentResponse
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            UserId = student.UserId,
            AppAccessEnabled = student.AppAccessEnabled,
            Phone = student.Phone,
            BirthDate = student.BirthDate,
            IsActive = student.IsActive,
            ArchivedAt = student.ArchivedAt,
            CreatedAt = student.CreatedAt
        };
    }

    public async Task<UpdateStudentResult> UpdateAsync(
    Guid studentId,
    Guid gymId,
    UpdateStudentRequest request)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null)
            return UpdateStudentResult.NotFound;

        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null : request.Email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("O nome é obrigatório.");
        if (normalizedEmail is not null && !new EmailAddressAttribute().IsValid(normalizedEmail))
            throw new ArgumentException("O e-mail é inválido.");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Name,
            PersistenceTextPolicy.UserNameMaxLength,
            "O nome");

        PersistenceTextPolicy.ValidateMaxLength(
            normalizedEmail,
            PersistenceTextPolicy.EmailMaxLength,
            "O e-mail");

        PersistenceTextPolicy.ValidateMaxLength(
            request.Phone,
            PersistenceTextPolicy.PhoneMaxLength,
            "O telefone");

        var now = DateTime.UtcNow;
        var emailChanged = !string.Equals(
            NormalizeEmail(student.Email),
            normalizedEmail,
            StringComparison.Ordinal);
        if (student.User is not null)
        {
            if (normalizedEmail is null)
                throw new ArgumentException("Uma conta existente exige e-mail para login.");
            var existingUser = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingUser is not null && existingUser.Id != student.UserId)
                return UpdateStudentResult.EmailAlreadyInUse;
            student.User.Name = request.Name.Trim();
            student.User.Email = normalizedEmail;
            student.User.UpdatedAt = now;
        }
        student.Name = request.Name.Trim();
        student.Email = normalizedEmail;

        // A pending invitation is bound to the address that was current when
        // it was issued. Revoke it in the same save as an effective email
        // change, after validation has succeeded. Activated accounts keep
        // their existing access state; editing a profile never enables access.
        if (student.User is null && emailChanged &&
            student.AppInviteTokenHash is not null)
        {
            student.AppInviteTokenHash = null;
            student.AppInviteExpiresAt = null;
        }

        student.Phone = request.Phone?.Trim();
        student.BirthDate = request.BirthDate;

        student.UpdatedAt = now;

        await _studentRepository.UpdateAsync(student);

        return UpdateStudentResult.Success;
    }

    public async Task<bool> UpdateStatusAsync(
    Guid studentId,
    Guid gymId,
    bool isActive)
    {
        var student = await _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId);

        if (student is null ||
            student.ArchivedAt is not null)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        student.IsActive = isActive;

        if (isActive)
        {
            student.InactivationReason = null;
            student.InactivatedAt = null;
        }
        else
        {
            student.InactivationReason =
                StudentInactivationReason.Manual;

            student.InactivatedAt = now;
        }

        student.UpdatedAt = now;

        await _studentRepository.UpdateAsync(student);

        return true;
    }

    public async Task<StudentResponse?> GetMeAsync(
    Guid userId,
    Guid gymId)
    {
        var student = await _studentRepository
            .GetByUserIdAndGymIdAsync(userId, gymId);

        if (student is null)
            return null;

        return new StudentResponse
        {
            Id = student.Id,
            Name = student.Name,
            Email = student.Email,
            UserId = student.UserId,
            AppAccessEnabled = student.AppAccessEnabled,
            Phone = student.Phone,
            BirthDate = student.BirthDate,
            IsActive = student.IsActive,
            ArchivedAt = student.ArchivedAt,
            CreatedAt = student.CreatedAt
        };
    }

    public async Task<(bool Enabled, string? Token, DateTime? ExpiresAt)?> SetAppAccessAsync(
        Guid studentId, Guid gymId, bool enabled)
    {
        var student = await _studentRepository.GetByIdAndGymIdAsync(studentId, gymId);
        if (student is null || student.ArchivedAt is not null || !student.IsActive)
            return null;

        if (enabled && string.IsNullOrWhiteSpace(student.Email))
            throw new ArgumentException("Informe o e-mail do aluno antes de habilitar o aplicativo.");
        if (enabled && !new EmailAddressAttribute().IsValid(student.Email))
            throw new ArgumentException("O e-mail do aluno é inválido.");

        var now = DateTime.UtcNow;
        string? token = null;
        student.AppAccessEnabled = enabled;
        student.AppInviteTokenHash = null;
        student.AppInviteExpiresAt = null;
        if (enabled && student.UserId is null)
        {
            if (await _userRepository.GetByEmailAsync(student.Email!) is not null)
                throw new ArgumentException("Já existe uma conta com este e-mail.");
            token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            student.AppInviteTokenHash = HashToken(token);
            student.AppInviteExpiresAt = now.AddHours(24);
        }
        else if (enabled && student.User is not null)
        {
            // Only an explicit Admin operation may restore an existing digital account.
            student.User.IsActive = true;
            student.User.UpdatedAt = now;
        }
        student.UpdatedAt = now;
        await _studentRepository.UpdateAsync(student);
        return (enabled, token, student.AppInviteExpiresAt);
    }

    public async Task<bool> ActivateAppAsync(string token, string password)
    {
        PasswordPolicy.Validate(password);
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
            return false;
        var student = await _studentRepository.GetByInviteHashAsync(HashToken(token));
        if (student is null || student.UserId is not null ||
            !student.AppAccessEnabled || !student.IsActive ||
            student.ArchivedAt is not null ||
            student.AppInviteExpiresAt is null ||
            student.AppInviteExpiresAt.Value <= DateTime.UtcNow ||
            string.IsNullOrWhiteSpace(student.Email))
            return false;
        if (await _userRepository.GetByEmailAsync(student.Email) is not null)
            return false;

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(), GymId = student.GymId, Name = student.Name,
            Email = student.Email, PasswordHash = _passwordHasher.Hash(password),
            Role = UserRole.Student, IsActive = true, CreatedAt = now
        };
        student.UserId = user.Id;
        student.User = user;
        student.AppInviteTokenHash = null;
        student.AppInviteExpiresAt = null;
        student.UpdatedAt = now;
        await _studentRepository.AddUserAndSaveAsync(user, student);
        return true;
    }

    public async Task<string?> ValidateAppInviteAsync(string token)
    {
        const string unavailableMessage =
            "Este convite é inválido, já foi utilizado ou revogado. Solicite um novo convite ao Admin.";
        if (string.IsNullOrWhiteSpace(token) || token.Length != 64)
            return unavailableMessage;

        var student = await _studentRepository.GetByInviteHashAsync(HashToken(token));
        if (student is null || student.UserId is not null ||
            !student.AppAccessEnabled || !student.IsActive ||
            student.ArchivedAt is not null || string.IsNullOrWhiteSpace(student.Email))
            return unavailableMessage;

        if (student.AppInviteExpiresAt is null || student.AppInviteExpiresAt <= DateTime.UtcNow)
            return "Este convite expirou. Solicite um novo convite ao Admin.";

        return null;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
