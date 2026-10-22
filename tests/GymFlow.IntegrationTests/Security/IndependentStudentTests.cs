using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task IndependentStudent_EmailChangeRevokesPendingInviteOnlyAfterValidUpdate()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Invite target",
            Email = "before@example.test", IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var first = await IssueInvite(client, studentId);
        var invalid = await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "", email = "after@example.test" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token = first })).StatusCode);

        var changed = await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "Invite target", email = "  AFTER@EXAMPLE.TEST  " });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token = first })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate", new { token = first, password = TestApi.Password })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.Equal("after@example.test", student.Email);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Null(student.AppInviteExpiresAt);
            Assert.Null(student.UserId);
            Assert.True(student.AppAccessEnabled);
            Assert.False(db.Users.Any(u => u.Email == "after@example.test"));
        });

        var second = await IssueInvite(client, studentId);
        Assert.NotEqual(first, second);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(
            "/api/students/app-activate", new { token = second, password = TestApi.Password })).StatusCode);
        Guid activatedUserId = default;
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            activatedUserId = student.UserId!.Value;
            Assert.Equal("after@example.test", db.Users.Single(u => u.Id == activatedUserId).Email);
        });
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "Invite target updated", email = "activated@example.test" })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.Equal(activatedUserId, student.UserId);
            Assert.True(student.AppAccessEnabled);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Equal("activated@example.test", db.Users.Single(u => u.Id == activatedUserId).Email);
        });
    }

    [Fact]
    public async Task IndependentStudent_EmailRemovalAndFirstFillRevokePendingTokenWithoutEnablingAccess()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        var removalId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = removalId, GymId = factory.EnabledGym, Name = "Remove email",
            Email = "remove@example.test", IsActive = true
        }));
        var removalToken = await IssueInvite(client, removalId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/students/{removalId}",
            new { name = "Remove email", email = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token = removalToken })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == removalId);
            Assert.Null(student.Email);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Null(student.AppInviteExpiresAt);
            Assert.Null(student.UserId);
            Assert.True(student.AppAccessEnabled);
        });

        // A pre-existing/stale pending hash is revoked when a previously
        // email-less student receives their first address; editing does not
        // manufacture an invite or turn on app access.
        var firstFillId = Guid.NewGuid();
        const string staleToken = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        var staleHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(staleToken)));
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = firstFillId, GymId = factory.EnabledGym, Name = "First email",
            Email = null, IsActive = true, AppAccessEnabled = false,
            AppInviteTokenHash = staleHash, AppInviteExpiresAt = DateTime.UtcNow.AddHours(1)
        }));
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/students/{firstFillId}",
            new { name = "First email", email = "first@example.test" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token = staleToken })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == firstFillId);
            Assert.Equal("first@example.test", student.Email);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Null(student.AppInviteExpiresAt);
            Assert.False(student.AppAccessEnabled);
            Assert.Null(student.UserId);
        });
    }

    [Fact]
    public async Task IndependentStudent_EquivalentEmailAndForeignTenantUpdatePreserveInvite()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.DisabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Same email",
            Email = "same@example.test", IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var token = await IssueInvite(client, studentId);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "Same email", email = "  SAME@Example.Test " })).StatusCode);
        factory.WithDb(db => Assert.NotNull(db.Students.Single(s => s.Id == studentId).AppInviteTokenHash));
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "Cross tenant", email = "attacker@example.test" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.Equal("same@example.test", student.Email);
            Assert.Equal(factory.EnabledGym, student.GymId);
        });
    }

    [Fact]
    public Task IndependentStudent_ActivationWinningConcurrentEmailEditRejectsStaleEdit_Sqlite() =>
        CheckActivationWinningConcurrentEmailEdit(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task IndependentStudent_ActivationWinningConcurrentEmailEditRejectsStaleEdit_PostgreSql(bool postgres) =>
        CheckActivationWinningConcurrentEmailEdit(postgres);

    private static async Task CheckActivationWinningConcurrentEmailEdit(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres) { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Race target",
            Email = "race-before@example.test", IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var token = await IssueInvite(client, studentId);
        var activationStatus = HttpStatusCode.BadRequest;
        factory.BeforeNextAsyncSave = async _ =>
        {
            using var publicClient = factory.CreateClient();
            var activation = await publicClient.PostAsJsonAsync("/api/students/app-activate",
                new { token, password = TestApi.Password });
            activationStatus = activation.StatusCode;
        };

        var edit = await client.PutAsJsonAsync($"/api/students/{studentId}",
            new { name = "Race target", email = "race-after@example.test" });
        Assert.Equal(HttpStatusCode.NoContent, activationStatus);
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.Equal("race-before@example.test", student.Email);
            Assert.NotNull(student.UserId);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Equal("race-before@example.test", db.Users.Single(u => u.Id == student.UserId).Email);
            Assert.False(db.Users.Any(u => u.Email == "race-after@example.test"));
        });
    }

    private static async Task<string> IssueInvite(HttpClient client, Guid studentId)
    {
        var response = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task IndependentStudent_AdminCanRevokeInviteWithoutChangingAdministrativeStudentOrEnrollment()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.DisabledGym, UserRole.Admin);
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        var studentId = Guid.NewGuid();
        var enrollmentId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        factory.WithDb(db =>
        {
            var plan = new Plan
            {
                Id = planId, GymId = factory.EnabledGym, Name = "Invite revoke plan",
                Price = 100, DurationMonths = 1, IsActive = true
            };
            db.Students.Add(new Student
            {
                Id = studentId, GymId = factory.EnabledGym, Name = "Pending invite",
                Email = "pending-revoke@example.test", IsActive = true, AppAccessEnabled = false
            });
            db.Enrollments.Add(new Enrollment
            {
                Id = enrollmentId, StudentId = studentId, Plan = plan, PlanName = plan.Name,
                PlanPrice = 100, PlanDurationMonths = 1, Status = EnrollmentStatus.Active,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                EndDate = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(1)
            });
        });

        using var client = factory.CreateClient();
        var token = await IssueInviteAsAdmin(client, factory, admin, studentId);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            $"/api/students/{studentId}/app-access", new { enabled = false })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(professor));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(
            $"/api/students/{studentId}/app-access", new { enabled = false })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(foreignAdmin));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(
            $"/api/students/{studentId}/app-access", new { enabled = false })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var revoked = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
            new { enabled = false });
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var result = await revoked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(result.GetProperty("enabled").GetBoolean());
        Assert.True(result.GetProperty("token").ValueKind == JsonValueKind.Null);
        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate", new { token, password = TestApi.Password })).StatusCode);

        factory.WithDb(db =>
        {
            var student = db.Students.Single(item => item.Id == studentId);
            Assert.False(student.AppAccessEnabled);
            Assert.Null(student.AppInviteTokenHash);
            Assert.Null(student.AppInviteExpiresAt);
            Assert.Null(student.UserId);
            Assert.True(student.IsActive);
            Assert.Null(student.ArchivedAt);
            var enrollment = db.Enrollments.Single(item => item.Id == enrollmentId);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            Assert.Equal(planId, enrollment.PlanId);
            Assert.Empty(db.Users.Where(user => user.Email == "pending-revoke@example.test"));
        });
    }

    [Fact]
    public async Task IndependentStudent_ActivationRejectsInviteWithMissingExpiration()
    {
        await using var factory = new TestApi { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Null expiry",
            Email = "null-expiry@example.test", IsActive = true
        }));
        using var client = factory.CreateClient();
        var token = await IssueInviteAsAdmin(client, factory, admin, studentId);
        factory.WithDb(db => db.Students.Single(item => item.Id == studentId).AppInviteExpiresAt = null);

        client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate/validate", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(
            "/api/students/app-activate", new { token, password = TestApi.Password })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(item => item.Id == studentId);
            Assert.Null(student.AppInviteExpiresAt);
            Assert.NotNull(student.AppInviteTokenHash);
            Assert.Null(student.UserId);
            Assert.Empty(db.Users.Where(user => user.Email == "null-expiry@example.test"));
        });
    }

    [Theory]
    [InlineData("activation-wins-revocation")]
    [InlineData("revocation-wins-activation")]
    [InlineData("reissue-wins-activation")]
    public Task IndependentStudent_InviteLifecycleRace_IsSafe_Sqlite(string race) =>
        CheckInviteLifecycleRace(race, false);

    [PostgresTheory]
    [InlineData("activation-wins-revocation")]
    [InlineData("revocation-wins-activation")]
    [InlineData("reissue-wins-activation")]
    public Task IndependentStudent_InviteLifecycleRace_IsSafe_PostgreSql(string race) =>
        CheckInviteLifecycleRace(race, true);

    private static async Task CheckInviteLifecycleRace(string race, bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres) { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Invite race",
            Email = $"{race}@example.test", IsActive = true
        }));
        using var adminClient = factory.CreateClient();
        var oldToken = await IssueInviteAsAdmin(adminClient, factory, admin, studentId);
        HttpStatusCode competingStatus = HttpStatusCode.BadRequest;
        string? replacementToken = null;

        Func<Task<HttpResponseMessage>> competingOperation = race switch
        {
            "activation-wins-revocation" => async () =>
            {
                using var publicClient = factory.CreateClient();
                var response = await publicClient.PostAsJsonAsync("/api/students/app-activate",
                    new { token = oldToken, password = TestApi.Password });
                competingStatus = response.StatusCode;
                return response;
            },
            "revocation-wins-activation" => async () =>
            {
                var response = await adminClient.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                    new { enabled = false });
                competingStatus = response.StatusCode;
                return response;
            },
            _ => async () =>
            {
                var response = await adminClient.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                    new { enabled = true });
                competingStatus = response.StatusCode;
                if (response.IsSuccessStatusCode)
                    replacementToken = (await response.Content.ReadFromJsonAsync<JsonElement>())
                        .GetProperty("token").GetString();
                return response;
            }
        };
        factory.BeforeNextAsyncSave = async _ =>
        {
            using var response = await competingOperation();
        };

        using var outerClient = factory.CreateClient();
        HttpResponseMessage outer;
        if (race == "activation-wins-revocation")
        {
            outerClient.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
            outer = await outerClient.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                new { enabled = false });
            Assert.Equal(HttpStatusCode.NoContent, competingStatus);
            Assert.Equal(HttpStatusCode.Conflict, outer.StatusCode);
            factory.WithDb(db =>
            {
                var student = db.Students.Single(item => item.Id == studentId);
                Assert.NotNull(student.UserId);
                Assert.True(student.AppAccessEnabled);
                Assert.Null(student.AppInviteTokenHash);
            });
        }
        else
        {
            outerClient.DefaultRequestHeaders.Authorization = null;
            outer = await outerClient.PostAsJsonAsync("/api/students/app-activate",
                new { token = oldToken, password = TestApi.Password });
            Assert.Equal(HttpStatusCode.OK, competingStatus);
            Assert.Equal(HttpStatusCode.Conflict, outer.StatusCode);
            var oldValidation = await outerClient.PostAsJsonAsync(
                "/api/students/app-activate/validate", new { token = oldToken });
            Assert.Equal(HttpStatusCode.BadRequest, oldValidation.StatusCode);
            factory.WithDb(db =>
            {
                var student = db.Students.Single(item => item.Id == studentId);
                Assert.Null(student.UserId);
                Assert.Equal(race == "reissue-wins-activation",
                    student.AppInviteTokenHash is not null);
                Assert.False(db.Users.Any(user => user.Email == $"{race}@example.test"));
            });
            if (race == "reissue-wins-activation")
            {
                Assert.NotNull(replacementToken);
                Assert.NotEqual(oldToken, replacementToken);
                Assert.Equal(HttpStatusCode.OK, (await outerClient.PostAsJsonAsync(
                    "/api/students/app-activate/validate", new { token = replacementToken })).StatusCode);
            }
        }
        outer.Dispose();
    }

    private static async Task<string> IssueInviteAsAdmin(HttpClient client, TestApi factory, User admin, Guid studentId)
    {
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        return await IssueInvite(client, studentId);
    }

    [Fact]
    public Task IndependentStudent_CrudInvitationAndTenantIsolation_Sqlite() =>
        CheckIndependentStudentAsync(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task IndependentStudent_CrudInvitationAndTenantIsolation_PostgreSql(bool postgres) =>
        CheckIndependentStudentAsync(postgres);

    [PostgresTheory]
    [InlineData(true)]
    public async Task IndependentStudent_ConcurrentInviteRedemption_CreatesOneUser_PostgreSql(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: true)
        { EnabledFlag = "false" };
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentId = Guid.NewGuid();
        const string email = "concurrent121@example.test";
        factory.WithDb(db => db.Students.Add(new Student
        {
            Id = studentId, GymId = factory.EnabledGym, Name = "Concurrent student",
            Email = email, IsActive = true
        }));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));
        var invite = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, invite.StatusCode);
        var token = JsonDocument.Parse(await invite.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = null;
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/students/app-activate",
                new { token, password = "synthetic-password-123" }),
            client.PostAsJsonAsync("/api/students/app-activate",
                new { token, password = "synthetic-password-123" }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.NoContent);
        Assert.Single(responses, r => r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict);
        factory.WithDb(db =>
        {
            Assert.Single(db.Users.Where(u => u.Email == email));
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.NotNull(student.UserId);
            Assert.Null(student.AppInviteTokenHash);
        });
    }

    private static async Task CheckIndependentStudentAsync(bool postgres)
    {
        await using var factory = new TestApi(postgres, migrateSchema: postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var foreignAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));

        var created = await client.PostAsJsonAsync("/api/students", new { name = "Aluno sem conta" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using (var unauthenticatedClient = factory.CreateClient())
        {
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await unauthenticatedClient.GetAsync("/api/students/me")).StatusCode);
        }
        Guid studentId = default;
        var chargeId = Guid.NewGuid();
        factory.WithDb(db =>
        {
            var student = db.Students.Single(x => x.Name == "Aluno sem conta");
            studentId = student.Id;
            Assert.Equal(factory.EnabledGym, student.GymId);
            Assert.Null(student.UserId);
            Assert.Null(student.Email);
            Assert.False(student.AppAccessEnabled);
            var plan = new Plan { Id = Guid.NewGuid(), GymId = factory.EnabledGym,
                Name = "Plano sintético", Price = 100, DurationMonths = 1, IsActive = true };
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            db.Enrollments.Add(new Enrollment { Id = Guid.NewGuid(), StudentId = student.Id,
                Plan = plan, PlanName = plan.Name, PlanPrice = 100, PlanDurationMonths = 1,
                Status = EnrollmentStatus.Active, StartDate = today,
                EndDate = today.AddMonths(1),
                Charge = new Charge { Id = chargeId, Amount = 100,
                    DueDate = today, Status = ChargeStatus.Pending } });
        });

        var administrativeStudent = await client.GetAsync($"/api/students/{studentId}");
        Assert.Equal(HttpStatusCode.OK, administrativeStudent.StatusCode);
        using (var studentJson = JsonDocument.Parse(await administrativeStudent.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, studentJson.RootElement.GetProperty("email").ValueKind);
            Assert.Equal(JsonValueKind.Null, studentJson.RootElement.GetProperty("userId").ValueKind);
        }
        var studentList = await client.GetAsync("/api/students");
        Assert.Equal(HttpStatusCode.OK, studentList.StatusCode);
        Assert.Contains("Aluno sem conta", await studentList.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/students/{studentId}",
                new { name = "Aluno sem conta atualizado", email = (string?)null })).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(x => x.Id == studentId);
            Assert.Equal("Aluno sem conta atualizado", student.Name);
            Assert.Null(student.Email);
            Assert.Null(student.UserId);
            Assert.False(student.AppAccessEnabled);
        });
        var professor = factory.AddUser(factory.EnabledGym, UserRole.Professor);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(professor));
        var professorStudent = await client.GetAsync($"/api/students/{studentId}");
        Assert.Equal(HttpStatusCode.OK, professorStudent.StatusCode);
        using (var professorJson = JsonDocument.Parse(await professorStudent.Content.ReadAsStringAsync()))
        {
            Assert.Equal(JsonValueKind.Null, professorJson.RootElement.GetProperty("email").ValueKind);
            Assert.Equal(JsonValueKind.Null, professorJson.RootElement.GetProperty("userId").ValueKind);
        }
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));

        var charges = await client.GetAsync("/api/charges");
        Assert.Equal(HttpStatusCode.OK, charges.StatusCode);
        Assert.Contains("Aluno sem conta", await charges.Content.ReadAsStringAsync());
        var report = await client.GetAsync("/api/reports/students");
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        Assert.Contains("Aluno sem conta", await report.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,
            (await client.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/api/physical-access/credentials", new
            {
                studentId, type = PhysicalAccessCredentialType.Card,
                providerKey = "synthetic", externalIdentifier = $"card-{studentId:N}"
            })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PatchAsJsonAsync($"/api/charges/{chargeId}/confirm-payment", new
            {
                paidAmount = 100, discountAmount = 0,
                paymentMethod = PaymentMethod.Pix, paidAt = DateTimeOffset.UtcNow
            })).StatusCode);
        factory.WithDb(db =>
        {
            Assert.Equal(ChargeStatus.Paid, db.Charges.Single(c => c.Id == chargeId).Status);
            var student = db.Students.Single(s => s.Id == studentId);
            Assert.Null(student.UserId);
            Assert.False(student.AppAccessEnabled);
        });

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(foreignAdmin));
        var foreignCharges = await client.GetAsync("/api/charges");
        Assert.Equal(HttpStatusCode.OK, foreignCharges.StatusCode);
        Assert.DoesNotContain("Aluno sem conta",
            await foreignCharges.Content.ReadAsStringAsync());
        Assert.DoesNotContain("Aluno sem conta",
            await (await client.GetAsync("/api/reports/students")).Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/students/{studentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                new { enabled = true })).StatusCode);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                new { enabled = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PutAsJsonAsync($"/api/students/{studentId}",
                new { name = "Aluno sem conta", email = "student121@example.test" })).StatusCode);
        var inviteResponse = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, inviteResponse.StatusCode);
        var invite = JsonDocument.Parse(await inviteResponse.Content.ReadAsStringAsync());
        var oldToken = invite.RootElement.GetProperty("token").GetString();
        Assert.NotNull(oldToken);
        client.DefaultRequestHeaders.Authorization = null;
        var validInvite = await client.PostAsJsonAsync("/api/students/app-activate/validate",
            new { token = oldToken });
        Assert.Equal(HttpStatusCode.OK, validInvite.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));
        factory.WithDb(db =>
        {
            var student = db.Students.Single(s => s.Id == studentId);
            student.AppInviteExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        });
        client.DefaultRequestHeaders.Authorization = null;
        var expiredInvite = await client.PostAsJsonAsync("/api/students/app-activate/validate",
            new { token = oldToken });
        Assert.Equal(HttpStatusCode.BadRequest, expiredInvite.StatusCode);
        Assert.Contains("expirou", await expiredInvite.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));
        var reissueResponse = await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, reissueResponse.StatusCode);
        var token = JsonDocument.Parse(await reissueResponse.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString();
        Assert.NotEqual(oldToken, token);

        client.DefaultRequestHeaders.Authorization = null;
        var revokedInvite = await client.PostAsJsonAsync("/api/students/app-activate/validate",
            new { token = oldToken });
        Assert.Equal(HttpStatusCode.BadRequest, revokedInvite.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/students/app-activate",
                new { token = oldToken, password = "synthetic-password-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/students/app-activate/validate",
                new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/students/app-activate",
                new { token, password = "synthetic-password-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/students/app-activate",
                new { token, password = "synthetic-password-123" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/students/app-activate/validate",
                new { token })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "student121@example.test", password = "synthetic-password-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var jwt = JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", factory.Token(admin));
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync($"/api/students/{studentId}/app-access",
                new { enabled = false })).StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/students/me")).StatusCode);
        factory.WithDb(db =>
        {
            var student = db.Students.Single(x => x.Id == studentId);
            Assert.False(student.AppAccessEnabled);
            Assert.NotNull(student.UserId);
            Assert.Equal(factory.EnabledGym, student.GymId);
        });
    }
}
