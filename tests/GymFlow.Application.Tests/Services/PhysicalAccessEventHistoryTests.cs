using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Interfaces.Time;
using GymFlow.Application.Services;
using GymFlow.Domain.Enums;
using NSubstitute;

namespace GymFlow.Application.Tests.Services;

public class PhysicalAccessEventHistoryTests
{
    private readonly IPhysicalAccessEventRepository _eventRepository;
    private readonly PhysicalAccessService _service;

    public PhysicalAccessEventHistoryTests()
    {
        _eventRepository =
            Substitute.For<IPhysicalAccessEventRepository>();

        var timeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        _service = new PhysicalAccessService(
            Substitute.For<IPhysicalAccessCredentialRepository>(),
            Substitute.For<IPhysicalAccessOverrideRepository>(),
            _eventRepository,
            Substitute.For<IEnrollmentRepository>(),
            Substitute.For<IChargeRepository>(),
            timeZoneProvider);
    }

    [Fact]
    public async Task GetEventHistoryAsync_WhenValid_ShouldForwardFiltersAndPagination()
    {
        var gymId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var timeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        timeZoneProvider.GetTimeZone(gymId)
            .Returns(TimeZoneInfo.CreateCustomTimeZone(
                "TestUTCMinus3",
                TimeSpan.FromHours(-3),
                "Test UTC-3",
                "Test UTC-3"));

        var service = new PhysicalAccessService(
            Substitute.For<IPhysicalAccessCredentialRepository>(),
            Substitute.For<IPhysicalAccessOverrideRepository>(),
            _eventRepository,
            Substitute.For<IEnrollmentRepository>(),
            Substitute.For<IChargeRepository>(),
            timeZoneProvider);

        var fromUtc = new DateTime(
            2026, 10, 10, 3, 0, 0, DateTimeKind.Utc);

        var toUtc = new DateTime(
            2026, 10, 12, 3, 0, 0, DateTimeKind.Utc);

        _eventRepository.GetPagedAsync(
                gymId,
                studentId,
                PhysicalAccessDecision.Denied,
                PhysicalAccessDecisionSource.OfflineCache,
                fromUtc,
                toUtc,
                20,
                20)
            .Returns((
                new List<GymFlow.Domain.Entities.PhysicalAccessEvent>(),
                25));

        var result = await service.GetEventHistoryAsync(
            gymId,
            studentId,
            PhysicalAccessDecision.Denied,
            PhysicalAccessDecisionSource.OfflineCache,
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 11),
            2,
            20);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Empty(result.Items);

        await _eventRepository.Received(1).GetPagedAsync(
            gymId,
            studentId,
            PhysicalAccessDecision.Denied,
            PhysicalAccessDecisionSource.OfflineCache,
            fromUtc,
            toUtc,
            20,
            20);
    }
    [Fact]
    public async Task GetEventHistoryAsync_WhenEventHasNoStudent_ShouldReturnSafeHistory()
    {
        var gymId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        var timeZoneProvider =
            Substitute.For<IGymTimeZoneProvider>();

        timeZoneProvider.GetTimeZone(gymId)
            .Returns(TimeZoneInfo.Utc);

        var service = new PhysicalAccessService(
            Substitute.For<IPhysicalAccessCredentialRepository>(),
            Substitute.For<IPhysicalAccessOverrideRepository>(),
            _eventRepository,
            Substitute.For<IEnrollmentRepository>(),
            Substitute.For<IChargeRepository>(),
            timeZoneProvider);

        var occurredAt = new DateTime(
            2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

        var accessEvent = new GymFlow.Domain.Entities.PhysicalAccessEvent
        {
            Id = Guid.NewGuid(),
            GymId = gymId,
            RequestId = requestId,
            StudentId = null,
            CredentialId = null,
            Decision = PhysicalAccessDecision.Denied,
            Reason = PhysicalAccessDecisionReason.CredentialNotFound,
            Source = PhysicalAccessDecisionSource.Online,
            OccurredAt = occurredAt,
            ProcessedAt = occurredAt
        };

        _eventRepository.GetPagedAsync(
                gymId, null, null, null,
                null, null, 0, 20)
            .Returns((
                new List<GymFlow.Domain.Entities.PhysicalAccessEvent>
                {
                    accessEvent
                },
                1));

        var result = await service.GetEventHistoryAsync(
            gymId, null, null, null,
            null, null, 1, 20);

        var item = Assert.Single(result.Items);

        Assert.Equal(requestId, item.RequestId);
        Assert.Null(item.StudentId);
        Assert.Null(item.StudentName);
        Assert.Equal(PhysicalAccessDecision.Denied, item.Decision);
        Assert.Equal(
            PhysicalAccessDecisionReason.CredentialNotFound,
            item.Reason);
        Assert.Equal(PhysicalAccessDecisionSource.Online, item.Source);
        Assert.Equal(occurredAt, item.OccurredAt);
        Assert.Equal(1, result.TotalCount);
    }
    [Fact]
    public void HistoryResponse_ShouldNotExposeCredentialIdentifiers()
    {
        var response =
            new GymFlow.Application.DTOs.PhysicalAccess
                .PhysicalAccessEventHistoryItemResponse
            {
                Id = Guid.NewGuid(),
                RequestId = Guid.NewGuid(),
                Decision = PhysicalAccessDecision.Denied,
                Reason = PhysicalAccessDecisionReason.CredentialNotFound,
                Source = PhysicalAccessDecisionSource.Online,
                OccurredAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };

        var json = System.Text.Json.JsonSerializer.Serialize(response);

        Assert.DoesNotContain(
            "CredentialId",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain(
            "ExternalIdentifier",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains("RequestId", json);
        Assert.Contains("Decision", json);
        Assert.Contains("Reason", json);
    }
    [Fact]
    public async Task GetEventHistoryAsync_WhenGymIdIsEmpty_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventHistoryAsync(
                Guid.Empty, null, null, null,
                null, null, 1, 20));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task GetEventHistoryAsync_WhenPaginationIsInvalid_ShouldThrow(
        int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventHistoryAsync(
                Guid.NewGuid(), null, null, null,
                null, null, page, pageSize));
    }

    [Fact]
    public async Task GetEventHistoryAsync_WhenDatesAreReversed_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventHistoryAsync(
                Guid.NewGuid(), null, null, null,
                new DateOnly(2026, 10, 12),
                new DateOnly(2026, 10, 10),
                1, 20));
    }

    [Fact]
    public async Task GetEventHistoryAsync_WhenDecisionIsInvalid_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventHistoryAsync(
                Guid.NewGuid(), null,
                (PhysicalAccessDecision)999,
                null, null, null, 1, 20));
    }

    [Fact]
    public async Task GetEventHistoryAsync_WhenSourceIsInvalid_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.GetEventHistoryAsync(
                Guid.NewGuid(), null, null,
                (PhysicalAccessDecisionSource)999,
                null, null, 1, 20));
    }
}
