using GymFlow.Application.DTOs.PhysicalAccess;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Application.Services;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAccessPinTests
{
    [Fact]
    public async Task ManualPinCreationIsRejectedAndNormalListingDoesNotRevealLegacyPin()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var students = Substitute.For<IStudentRepository>();
        var credentials = Substitute.For<IPhysicalAccessCredentialRepository>();
        students.GetByIdAndGymIdAsync(studentId, gymId)
            .Returns(new Student { Id = studentId, GymId = gymId, IsActive = true });
        credentials.GetByStudentAsync(studentId, gymId).Returns(
            [new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(), GymId = gymId, StudentId = studentId,
                Type = PhysicalAccessCredentialType.PinExternalId,
                ProviderKey = "toletus-litenet2", ExternalIdentifier = "1234"
            }]);
        var service = new PhysicalAccessCredentialService(
            credentials, students, Substitute.For<IPhysicalAccessPinProtector>());

        var created = await service.CreateAsync(gymId,
            new CreatePhysicalAccessCredentialRequest
            {
                StudentId = studentId, Type = PhysicalAccessCredentialType.PinExternalId,
                ProviderKey = "toletus-litenet2", ExternalIdentifier = "1234"
            });
        var listed = await service.ListByStudentAsync(gymId, studentId);

        Assert.Null(created);
        Assert.Single(listed);
        Assert.Equal(string.Empty, listed[0].ExternalIdentifier);
        await credentials.DidNotReceive().AddAsync(Arg.Any<PhysicalAccessCredential>());
    }

    [Fact]
    public async Task IssueAndResetUseFourDigitsAndStoreOnlyTenantBoundHash()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var students = Substitute.For<IStudentRepository>();
        var credentials = Substitute.For<IPhysicalAccessCredentialRepository>();
        var protector = Substitute.For<IPhysicalAccessPinProtector>();
        var stored = new List<PhysicalAccessCredential>();
        students.GetByIdAndGymIdAsync(studentId, gymId)
            .Returns(new Student { Id = studentId, GymId = gymId, IsActive = true });
        credentials.GetByStudentAsync(studentId, gymId)
            .Returns(_ => Task.FromResult(stored.ToList()));
        protector.ComputeLookupHash(gymId, Arg.Any<string>())
            .Returns(call => $"hash:{gymId:N}:{call.ArgAt<string>(1)}");
        credentials.GetByExternalIdentifierAsync(gymId,
                PhysicalAccessCredentialService.PinProviderKey,
                PhysicalAccessCredentialType.PinExternalId, Arg.Any<string>())
            .Returns(call => Task.FromResult<PhysicalAccessCredential?>(
                stored.FirstOrDefault(item => item.ExternalIdentifier == call.ArgAt<string>(3))));
        credentials.AddAsync(Arg.Any<PhysicalAccessCredential>())
            .Returns(call => { stored.Add(call.Arg<PhysicalAccessCredential>()); return Task.CompletedTask; });
        var service = new PhysicalAccessCredentialService(credentials, students, protector);

        var first = await service.IssuePinAsync(gymId, studentId);
        var second = await service.IssuePinAsync(gymId, studentId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Matches("^[0-9]{4}$", first.Pin);
        Assert.Matches("^[0-9]{4}$", second.Pin);
        Assert.NotEqual(first.Pin, second.Pin);
        Assert.Single(stored);
        Assert.Equal(first.Credential.Id, second.Credential.Id);
        Assert.Equal($"hash:{gymId:N}:{second.Pin}", stored[0].ExternalIdentifier);
        Assert.Equal(string.Empty, second.Credential.ExternalIdentifier);
        await credentials.Received(1).AddAsync(Arg.Any<PhysicalAccessCredential>());
        await credentials.Received(1).UpdateAsync(stored[0]);
    }

    [Fact]
    public async Task IssueRejectsOtherTenantAndArchivedStudent()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var students = Substitute.For<IStudentRepository>();
        var credentials = Substitute.For<IPhysicalAccessCredentialRepository>();
        var service = new PhysicalAccessCredentialService(credentials, students,
            Substitute.For<IPhysicalAccessPinProtector>());

        Assert.Null(await service.IssuePinAsync(gymId, studentId));
        students.GetByIdAndGymIdAsync(studentId, gymId).Returns(
            new Student { Id = studentId, GymId = gymId, ArchivedAt = DateTime.UtcNow });
        Assert.Null(await service.IssuePinAsync(gymId, studentId));
        await credentials.DidNotReceive().AddAsync(Arg.Any<PhysicalAccessCredential>());
    }

}
