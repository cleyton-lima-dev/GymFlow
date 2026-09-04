using GymFlow.Application.DTOs.Auth;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using GymFlow.Application.Security;
using GymFlow.Application.Validation;

namespace GymFlow.Application.Services;

public class AuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;

    public AuthenticationService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        var user =
            await _userRepository.GetByEmailAsync(normalizedEmail);

        if (user is null || !user.IsActive)
            return null;

        var passwordIsValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordIsValid)
            return null;
        
        
        var token = _tokenService.GenerateToken(user);

        return new LoginResponse
        {
            UserId = user.Id,
            GymId = user.GymId,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            Token = token
        };
    }

    public async Task<IReadOnlyList<ProfessorResponse>> GetProfessorsAsync(
    Guid gymId)
    {
        var professors =
            await _userRepository.GetProfessorsByGymIdAsync(gymId);

        return professors
            .Select(user => new ProfessorResponse(
                user.Id,
                user.Name,
                user.Email,
                user.IsActive,
                user.CreatedAt))
            .ToList();
    }
    public async Task<bool> RegisterAsync(
    Guid gymId,
    RegisterRequest request)
    {
        PasswordPolicy.Validate(request.Password);

        var normalizedEmail =
            request.Email.Trim().ToLowerInvariant();

        PersistenceTextPolicy.ValidateMaxLength(
            request.Name,
         PersistenceTextPolicy.UserNameMaxLength,
            "O nome");

        PersistenceTextPolicy.ValidateMaxLength(
            normalizedEmail,
            PersistenceTextPolicy.EmailMaxLength,
            "O e-mail");

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
            PasswordHash =
                _passwordHasher.Hash(request.Password),

            Role = UserRole.Professor,

            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);

        return true;
    }
}