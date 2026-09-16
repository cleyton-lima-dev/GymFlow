using GymFlow.Application.DTOs.Common;
using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Security;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.Interfaces.Time;


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
        PasswordPolicy.Validate(request.Password);

        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

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

        var existingUser =
            await _userRepository.GetByEmailAsync(normalizedEmail);

        if (existingUser is not null)
            return false;

        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Student,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Phone = request.Phone?.Trim(),
            BirthDate = request.BirthDate,
            CreatedAt = DateTime.UtcNow,
            User = user
        };

        await _studentRepository.AddAsync(user, student);

        return true;
    }

    public async Task<PagedResponse<StudentResponse>> GetAllAsync(
    Guid gymId,
    string? search,
    bool? isActive,
    StudentEnrollmentFilter? enrollmentFilter,
    int page,
    int pageSize)
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
                today,
                skip,
                pageSize);

        var studentIds = students
            .Select(student => student.Id)
            .ToArray();

        var enrollments =
            await _enrollmentRepository.GetByStudentIdsAsync(
                studentIds,
                gymId);

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
                    Name = student.User.Name,
                    Email = student.User.Email,
                    Phone = student.Phone,
                    BirthDate = student.BirthDate,
                    IsActive = student.User.IsActive,
                    EnrollmentStatus = enrollmentStatus,
                    PlanName = enrollment?.PlanName,
                    EnrollmentEndDate = enrollment?.EndDate,
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
            Name = student.User.Name,
            Email = student.User.Email,
            Phone = student.Phone,
            BirthDate = student.BirthDate,
            IsActive = student.User.IsActive,
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

        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

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

        var existingUser =
            await _userRepository.GetByEmailAsync(normalizedEmail);

        if (existingUser is not null &&
            existingUser.Id != student.UserId)
        {
            return UpdateStudentResult.EmailAlreadyInUse;
        }

        student.User.Name = request.Name.Trim();
        student.User.Email = normalizedEmail;

        student.Phone = request.Phone?.Trim();
        student.BirthDate = request.BirthDate;

        var now = DateTime.UtcNow;

        student.UpdatedAt = now;
        student.User.UpdatedAt = now;

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

        if (student is null)
            return false;

        student.User.IsActive = isActive;

        var now = DateTime.UtcNow;

        student.User.UpdatedAt = now;
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
            Name = student.User.Name,
            Email = student.User.Email,
            Phone = student.Phone,
            BirthDate = student.BirthDate,
            IsActive = student.User.IsActive,
            CreatedAt = student.CreatedAt
        };
    }
}