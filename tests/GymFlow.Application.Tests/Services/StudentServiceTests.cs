using GymFlow.Application.DTOs.Students;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class StudentServiceTests
{
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly StudentService _service;

    public StudentServiceTests()
    {
        _userRepository =
            Substitute.For<IUserRepository>();

        _studentRepository =
            Substitute.For<IStudentRepository>();

        _passwordHasher =
            Substitute.For<IPasswordHasher>();

        _service = new StudentService(
            _userRepository,
            _studentRepository,
            _passwordHasher);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_ShouldCreateStudentAndUser()
    {
        var gymId = Guid.NewGuid();
        var birthDate = new DateOnly(2000, 5, 10);

        var request = new CreateStudentRequest
        {
            Name = "  Aluno Teste  ",
            Email = "  ALUNO@GYMFLOW.DEV  ",
            Password = "Senha123",
            Phone = "  21999999999  ",
            BirthDate = birthDate
        };

        _userRepository
            .GetByEmailAsync("aluno@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash(request.Password)
            .Returns("hashed-password");

        var result =
            await _service.CreateAsync(gymId, request);

        Assert.True(result);

        await _studentRepository
            .Received(1)
            .AddAsync(
                Arg.Is<User>(user =>
                    user.GymId == gymId &&
                    user.Name == "Aluno Teste" &&
                    user.Email == "aluno@gymflow.dev" &&
                    user.PasswordHash == "hashed-password" &&
                    user.Role == UserRole.Student &&
                    user.IsActive),
                Arg.Is<Student>(student =>
                    student.UserId == student.User.Id &&
                    student.User.GymId == gymId &&
                    student.User.Name == "Aluno Teste" &&
                    student.User.Email == "aluno@gymflow.dev" &&
                    student.Phone == "21999999999" &&
                    student.BirthDate == birthDate));

        _passwordHasher
            .Received(1)
            .Hash("Senha123");
    }

    [Fact]
    public async Task CreateAsync_WhenEmailAlreadyExists_ShouldReturnFalse()
    {
        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            GymId = Guid.NewGuid(),
            Name = "Usuário Existente",
            Email = "aluno@gymflow.dev",
            PasswordHash = "stored-hash",
            Role = UserRole.Student,
            IsActive = true
        };

        var request = new CreateStudentRequest
        {
            Name = "Aluno Teste",
            Email = "  ALUNO@GYMFLOW.DEV  ",
            Password = "Senha123"
        };

        _userRepository
            .GetByEmailAsync("aluno@gymflow.dev")
            .Returns(existingUser);

        var result =
            await _service.CreateAsync(
                Guid.NewGuid(),
                request);

        Assert.False(result);

        _passwordHasher
            .DidNotReceive()
            .Hash(Arg.Any<string>());

        await _studentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<User>(),
                Arg.Any<Student>());
    }

    [Fact]
    public async Task CreateAsync_WithPasswordShorterThan8Characters_ShouldRejectCreation()
    {
        var request = new CreateStudentRequest
        {
            Name = "Aluno Teste",
            Email = "aluno@gymflow.dev",
            Password = "Senha12"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        _passwordHasher
            .DidNotReceive()
            .Hash(Arg.Any<string>());

        await _studentRepository
            .DidNotReceive()
            .AddAsync(
                Arg.Any<User>(),
                Arg.Any<Student>());
    }

    [Fact]
    public async Task CreateAsync_WithPasswordExactly8Characters_ShouldCreateStudent()
    {
        var request = new CreateStudentRequest
        {
            Name = "Aluno Teste",
            Email = "aluno@gymflow.dev",
            Password = "Senha123"
        };

        _userRepository
            .GetByEmailAsync("aluno@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash("Senha123")
            .Returns("hashed-password");

        var result =
            await _service.CreateAsync(
                Guid.NewGuid(),
                request);

        Assert.True(result);

        _passwordHasher
            .Received(1)
            .Hash("Senha123");

        await _studentRepository
            .Received(1)
            .AddAsync(
                Arg.Any<User>(),
                Arg.Any<Student>());
    }

    [Fact]
    public async Task CreateAsync_ShouldNormalizeEmailBeforeCheckingDuplication()
    {
        var request = new CreateStudentRequest
        {
            Name = "Aluno Teste",
            Email = "  ALUNO@GYMFLOW.DEV  ",
            Password = "Senha123"
        };

        _userRepository
            .GetByEmailAsync("aluno@gymflow.dev")
            .Returns((User?)null);

        _passwordHasher
            .Hash(request.Password)
            .Returns("hashed-password");

        await _service.CreateAsync(
            Guid.NewGuid(),
            request);

        await _userRepository
            .Received(1)
            .GetByEmailAsync("aluno@gymflow.dev");
    }
    [Fact]
    public async Task GetAllAsync_WithValidParameters_ShouldReturnPagedStudents()
    {
        var gymId = Guid.NewGuid();

        var student1 = CreateStudent(
            gymId,
            "Aluno Um",
            "aluno1@gymflow.dev",
            true);

        var student2 = CreateStudent(
            gymId,
            "Aluno Dois",
            "aluno2@gymflow.dev",
            false);

        var students = new List<Student>
    {
        student1,
        student2
    };

        _studentRepository
            .GetPagedByGymIdAsync(
                gymId,
                "aluno",
                null,
                20,
                20)
            .Returns(
                Task.FromResult(
                    (students, 25)));

        var result = await _service.GetAllAsync(
            gymId,
            "aluno",
            null,
            page: 2,
            pageSize: 20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(student1.Id, result.Items[0].Id);
        Assert.Equal("Aluno Um", result.Items[0].Name);
        Assert.Equal("aluno1@gymflow.dev", result.Items[0].Email);
        Assert.True(result.Items[0].IsActive);

        Assert.Equal(student2.Id, result.Items[1].Id);
        Assert.False(result.Items[1].IsActive);

        await _studentRepository
            .Received(1)
            .GetPagedByGymIdAsync(
                gymId,
                "aluno",
                null,
                20,
                20);
    }

    [Fact]
    public async Task GetAllAsync_WithPageLessThanOne_ShouldThrowArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetAllAsync(
                Guid.NewGuid(),
                null,
                null,
                page: 0,
                pageSize: 20));

        await _studentRepository
            .DidNotReceive()
            .GetPagedByGymIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task GetAllAsync_WithInvalidPageSize_ShouldThrowArgumentException(
    int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.GetAllAsync(
                Guid.NewGuid(),
                null,
                null,
                page: 1,
                pageSize: pageSize));

        await _studentRepository
            .DidNotReceive()
            .GetPagedByGymIdAsync(
                Arg.Any<Guid>(),
                Arg.Any<string?>(),
                Arg.Any<bool?>(),
                Arg.Any<int>(),
                Arg.Any<int>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenStudentExists_ShouldReturnStudent()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno Teste",
            "aluno@gymflow.dev",
            true);

        _studentRepository
            .GetByIdAndGymIdAsync(
                student.Id,
                gymId)
            .Returns(student);

        var result = await _service.GetByIdAsync(
            student.Id,
            gymId);

        Assert.NotNull(result);
        Assert.Equal(student.Id, result.Id);
        Assert.Equal(student.User.Name, result.Name);
        Assert.Equal(student.User.Email, result.Email);
        Assert.Equal(student.Phone, result.Phone);
        Assert.Equal(student.BirthDate, result.BirthDate);
        Assert.Equal(student.User.IsActive, result.IsActive);
    }

    [Fact]
    public async Task GetByIdAsync_WhenRepositoryDoesNotFindStudent_ShouldReturnNull()
    {
        var studentId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(
                studentId,
                gymId)
            .Returns((Student?)null);

        var result = await _service.GetByIdAsync(
            studentId,
            gymId);

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_ShouldUpdateStudent()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Nome Antigo",
            "antigo@gymflow.dev",
            true);

        var request = new UpdateStudentRequest
        {
            Name = "  Nome Atualizado  ",
            Email = "  NOVO@GYMFLOW.DEV  ",
            Phone = "  21888888888  ",
            BirthDate = new DateOnly(1999, 8, 15)
        };

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        _userRepository
            .GetByEmailAsync("novo@gymflow.dev")
            .Returns((User?)null);

        var result = await _service.UpdateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(UpdateStudentResult.Success, result);

        Assert.Equal("Nome Atualizado", student.User.Name);
        Assert.Equal("novo@gymflow.dev", student.User.Email);
        Assert.Equal("21888888888", student.Phone);
        Assert.Equal(
            new DateOnly(1999, 8, 15),
            student.BirthDate);

        Assert.NotNull(student.UpdatedAt);
        Assert.NotNull(student.User.UpdatedAt);
        Assert.Equal(
            student.UpdatedAt,
            student.User.UpdatedAt);

        await _studentRepository
            .Received(1)
            .UpdateAsync(student);
    }

    [Fact]
    public async Task UpdateAsync_WhenStudentDoesNotExist_ShouldReturnNotFound()
    {
        var studentId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId)
            .Returns((Student?)null);

        var request = new UpdateStudentRequest
        {
            Name = "Aluno",
            Email = "aluno@gymflow.dev"
        };

        var result = await _service.UpdateAsync(
            studentId,
            gymId,
            request);

        Assert.Equal(
            UpdateStudentResult.NotFound,
            result);

        await _userRepository
            .DidNotReceive()
            .GetByEmailAsync(Arg.Any<string>());

        await _studentRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Student>());
    }

    [Fact]
    public async Task UpdateAsync_WhenEmailBelongsToAnotherUser_ShouldReturnEmailAlreadyInUse()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno",
            "atual@gymflow.dev",
            true);

        var anotherUser = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = "Outro Usuário",
            Email = "ocupado@gymflow.dev",
            PasswordHash = "stored-hash",
            Role = UserRole.Student,
            IsActive = true
        };

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        _userRepository
            .GetByEmailAsync("ocupado@gymflow.dev")
            .Returns(anotherUser);

        var request = new UpdateStudentRequest
        {
            Name = "Aluno Atualizado",
            Email = "  OCUPADO@GYMFLOW.DEV  "
        };

        var result = await _service.UpdateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(
            UpdateStudentResult.EmailAlreadyInUse,
            result);

        await _studentRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Student>());
    }

    [Fact]
    public async Task UpdateAsync_WhenEmailBelongsToSameUser_ShouldAllowUpdate()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno",
            "aluno@gymflow.dev",
            true);

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        _userRepository
            .GetByEmailAsync("aluno@gymflow.dev")
            .Returns(student.User);

        var request = new UpdateStudentRequest
        {
            Name = "Aluno Atualizado",
            Email = "  ALUNO@GYMFLOW.DEV  ",
            Phone = "21911111111"
        };

        var result = await _service.UpdateAsync(
            student.Id,
            gymId,
            request);

        Assert.Equal(UpdateStudentResult.Success, result);
        Assert.Equal(
            "aluno@gymflow.dev",
            student.User.Email);

        await _studentRepository
            .Received(1)
            .UpdateAsync(student);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenStudentExists_ShouldUpdateStatus()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno",
            "aluno@gymflow.dev",
            true);

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        var result = await _service.UpdateStatusAsync(
            student.Id,
            gymId,
            false);

        Assert.True(result);
        Assert.False(student.User.IsActive);

        Assert.NotNull(student.UpdatedAt);
        Assert.NotNull(student.User.UpdatedAt);
        Assert.Equal(
            student.UpdatedAt,
            student.User.UpdatedAt);

        await _studentRepository
            .Received(1)
            .UpdateAsync(student);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenStudentDoesNotExist_ShouldReturnFalse()
    {
        var studentId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _studentRepository
            .GetByIdAndGymIdAsync(studentId, gymId)
            .Returns((Student?)null);

        var result = await _service.UpdateStatusAsync(
            studentId,
            gymId,
            false);

        Assert.False(result);

        await _studentRepository
            .DidNotReceive()
            .UpdateAsync(Arg.Any<Student>());
    }

    [Fact]
    public async Task GetMeAsync_WhenStudentExists_ShouldReturnOwnStudent()
    {
        var gymId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno",
            "aluno@gymflow.dev",
            true);

        student.User.Id = userId;
        student.UserId = userId;

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns(student);

        var result = await _service.GetMeAsync(
            userId,
            gymId);

        Assert.NotNull(result);
        Assert.Equal(student.Id, result.Id);
        Assert.Equal("Aluno", result.Name);
        Assert.Equal(
            "aluno@gymflow.dev",
            result.Email);

        await _studentRepository
            .Received(1)
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId);
    }

    [Fact]
    public async Task GetMeAsync_WhenStudentDoesNotExist_ShouldReturnNull()
    {
        var userId = Guid.NewGuid();
        var gymId = Guid.NewGuid();

        _studentRepository
            .GetByUserIdAndGymIdAsync(
                userId,
                gymId)
            .Returns((Student?)null);

        var result = await _service.GetMeAsync(
            userId,
            gymId);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("Name", "no máximo 150")]
    [InlineData("Email", "no máximo 200")]
    [InlineData("Phone", "no máximo 20")]
    public async Task CreateAsync_WhenPersistedTextExceedsDatabaseLimit_ShouldThrowArgumentException(
    string field,
    string expectedMessage)
    {
        var request = new CreateStudentRequest
        {
            Name = "Aluno Teste",
            Email = "aluno@gymflow.dev",
            Password = "Senha123",
            Phone = "21999999999"
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

            case "Phone":
                request.Phone = new string('1', 21);
                break;
        }

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateAsync(
                Guid.NewGuid(),
                request));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenNameExceedsDatabaseLimit_ShouldThrowArgumentException()
    {
        var gymId = Guid.NewGuid();

        var student = CreateStudent(
            gymId,
            "Aluno",
            "aluno@gymflow.dev",
            true);

        _studentRepository
            .GetByIdAndGymIdAsync(student.Id, gymId)
            .Returns(student);

        var request = new UpdateStudentRequest
        {
            Name = new string('N', 151),
            Email = "aluno@gymflow.dev",
            Phone = "21999999999"
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _service.UpdateAsync(
                student.Id,
                gymId,
                request));

        Assert.Contains("no máximo 150", exception.Message);
    }

    private static Student CreateStudent(
    Guid gymId,
    string name,
    string email,
    bool isActive)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            Name = name,
            Email = email,
            PasswordHash = "stored-hash",
            Role = UserRole.Student,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        return new Student
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Phone = "21999999999",
            BirthDate = new DateOnly(2000, 1, 1),
            CreatedAt = DateTime.UtcNow,
            User = user
        };
    }
}