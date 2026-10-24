using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GymFlow.IntegrationTests.Security;

// Uses the existing isolated factory and the production JWT middleware, policies and repositories.
public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public Task ErpEndpoints_RequireExplicitTenantEntitlement_AndLegacyOperationsRemainAvailable() =>
        CheckErpEndpointEntitlement(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_ErpEndpoints_RequireExplicitTenantEntitlement_AndLegacyOperationsRemainAvailable(bool postgres) =>
        CheckErpEndpointEntitlement(postgres);

    private static async Task CheckErpEndpointEntitlement(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var disabledAdmin = factory.AddUser(factory.DisabledGym, UserRole.Admin);
        var legacyAdmin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var enabledAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();

        foreach (var (admin, expectedEnabled) in new[]
        {
            (disabledAdmin, false),
            (legacyAdmin, false),
            (enabledAdmin, true)
        })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
            var features = await client.GetFromJsonAsync<JsonElement>("/api/gym-features");
            Assert.Equal(expectedEnabled, features.GetProperty("erpEnabled").GetBoolean());

            var migrationResponse = await client.GetAsync("/api/migrations");
            Assert.Equal(expectedEnabled ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                migrationResponse.StatusCode);

            var importResponse = await client.PostAsJsonAsync("/api/students/imports/preview", new { rows = Array.Empty<object>() });
            if (expectedEnabled)
                Assert.NotEqual(HttpStatusCode.Forbidden, importResponse.StatusCode);
            else
                Assert.Equal(HttpStatusCode.Forbidden, importResponse.StatusCode);
        }

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(legacyAdmin));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard/operational")).StatusCode);

        var legacyStudent = factory.AddUser(factory.LegacyGym);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            legacyStudent.Email,
            Password = TestApi.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var studentToken = (await login.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new("Bearer", studentToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task IntegratedEndpoints_WithoutJwt_Return401()
    {
        await using var factory = new TestApi();
        using var client = factory.CreateClient();
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(endpoint)).StatusCode);
        foreach (var endpoint in new[] { "/api/check-ins/manual", "/api/access-agent/check-ins/physical-passage" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Professor)]
    public async Task IntegratedEndpoints_NonAdminJwt_Return403(UserRole role)
    {
        await using var factory = new TestApi();
        var user = factory.AddUser(factory.LegacyGym, role);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(endpoint)).StatusCode);
        foreach (var endpoint in new[] { "/api/check-ins/manual", "/api/access-agent/check-ins/physical-passage" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(endpoint, new { })).StatusCode);
    }

    [Fact]
    public Task IntegratedAdminEndpoints_IsolateTenantsAndDeduplicateCheckIns() => CheckIntegratedAdmin(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_IntegratedAdminEndpoints_IsolateTenantsAndDeduplicateCheckIns(bool postgres) =>
        CheckIntegratedAdmin(postgres);

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_CheckInAndPhysicalEventHistories_PaginateFilterAndIsolateTenants(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var adminA = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var studentA1 = factory.AddUser(factory.LegacyGym);
        var studentA2 = factory.AddUser(factory.LegacyGym);
        var adminB = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentB = factory.AddUser(factory.EnabledGym);
        Guid studentA1Id = default, studentA2Id = default, studentBId = default;
        factory.WithDb(db =>
        {
            studentA1Id = db.Students.Single(student => student.UserId == studentA1.Id).Id;
            studentA2Id = db.Students.Single(student => student.UserId == studentA2.Id).Id;
            studentBId = db.Students.Single(student => student.UserId == studentB.Id).Id;
        });

        var baseline = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var checkInsA = Enumerable.Range(0, 45).Select(index =>
        {
            var occurredAt = baseline.AddHours(index);
            return new CheckIn
            {
                Id = Guid.NewGuid(), GymId = factory.LegacyGym,
                StudentId = index % 2 == 0 ? studentA1Id : studentA2Id,
                RequestId = Guid.NewGuid(), Source = CheckInSource.Manual,
                RecordedByUserId = adminA.Id, OccurredAt = occurredAt, CreatedAt = occurredAt
            };
        }).ToList();
        var eventsA = Enumerable.Range(0, 45).Select(index =>
        {
            var occurredAt = baseline.AddHours(index);
            var decision = index % 2 == 0
                ? PhysicalAccessDecision.Allowed
                : PhysicalAccessDecision.Denied;
            return new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(), GymId = factory.LegacyGym, RequestId = Guid.NewGuid(),
                StudentId = index % 2 == 0 ? studentA1Id : studentA2Id,
                Decision = decision,
                Reason = decision == PhysicalAccessDecision.Allowed
                    ? PhysicalAccessDecisionReason.Eligible
                    : PhysicalAccessDecisionReason.CredentialNotFound,
                Source = (PhysicalAccessDecisionSource)(index % 3 + 1),
                OccurredAt = occurredAt, ProcessedAt = occurredAt
            };
        }).ToList();
        var checkInsB = Enumerable.Range(0, 3).Select(index => new CheckIn
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, StudentId = studentBId,
            RequestId = Guid.NewGuid(), Source = CheckInSource.Manual,
            RecordedByUserId = adminB.Id, OccurredAt = baseline.AddHours(index),
            CreatedAt = baseline.AddHours(index)
        }).ToList();
        var eventsB = Enumerable.Range(0, 3).Select(index => new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, RequestId = Guid.NewGuid(),
            StudentId = studentBId, Decision = PhysicalAccessDecision.Allowed,
            Reason = PhysicalAccessDecisionReason.Eligible,
            Source = PhysicalAccessDecisionSource.Online,
            OccurredAt = baseline.AddHours(index), ProcessedAt = baseline.AddHours(index)
        }).ToList();
        factory.WithDb(db =>
        {
            db.CheckIns.AddRange(checkInsA.Concat(checkInsB));
            db.PhysicalAccessEvents.AddRange(eventsA.Concat(eventsB));
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminA));

        static async Task<JsonElement> GetPage(HttpClient http, string endpoint, int page)
        {
            using var response = await http.GetAsync($"{endpoint}?page={page}&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        }

        static Guid[] ItemIds(JsonElement page) => page.GetProperty("items")
            .EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();

        static void AssertThreePages(JsonElement page, int expectedPage)
        {
            Assert.Equal(expectedPage, page.GetProperty("page").GetInt32());
            Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
            Assert.Equal(45, page.GetProperty("totalCount").GetInt32());
            Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        }

        var checkInPages = new[]
        {
            await GetPage(client, "/api/check-ins", 1),
            await GetPage(client, "/api/check-ins", 2),
            await GetPage(client, "/api/check-ins", 3)
        };
        var checkInIds = new List<Guid>();
        for (var index = 0; index < checkInPages.Length; index++)
        {
            AssertThreePages(checkInPages[index], index + 1);
            checkInIds.AddRange(ItemIds(checkInPages[index]));
        }
        var expectedCheckInIds = checkInsA.OrderByDescending(item => item.OccurredAt)
            .Select(item => item.Id).ToArray();
        Assert.Equal(expectedCheckInIds, checkInIds);
        Assert.Equal(45, checkInIds.Distinct().Count());

        var eventPages = new[]
        {
            await GetPage(client, "/api/physical-access/events", 1),
            await GetPage(client, "/api/physical-access/events", 2),
            await GetPage(client, "/api/physical-access/events", 3)
        };
        var eventIds = new List<Guid>();
        for (var index = 0; index < eventPages.Length; index++)
        {
            AssertThreePages(eventPages[index], index + 1);
            eventIds.AddRange(ItemIds(eventPages[index]));
        }
        var expectedEventIds = eventsA.OrderByDescending(item => item.OccurredAt)
            .Select(item => item.Id).ToArray();
        Assert.Equal(expectedEventIds, eventIds);
        Assert.Equal(45, eventIds.Distinct().Count());

        using var checkInFilterResponse = await client.GetAsync(
            $"/api/check-ins?studentId={studentA1Id}&fromDate=2026-10-02&toDate=2026-10-03&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, checkInFilterResponse.StatusCode);
        var checkInFilter = await checkInFilterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var expectedFilteredCheckIns = checkInsA.Where(item => item.StudentId == studentA1Id &&
                item.OccurredAt >= new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc) &&
                item.OccurredAt < new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc))
            .OrderByDescending(item => item.OccurredAt).Select(item => item.Id).ToArray();
        Assert.Equal(expectedFilteredCheckIns, ItemIds(checkInFilter));
        Assert.Equal(expectedFilteredCheckIns.Length, checkInFilter.GetProperty("totalCount").GetInt32());

        using var eventFilterResponse = await client.GetAsync(
            $"/api/physical-access/events?studentId={studentA2Id}&decision=2&source=2&fromDate=2026-10-02&toDate=2026-10-03&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, eventFilterResponse.StatusCode);
        var eventFilter = await eventFilterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var expectedFilteredEvents = eventsA.Where(item => item.StudentId == studentA2Id &&
                item.Decision == PhysicalAccessDecision.Denied &&
                item.Source == PhysicalAccessDecisionSource.OfflineCache &&
                item.OccurredAt >= new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc) &&
                item.OccurredAt < new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc))
            .OrderByDescending(item => item.OccurredAt).Select(item => item.Id).ToArray();
        Assert.NotEmpty(expectedFilteredEvents);
        Assert.Equal(expectedFilteredEvents, ItemIds(eventFilter));
        Assert.Equal(expectedFilteredEvents.Length, eventFilter.GetProperty("totalCount").GetInt32());

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminB));
        var checkInsInOtherGym = await GetPage(client, "/api/check-ins", 1);
        var eventsInOtherGym = await GetPage(client, "/api/physical-access/events", 1);
        Assert.Equal(3, checkInsInOtherGym.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, eventsInOtherGym.GetProperty("totalCount").GetInt32());
        Assert.All(ItemIds(checkInsInOtherGym), id => Assert.Contains(id, checkInsB.Select(item => item.Id)));
        Assert.All(ItemIds(eventsInOtherGym), id => Assert.Contains(id, eventsB.Select(item => item.Id)));

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(adminA));
        var checkInPageBeforeInsert = await GetPage(client, "/api/check-ins", 1);
        var eventPageBeforeInsert = await GetPage(client, "/api/physical-access/events", 1);
        factory.WithDb(db =>
        {
            var insertedAt = baseline.AddDays(10);
            db.CheckIns.Add(new CheckIn
            {
                Id = Guid.NewGuid(), GymId = factory.LegacyGym, StudentId = studentA1Id,
                RequestId = Guid.NewGuid(), Source = CheckInSource.Manual,
                RecordedByUserId = adminA.Id, OccurredAt = insertedAt, CreatedAt = insertedAt
            });
            db.PhysicalAccessEvents.Add(new PhysicalAccessEvent
            {
                Id = Guid.NewGuid(), GymId = factory.LegacyGym, RequestId = Guid.NewGuid(),
                StudentId = studentA1Id, Decision = PhysicalAccessDecision.Allowed,
                Reason = PhysicalAccessDecisionReason.Eligible,
                Source = PhysicalAccessDecisionSource.Online,
                OccurredAt = insertedAt, ProcessedAt = insertedAt
            });
        });
        var shiftedCheckInPage2 = await GetPage(client, "/api/check-ins", 2);
        var shiftedEventPage2 = await GetPage(client, "/api/physical-access/events", 2);
        // A new record before the current offset can repeat the old page boundary.
        Assert.Equal(ItemIds(checkInPageBeforeInsert)[^1], ItemIds(shiftedCheckInPage2)[0]);
        Assert.Equal(ItemIds(eventPageBeforeInsert)[^1], ItemIds(shiftedEventPage2)[0]);
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentManualRetries_RecordOneCheckIn(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        Guid studentId = default;
        factory.WithDb(db => studentId = db.Students.Single(s => s.UserId == student.Id).Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        SynchronizeNextTwoSaves(factory);
        var request = new { studentId, requestId = Guid.NewGuid() };
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
            client.PostAsJsonAsync("/api/check-ins/manual", request)));
        var ids = new HashSet<Guid>();
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                ids.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            }
        }
        Assert.Single(ids);
        factory.WithDb(db =>
        {
            var checkIn = Assert.Single(db.CheckIns);
            Assert.Equal(request.requestId, checkIn.RequestId);
            Assert.Equal(factory.LegacyGym, checkIn.GymId);
            Assert.Equal(studentId, checkIn.StudentId);
        });
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentManualRequestsWithSameRequestIdAndDifferentStudents_Conflict(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var firstStudent = factory.AddUser(factory.LegacyGym);
        var secondStudent = factory.AddUser(factory.LegacyGym);
        Guid firstStudentId = default, secondStudentId = default;
        factory.WithDb(db =>
        {
            firstStudentId = db.Students.Single(s => s.UserId == firstStudent.Id).Id;
            secondStudentId = db.Students.Single(s => s.UserId == secondStudent.Id).Id;
        });

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var requestId = Guid.NewGuid();
        SynchronizeNextTwoSaves(factory);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/check-ins/manual", new { studentId = firstStudentId, requestId }),
            client.PostAsJsonAsync("/api/check-ins/manual", new { studentId = secondStudentId, requestId }));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var successful = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var persistedStudentId = (await successful.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("studentId").GetGuid();
            Assert.Contains(persistedStudentId, new[] { firstStudentId, secondStudentId });
            factory.WithDb(db =>
            {
                var checkIn = Assert.Single(db.CheckIns);
                Assert.Equal(factory.LegacyGym, checkIn.GymId);
                Assert.Equal(requestId, checkIn.RequestId);
                Assert.Equal(persistedStudentId, checkIn.StudentId);
                Assert.Equal(CheckInSource.Manual, checkIn.Source);
            });
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentManualAndPhysicalRequestsWithSameRequestId_ConflictWithoutCrossLinking(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var manualStudent = factory.AddUser(factory.LegacyGym);
        var physicalStudent = factory.AddUser(factory.LegacyGym);
        Guid manualStudentId = default, physicalStudentId = default;
        factory.WithDb(db =>
        {
            manualStudentId = db.Students.Single(s => s.UserId == manualStudent.Id).Id;
            physicalStudentId = db.Students.Single(s => s.UserId == physicalStudent.Id).Id;
        });

        var requestId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic race agent",
            MachineName = "isolated-test", SecretHash = "unused", CredentialVersion = 1,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-a\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true}]"
        };
        var accessEvent = new PhysicalAccessEvent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, RequestId = requestId,
            AccessAgentId = agent.Id, DeviceKey = "door-a",
            StudentId = physicalStudentId, Decision = PhysicalAccessDecision.Allowed,
            Reason = PhysicalAccessDecisionReason.Eligible, OccurredAt = occurredAt
        };
        factory.WithDb(db =>
        {
            db.AccessAgents.Add(agent);
            db.PhysicalAccessEvents.Add(accessEvent);
        });

        using var manualClient = factory.CreateClient();
        manualClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        using var agentScope = factory.Services.CreateScope();
        using var physicalClient = factory.CreateClient();
        physicalClient.DefaultRequestHeaders.Authorization = new("Bearer",
            agentScope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));

        SynchronizeNextTwoSaves(factory);
        var responses = await Task.WhenAll(
            manualClient.PostAsJsonAsync("/api/check-ins/manual",
                new { studentId = manualStudentId, requestId }),
            physicalClient.PostAsJsonAsync("/api/access-agent/check-ins/physical-passage",
                new { requestId, occurredAt, deviceKey = "door-a" }));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var successful = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var responseBody = await successful.Content.ReadFromJsonAsync<JsonElement>();
            var persistedId = responseBody.GetProperty("id").GetGuid();
            factory.WithDb(db =>
            {
                var checkIn = Assert.Single(db.CheckIns);
                Assert.Equal(persistedId, checkIn.Id);
                Assert.Equal(factory.LegacyGym, checkIn.GymId);
                Assert.Equal(requestId, checkIn.RequestId);
                if (checkIn.Source == CheckInSource.Manual)
                {
                    Assert.Equal(manualStudentId, checkIn.StudentId);
                    Assert.Null(checkIn.PhysicalAccessEventId);
                }
                else
                {
                    Assert.Equal(CheckInSource.PhysicalAccess, checkIn.Source);
                    Assert.Equal(physicalStudentId, checkIn.StudentId);
                    Assert.Equal(accessEvent.Id, checkIn.PhysicalAccessEventId);
                }
            });
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    private static void SynchronizeNextTwoSaves(TestApi factory)
    {
        var arrived = 0;
        var barrier = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.BeforeEveryAsyncSave = async _ =>
        {
            if (Interlocked.Increment(ref arrived) == 2)
                barrier.TrySetResult(true);
            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(10));
        };
    }

    [PostgresTheory]
    [InlineData(true)]
    public async Task PostgreSql_ConcurrentAccessDecisionsWithSameRequestIdAndDifferentCredentials_ReturnOneDecisionAndOneConflict(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var firstStudent = factory.AddUser(factory.LegacyGym);
        var secondStudent = factory.AddUser(factory.LegacyGym);
        Guid firstStudentId = default, secondStudentId = default;
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic decision race",
            MachineName = "isolated-test", SecretHash = "unused", CredentialVersion = 1,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-a\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true}]"
        };
        factory.WithDb(db =>
        {
            firstStudentId = db.Students.Single(s => s.UserId == firstStudent.Id).Id;
            secondStudentId = db.Students.Single(s => s.UserId == secondStudent.Id).Id;
            db.AccessAgents.Add(agent);
            db.PhysicalAccessCredentials.AddRange(
                new PhysicalAccessCredential
                {
                    Id = Guid.NewGuid(), GymId = factory.LegacyGym, StudentId = firstStudentId,
                    Type = PhysicalAccessCredentialType.Card, ProviderKey = "toletus-litenet2",
                    ExternalIdentifier = "race-card-one", IsActive = true
                },
                new PhysicalAccessCredential
                {
                    Id = Guid.NewGuid(), GymId = factory.LegacyGym, StudentId = secondStudentId,
                    Type = PhysicalAccessCredentialType.Card, ProviderKey = "toletus-litenet2",
                    ExternalIdentifier = "race-card-two", IsActive = true
                });
        });

        using var scope = factory.Services.CreateScope();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        var requestId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        object Body(string credential) => new
        {
            requestId, deviceKey = "door-a", providerKey = "toletus-litenet2",
            credentialType = (int)PhysicalAccessCredentialType.Card,
            externalIdentifier = credential, occurredAt
        };

        SynchronizeNextTwoSaves(factory);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/access-agent/physical-access/decisions", Body("race-card-one")),
            client.PostAsJsonAsync("/api/access-agent/physical-access/decisions", Body("race-card-two")));

        try
        {
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var winnerStudentId = (await success.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("studentId").GetGuid();
            Assert.Contains(winnerStudentId, new[] { firstStudentId, secondStudentId });
            factory.WithDb(db =>
            {
                var decision = Assert.Single(db.PhysicalAccessEvents.Where(e => e.RequestId == requestId));
                Assert.Equal(winnerStudentId, decision.StudentId);
                Assert.Equal(agent.Id, decision.AccessAgentId);
                Assert.Equal("door-a", decision.DeviceKey);
                Assert.NotNull(decision.RequestFingerprint);
                Assert.Empty(db.CheckIns);
            });

            requestId = Guid.NewGuid();
            var identicalBody = Body("race-card-one");
            SynchronizeNextTwoSaves(factory);
            var identicalResponses = await Task.WhenAll(
                client.PostAsJsonAsync("/api/access-agent/physical-access/decisions", identicalBody),
                client.PostAsJsonAsync("/api/access-agent/physical-access/decisions", identicalBody));
            try
            {
                Assert.All(identicalResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
                var decisionIds = await Task.WhenAll(identicalResponses.Select(async response =>
                    (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestId").GetGuid()));
                Assert.All(decisionIds, id => Assert.Equal(requestId, id));
                factory.WithDb(db =>
                {
                    var persisted = Assert.Single(db.PhysicalAccessEvents.Where(e => e.RequestId == requestId));
                    Assert.Equal(agent.Id, persisted.AccessAgentId);
                    Assert.Equal("door-a", persisted.DeviceKey);
                    Assert.NotNull(persisted.RequestFingerprint);
                    Assert.Empty(db.CheckIns);
                });
            }
            finally
            {
                foreach (var response in identicalResponses) response.Dispose();
            }
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    private static async Task CheckIntegratedAdmin(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        var student = factory.AddUser(factory.LegacyGym);
        var foreign = factory.AddUser(factory.EnabledGym);
        Guid studentId = default, foreignId = default;
        factory.WithDb(db =>
        {
            studentId = db.Students.Single(s => s.UserId == student.Id).Id;
            foreignId = db.Students.Single(s => s.UserId == foreign.Id).Id;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var request = new { studentId, requestId = Guid.NewGuid() };
        var first = await client.PostAsJsonAsync("/api/check-ins/manual", request);
        var repeat = await client.PostAsJsonAsync("/api/check-ins/manual", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(),
            (await repeat.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/check-ins/manual",
            new { studentId = foreignId, request.requestId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/check-ins/manual",
            new { studentId = foreignId, requestId = Guid.NewGuid() })).StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>("/api/check-ins");
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        Assert.Equal(studentId, history.GetProperty("items")[0].GetProperty("studentId").GetGuid());
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard/operational");
        Assert.Equal(1, dashboard.GetProperty("activeStudents").GetInt32());
        Assert.False(dashboard.TryGetProperty("activeEnrollments", out _));
        Assert.False(dashboard.TryGetProperty("receivedRevenue", out _));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            "/api/access-agent/check-ins/physical-passage", new { })).StatusCode);

        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var sameRequestIdInOtherGym = await client.PostAsJsonAsync("/api/check-ins/manual",
            new { studentId = foreignId, request.requestId });
        Assert.Equal(HttpStatusCode.OK, sameRequestIdInOtherGym.StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/check-ins")).GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public Task IntegratedAccessAgent_AllowedIsNotPresenceAndPassageIsTenantScoped() => CheckIntegratedAgent(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task PostgreSql_IntegratedAccessAgent_AllowedIsNotPresenceAndPassageIsTenantScoped(bool postgres) =>
        CheckIntegratedAgent(postgres);

    private static async Task CheckIntegratedAgent(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var student = factory.AddUser(factory.LegacyGym);
        var foreign = factory.AddUser(factory.EnabledGym);
        if (postgres)
        {
            factory.WithDb(db =>
            {
                var applied = db.Database.GetAppliedMigrations().ToHashSet(StringComparer.Ordinal);
                Assert.Contains("20261024190000_BindPhysicalAccessEventsToAgentDevices", applied);
                Assert.Contains("20261024200000_AddPhysicalAccessRequestFingerprint", applied);
                Assert.Empty(db.Database.GetPendingMigrations());

                db.Database.OpenConnection();
                try
                {
                    var connection = db.Database.GetDbConnection();
                    string ScalarString(string sql)
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = sql;
                        return (string)command.ExecuteScalar()!;
                    }
                    Assert.Equal("YES", ScalarString("SELECT is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='PhysicalAccessEvents' AND column_name='AccessAgentId'"));
                    Assert.Equal("YES", ScalarString("SELECT is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='PhysicalAccessEvents' AND column_name='DeviceKey'"));
                    Assert.Equal("YES", ScalarString("SELECT is_nullable FROM information_schema.columns WHERE table_schema='public' AND table_name='PhysicalAccessEvents' AND column_name='RequestFingerprint'"));
                    Assert.Equal("IX_PhysicalAccessEvents_AccessAgentId", ScalarString("SELECT indexname FROM pg_indexes WHERE schemaname='public' AND tablename='PhysicalAccessEvents' AND indexname='IX_PhysicalAccessEvents_AccessAgentId'"));
                    Assert.Equal("r", ScalarString("SELECT confdeltype::text FROM pg_constraint WHERE conname='FK_PhysicalAccessEvents_AccessAgents_AccessAgentId'"));
                }
                finally
                {
                    db.Database.CloseConnection();
                }
            });
        }
        var ownRequest = Guid.NewGuid();
        var foreignRequest = Guid.NewGuid();
        var deniedRequest = Guid.NewGuid();
        var historicalRequest = Guid.NewGuid();
        var now = DateTime.UtcNow;
        const string cardIdentifier = "synthetic-card-83";
        var agent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Synthetic integrated agent",
            MachineName = "isolated-test", SecretHash = "unused", CredentialVersion = 1,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-a\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true},{\"deviceKey\":\"door-b\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true}]"
        };
        var otherAgent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.LegacyGym, Name = "Second synthetic agent",
            MachineName = "isolated-test-2", SecretHash = "unused", CredentialVersion = 1,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-a\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true}]"
        };
        var otherGymAgent = new AccessAgent
        {
            Id = Guid.NewGuid(), GymId = factory.EnabledGym, Name = "Other gym synthetic agent",
            MachineName = "isolated-test-other-gym", SecretHash = "unused", CredentialVersion = 1,
            DeviceConfigurationJson = "[{\"deviceKey\":\"door-f\",\"providerKey\":\"toletus-litenet2\",\"enabled\":true}]"
        };
        factory.WithDb(db =>
        {
            db.AccessAgents.AddRange(agent, otherAgent, otherGymAgent);
            db.PhysicalAccessCredentials.Add(new PhysicalAccessCredential
            {
                Id = Guid.NewGuid(),
                GymId = factory.LegacyGym,
                StudentId = db.Students.Single(s => s.UserId == student.Id).Id,
                Type = PhysicalAccessCredentialType.Card,
                ProviderKey = "toletus-litenet2",
                ExternalIdentifier = cardIdentifier,
                IsActive = true
            });
            foreach (var (user, request, decision) in new[]
            {
                (foreign, foreignRequest, PhysicalAccessDecision.Allowed),
                (student, deniedRequest, PhysicalAccessDecision.Denied),
                (student, historicalRequest, PhysicalAccessDecision.Allowed)
            })
                db.PhysicalAccessEvents.Add(new PhysicalAccessEvent
                {
                    Id = Guid.NewGuid(), GymId = user.GymId, RequestId = request,
                    StudentId = db.Students.Single(s => s.UserId == user.Id).Id,
                    AccessAgentId = user == foreign || request == historicalRequest ? null : agent.Id,
                    DeviceKey = user == foreign || request == historicalRequest ? null : "door-a",
                    Decision = decision, Reason = PhysicalAccessDecisionReason.Eligible, OccurredAt = now
                });
            var historical = db.PhysicalAccessEvents.Local.Single(e => e.RequestId == historicalRequest);
            Assert.Null(historical.AccessAgentId);
            Assert.Null(historical.DeviceKey);
            Assert.Null(historical.RequestFingerprint);
            Assert.Empty(db.CheckIns);
        });
        using var scope = factory.Services.CreateScope();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        var otherAgentToken = scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(otherAgent);
        foreach (var endpoint in new[] { "/api/check-ins", "/api/physical-access/events", "/api/dashboard" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(endpoint)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/check-ins/manual", new { })).StatusCode);
        var decisionBody = new
        {
            requestId = ownRequest,
            deviceKey = "door-a",
            providerKey = "toletus-litenet2",
            credentialType = (int)PhysicalAccessCredentialType.Card,
            externalIdentifier = cardIdentifier,
            occurredAt = now
        };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new { requestId = Guid.NewGuid(), deviceKey = "unconfigured-door",
                decisionBody.providerKey, decisionBody.credentialType,
                decisionBody.externalIdentifier, decisionBody.occurredAt })).StatusCode);
        var decisionResponse = await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            decisionBody);
        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
        var decision = await decisionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal((int)PhysicalAccessDecision.Allowed, decision.GetProperty("decision").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions", decisionBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new { decisionBody.requestId, decisionBody.deviceKey, decisionBody.providerKey,
                decisionBody.credentialType, externalIdentifier = "different-card", decisionBody.occurredAt })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new { decisionBody.requestId, decisionBody.deviceKey, decisionBody.providerKey,
                credentialType = (int)PhysicalAccessCredentialType.BiometricExternalId,
                decisionBody.externalIdentifier, decisionBody.occurredAt })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new { decisionBody.requestId, deviceKey = "door-b", decisionBody.providerKey,
                decisionBody.credentialType, decisionBody.externalIdentifier, decisionBody.occurredAt })).StatusCode);

        var offlineRequestId = Guid.NewGuid();
        var offlineOccurredAt = now.AddMinutes(-3);
        var offlineProcessedAt = now.AddMinutes(-2);
        var offlineBody = new
        {
            requestId = offlineRequestId,
            deviceKey = "door-a",
            providerKey = "toletus-litenet2",
            credentialType = (int)PhysicalAccessCredentialType.Card,
            externalIdentifier = cardIdentifier,
            occurredAt = offlineOccurredAt,
            decision = (int)PhysicalAccessDecision.Allowed,
            reason = (int)PhysicalAccessDecisionReason.Eligible,
            source = (int)PhysicalAccessDecisionSource.OfflineCache,
            processedAt = offlineProcessedAt
        };
        const string offlineSync = "/api/access-agent/physical-access/offline-events";
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync(offlineSync, offlineBody)).StatusCode);
        // Retry the exact persisted queue payload, including both timestamps.
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync(offlineSync, offlineBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync("/api/access-agent/check-ins/physical-passage",
                new { requestId = offlineRequestId, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await client.PostAsJsonAsync(offlineSync,
                new { offlineBody.requestId, offlineBody.deviceKey, offlineBody.providerKey,
                    offlineBody.credentialType, externalIdentifier = "tampered-card",
                    offlineBody.occurredAt, offlineBody.decision, offlineBody.reason,
                    offlineBody.source, offlineBody.processedAt })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(offlineSync,
                new { requestId = Guid.NewGuid(), offlineBody.deviceKey, offlineBody.providerKey,
                    offlineBody.credentialType, offlineBody.externalIdentifier,
                    offlineBody.occurredAt, offlineBody.decision,
                    reason = (int)PhysicalAccessDecisionReason.FinancialRestriction,
                    offlineBody.source, offlineBody.processedAt })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", otherAgentToken);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new { decisionBody.requestId, deviceKey = "door-a", decisionBody.providerKey,
                decisionBody.credentialType, decisionBody.externalIdentifier, decisionBody.occurredAt })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        factory.WithDb(db =>
        {
            var authorization = Assert.Single(db.PhysicalAccessEvents.Where(e => e.RequestId == ownRequest));
            Assert.Equal(agent.Id, authorization.AccessAgentId);
            Assert.Equal("door-a", authorization.DeviceKey);
            Assert.Equal(db.Students.Single(s => s.UserId == student.Id).Id, authorization.StudentId);
            Assert.Empty(db.CheckIns);
        });
        const string passage = "/api/access-agent/check-ins/physical-passage";
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(passage,
            new { requestId = foreignRequest, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(passage,
            new { requestId = deniedRequest, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(passage,
            new { requestId = historicalRequest, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        // Same gym, wrong agent and same agent with wrong device cannot claim the authorization.
        client.DefaultRequestHeaders.Authorization = new("Bearer", otherAgentToken);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(passage,
            new { requestId = ownRequest, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(agent));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(passage,
            new { requestId = ownRequest, occurredAt = now, deviceKey = "door-b" })).StatusCode);
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(passage,
                new { requestId = ownRequest, occurredAt = now, deviceKey = "door-a" })).StatusCode);
        factory.WithDb(db =>
        {
            var checkIn = Assert.Single(db.CheckIns);
            Assert.Equal(factory.LegacyGym, checkIn.GymId);
            Assert.Equal(CheckInSource.PhysicalAccess, checkIn.Source);
            Assert.Equal(ownRequest, checkIn.RequestId);
        });

        var admin = factory.AddUser(factory.LegacyGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var ownEvents = await client.GetFromJsonAsync<JsonElement>("/api/physical-access/events");
        Assert.Equal(4, ownEvents.GetProperty("totalCount").GetInt32());
        Assert.All(ownEvents.GetProperty("items").EnumerateArray(), item =>
            Assert.NotEqual(foreignRequest, item.GetProperty("requestId").GetGuid()));
        var adminDecisionBody = new
        {
            requestId = Guid.NewGuid(), deviceKey = "admin-origin-request",
            providerKey = "toletus-litenet2",
            credentialType = (int)PhysicalAccessCredentialType.Card,
            externalIdentifier = cardIdentifier, occurredAt = now
        };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/physical-access/decisions", adminDecisionBody)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(
            "/api/physical-access/decisions",
            new { adminDecisionBody.requestId, adminDecisionBody.deviceKey,
                adminDecisionBody.providerKey, adminDecisionBody.credentialType,
                externalIdentifier = "different-admin-card", adminDecisionBody.occurredAt })).StatusCode);
        var otherAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            scope.ServiceProvider.GetRequiredService<IAccessAgentTokenService>().GenerateToken(otherGymAgent));
        var sameRequestInOtherGym = await client.PostAsJsonAsync(
            "/api/access-agent/physical-access/decisions",
            new
            {
                requestId = ownRequest, deviceKey = "door-f", providerKey = "toletus-litenet2",
                credentialType = (int)PhysicalAccessCredentialType.Card,
                externalIdentifier = cardIdentifier, occurredAt = now
            });
        Assert.Equal(HttpStatusCode.OK, sameRequestInOtherGym.StatusCode);
        Assert.Equal((int)PhysicalAccessDecision.Denied,
            (await sameRequestInOtherGym.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("decision").GetInt32());
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(otherAdmin));
        var foreignEvents = await client.GetFromJsonAsync<JsonElement>("/api/physical-access/events");
        Assert.Equal(2, foreignEvents.GetProperty("totalCount").GetInt32());
        Assert.Contains(foreignEvents.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("requestId").GetGuid() == foreignRequest);
        Assert.Contains(foreignEvents.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("requestId").GetGuid() == ownRequest);
        factory.WithDb(db =>
        {
            var sameRequestEvents = db.PhysicalAccessEvents.Where(e => e.RequestId == ownRequest).ToArray();
            Assert.Equal(2, sameRequestEvents.Length);
            Assert.Contains(sameRequestEvents, e => e.GymId == factory.LegacyGym && e.Decision == PhysicalAccessDecision.Allowed);
            Assert.Contains(sameRequestEvents, e => e.GymId == factory.EnabledGym && e.Decision == PhysicalAccessDecision.Denied);
        });
    }
}
