using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.DTOs.Common;


namespace GymFlow.Application.Services;

public class StudentService
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPasswordHasher _passwordHasher;

    public StudentService(
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> CreateAsync(
        Guid gymId,
        CreateStudentRequest request)
    {
        var normalizedEmail = request.Email
            .Trim()
            .ToLowerInvariant();

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
    int page,
    int pageSize)
    {
        if (page < 1)
            throw new ArgumentException(
                "A página deve ser maior ou igual a 1.");

        if (pageSize < 1 || pageSize > 100)
            throw new ArgumentException(
                "O tamanho da página deve estar entre 1 e 100.");

        var skip = (page - 1) * pageSize;

        var (students, totalCount) =
             await _studentRepository.GetPagedByGymIdAsync(
            gymId,
            search,
            isActive,
            skip,
            pageSize);

        var items = students
            .Select(student => new StudentResponse
            {
                Id = student.Id,
                Name = student.User.Name,
                Email = student.User.Email,
                Phone = student.Phone,
                BirthDate = student.BirthDate,
                IsActive = student.User.IsActive,
                CreatedAt = student.CreatedAt
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