using GymFlow.Application.DTOs.Auth;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class AuthenticationServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly AuthenticationService _service;

    public AuthenticationServiceTests()
    {
        _userRepository = Substitute.For<IUserRepository>();
        _passwordHasher = Substitute.For<IPasswordHasher>();
        _tokenService = Substitute.For<ITokenService>();

        _service = new AuthenticationService(
            _userRepository,
            _passwordHasher,
            _tokenService);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ShouldReturnLoginResponse()
    {
        var user = CreateUser();

        _userRepository
            .GetByEmailAsync(user.Email)
            .Returns(user);

        _passwordHasher
            .Verify("ValidPassword123", user.PasswordHash)
            .Returns(true);

        _tokenService
            .GenerateToken(user)
            .Returns("generated-token");

        var request = new LoginRequest
        {
            Email = user.Email,
            Password = "ValidPassword123"
        };

        var result = await _service.LoginAsync(request);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.GymId, result.GymId);
        Assert.Equal(user.Name, result.Name);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(UserRole.Professor.ToString(), result.Role);
        Assert.Equal("generated-token", result.Token);

        _tokenService
            .Received(1)
            .GenerateToken(user);
    }

    [Fact]
    public async Task LoginAsync_WhenUserDoesNotExist_ShouldReturnNull()
    {
        _userRepository
            .GetByEmailAsync("missing@gymflow.dev")
            .Returns((User?)null);

        var request = new LoginRequest
        {
            Email = "missing@gymflow.dev",
            Password = "AnyPassword"
        };

        var result = await _service.LoginAsync(request);

        Assert.Null(result);

        _passwordHasher
            .DidNotReceive()
            .Verify(
                Arg.Any<string>(),
                Arg.Any<string>());

        _tokenService
            .DidNotReceive()
            .GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WhenUserIsInactive_ShouldReturnNull()
    {
        var user = CreateUser();
        user.IsActive = false;

        _userRepository
            .GetByEmailAsync(user.Email)
            .Returns(user);

        var request = new LoginRequest
        {
            Email = user.Email,
            Password = "ValidPassword123"
        };

        var result = await _service.LoginAsync(request);

        Assert.Null(result);

        _passwordHasher
            .DidNotReceive()
            .Verify(
                Arg.Any<string>(),
                Arg.Any<string>());

        _tokenService
            .DidNotReceive()
            .GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_WithInvalidPassword_ShouldReturnNull()
    {
        var user = CreateUser();

        _userRepository
            .GetByEmailAsync(user.Email)
            .Returns(user);

        _passwordHasher
            .Verify("WrongPassword", user.PasswordHash)
            .Returns(false);

        var request = new LoginRequest
        {
            Email = user.Email,
            Password = "WrongPassword"
        };

        var result = await _service.LoginAsync(request);

        Assert.Null(result);

        _tokenService
            .DidNotReceive()
            .GenerateToken(Arg.Any<User>());
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_ShouldCreateProfessorWithNormalizedData()
    {
        var gymId = Guid.NewGuid();

        var request = new RegisterRequest
        {
            Name = "  Professor Teste  ",
            Email = "  PROFESSOR@GYMFLOW.DEV  ",
            Password = "ValidPassword123"
        };

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash(request.Password)
            .Returns("hashed-password");

        var result = await _service.RegisterAsync(gymId, request);

        Assert.True(result);

        await _userRepository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.GymId == gymId &&
                    user.Name == "Professor Teste" &&
                    user.Email == "professor@gymflow.dev" &&
                    user.PasswordHash == "hashed-password" &&
                    user.Role == UserRole.Professor &&
                    user.IsActive));

        _passwordHasher
            .Received(1)
            .Hash(request.Password);
    }

    [Fact]
    public async Task RegisterAsync_WhenEmailAlreadyExists_ShouldReturnFalse()
    {
        var existingUser = CreateUser();

        var request = new RegisterRequest
        {
            Name = "Outro Professor",
            Email = "  PROFESSOR@GYMFLOW.DEV  ",
            Password = "ValidPassword123"
        };

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns(existingUser);

        var result = await _service.RegisterAsync(
            Guid.NewGuid(),
            request);

        Assert.False(result);

        _passwordHasher
            .DidNotReceive()
            .Hash(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task LoginAsync_ShouldNormalizeEmailBeforeLookup()
    {
        var user = CreateUser();

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns(user);

        _passwordHasher
            .Verify("ValidPassword123", user.PasswordHash)
            .Returns(true);

        _tokenService
            .GenerateToken(user)
            .Returns("generated-token");

        var request = new LoginRequest
        {
            Email = "  PROFESSOR@GYMFLOW.DEV  ",
            Password = "ValidPassword123"
        };

        var result = await _service.LoginAsync(request);

        Assert.NotNull(result);

        await _userRepository
            .Received(1)
            .GetByEmailAsync("professor@gymflow.dev");
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordShorterThan8Characters_ShouldRejectRegistration()
    {
        var gymId = Guid.NewGuid();

        var request = new RegisterRequest
        {
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            Password = "Senha12"
        };

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns((User?)null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RegisterAsync(gymId, request));

        _passwordHasher
            .DidNotReceive()
            .Hash(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordExactly8Characters_ShouldCreateUser()
    {
        var gymId = Guid.NewGuid();

        var request = new RegisterRequest
        {
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            Password = "Senha123"
        };

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash(request.Password)
            .Returns("hashed-password");

        var result = await _service.RegisterAsync(gymId, request);

        Assert.True(result);

        await _userRepository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.GymId == gymId &&
                    user.PasswordHash == "hashed-password"));
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordExactly128Characters_ShouldCreateUser()
    {
        var gymId = Guid.NewGuid();
        var password = new string('a', 128);

        var request = new RegisterRequest
        {
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            Password = password
        };

        _userRepository
            .GetByEmailAsync("professor@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash(password)
            .Returns("hashed-password");

        var result = await _service.RegisterAsync(gymId, request);

        Assert.True(result);

        _passwordHasher
            .Received(1)
            .Hash(password);

        await _userRepository
            .Received(1)
            .AddAsync(Arg.Any<User>());
    }

    [Fact]
    public async Task RegisterAsync_WithPasswordLongerThan128Characters_ShouldRejectRegistration()
    {
        var request = new RegisterRequest
        {
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            Password = new string('a', 129)
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RegisterAsync(Guid.NewGuid(), request));

        _passwordHasher
            .DidNotReceive()
            .Hash(Arg.Any<string>());

        await _userRepository
            .DidNotReceive()
            .AddAsync(Arg.Any<User>());
    }

    [Theory]
    [InlineData("Name", "no máximo 150")]
    [InlineData("Email", "no máximo 200")]
    public async Task RegisterAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrowArgumentException(
    string field,
    string expectedMessage)
    {
        var request = new RegisterRequest
        {
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            Password = "ValidPassword123"
        };

        switch (field)
        {
            case "Name":
                request.Name = new string('N', 151);
                break;

            case "Email":
                request.Email =
                    new string('a', 189) + "@gymflow.dev";
                break;
        }

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.RegisterAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    private static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            GymId = Guid.NewGuid(),
            Name = "Professor Teste",
            Email = "professor@gymflow.dev",
            PasswordHash = "stored-password-hash",
            Role = UserRole.Professor,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }
}