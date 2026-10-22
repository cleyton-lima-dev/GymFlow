using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Theory]
    [InlineData("plan-list")]
    [InlineData("plan-create")]
    [InlineData("plan-update")]
    [InlineData("plan-status")]
    [InlineData("enrollment-list")]
    [InlineData("enrollment-create")]
    [InlineData("enrollment-cancel")]
    [InlineData("enrollment-renew")]
    [InlineData("charge-list")]
    [InlineData("charge-payment")]
    public Task G6Financial_Endpoints_RequireAdminAndExplicitTenantEntitlement(string operation) =>
        CheckG6FinancialEndpointAuthorization(operation, false);

    [PostgresTheory]
    [InlineData("plan-list")]
    [InlineData("plan-create")]
    [InlineData("plan-update")]
    [InlineData("plan-status")]
    [InlineData("enrollment-list")]
    [InlineData("enrollment-create")]
    [InlineData("enrollment-cancel")]
    [InlineData("enrollment-renew")]
    [InlineData("charge-list")]
    [InlineData("charge-payment")]
    public Task G6Financial_PostgreSql_Endpoints_RequireAdminAndExplicitTenantEntitlement(string operation) =>
        CheckG6FinancialEndpointAuthorization(operation, true);

    private static async Task CheckG6FinancialEndpointAuthorization(string operation, bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var seeds = new Dictionary<Guid, G6FinancialSeed>();
        foreach (var gym in new[] { factory.EnabledGym, factory.DisabledGym, factory.LegacyGym })
            seeds[gym] = SeedG6FinancialRecords(factory, gym);

        var principals = new List<(User User, HttpStatusCode Expected)>();
        foreach (var gym in new[] { factory.DisabledGym, factory.LegacyGym, factory.EnabledGym })
        {
            foreach (var role in new[] { UserRole.Admin, UserRole.Professor, UserRole.Student })
            {
                // Successful Admin operations are covered separately with real state assertions.
                if (gym == factory.EnabledGym && role == UserRole.Admin) continue;
                var user = factory.AddUser(gym, role);
                if (role == UserRole.Student && gym == factory.EnabledGym)
                    factory.AddEnrollment(user, gym, EnrollmentStatus.Active, -1, 1);
                principals.Add((user, HttpStatusCode.Forbidden));
            }
        }

        var before = G6FinancialState(factory);
        using var client = factory.CreateClient();
        using (var anonymous = await SendG6FinancialRequest(client, operation, seeds[factory.EnabledGym]))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        foreach (var (user, expected) in principals)
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(user));
            using var response = await SendG6FinancialRequest(client, operation, seeds[user.GymId]);
            Assert.Equal(expected, response.StatusCode);
        }

        Assert.Equal(before, G6FinancialState(factory));
    }

    [Fact]
    public Task G6Financial_EnabledAdmin_CanManageOwnPlansEnrollmentsAndPayments() =>
        CheckG6FinancialEnabledAdmin(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task G6Financial_PostgreSql_EnabledAdmin_CanManageOwnPlansEnrollmentsAndPayments(bool postgres) =>
        CheckG6FinancialEnabledAdmin(postgres);

    private static async Task CheckG6FinancialEnabledAdmin(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentUser = factory.AddUser(factory.EnabledGym);
        Guid studentId = default;
        factory.WithDb(db => studentId = db.Students.Single(s => s.UserId == studentUser.Id).Id);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        using (var create = await client.PostAsJsonAsync("/api/plans", G6FinancialPlanBody("G6 own plan")))
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var plans = await client.GetFromJsonAsync<JsonElement>("/api/plans");
        var planId = Assert.Single(plans.EnumerateArray()).GetProperty("id").GetGuid();
        using (var update = await client.PutAsJsonAsync($"/api/plans/{planId}", G6FinancialPlanBody("G6 updated plan")))
            Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        using (var status = await client.PatchAsJsonAsync($"/api/plans/{planId}/status", new { isActive = false }))
            Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/plans?isActive=true")).EnumerateArray());
        using (var status = await client.PatchAsJsonAsync($"/api/plans/{planId}/status", new { isActive = true }))
            Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Guid createdEnrollmentId;
        using (var create = await client.PostAsJsonAsync("/api/enrollments", new
        {
            studentId, planId, startDate = today, payment = G6FinancialPaymentBody()
        }))
        {
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            createdEnrollmentId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var enrollments = await client.GetFromJsonAsync<JsonElement>("/api/enrollments");
        Assert.Equal(createdEnrollmentId, Assert.Single(enrollments.EnumerateArray()).GetProperty("id").GetGuid());
        Guid renewedEnrollmentId;
        using (var renew = await client.PostAsJsonAsync($"/api/enrollments/{createdEnrollmentId}/renew", new { planId }))
        {
            Assert.Equal(HttpStatusCode.Created, renew.StatusCode);
            renewedEnrollmentId = (await renew.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var charges = await client.GetFromJsonAsync<JsonElement>("/api/charges");
        Assert.Equal(2, charges.GetArrayLength());
        var renewedChargeId = charges.EnumerateArray().Single(c =>
            c.GetProperty("enrollmentId").GetGuid() == renewedEnrollmentId).GetProperty("id").GetGuid();
        using (var payment = await client.PatchAsJsonAsync($"/api/charges/{renewedChargeId}/confirm-payment", G6FinancialPaymentBody()))
            Assert.Equal(HttpStatusCode.NoContent, payment.StatusCode);
        using (var duplicatePayment = await client.PatchAsJsonAsync($"/api/charges/{renewedChargeId}/confirm-payment", G6FinancialPaymentBody()))
            Assert.Equal(HttpStatusCode.Conflict, duplicatePayment.StatusCode);
        using (var cancel = await client.PatchAsync($"/api/enrollments/{renewedEnrollmentId}/cancel", null))
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        factory.WithDb(db =>
        {
            var plan = Assert.Single(db.Plans);
            Assert.Equal(factory.EnabledGym, plan.GymId);
            Assert.Equal("G6 updated plan", plan.Name);
            Assert.Equal(2, db.Enrollments.Count());
            Assert.All(db.Enrollments, e => Assert.Equal(studentId, e.StudentId));
            Assert.Equal(EnrollmentStatus.Active, db.Enrollments.Single(e => e.Id == createdEnrollmentId).Status);
            Assert.Equal(EnrollmentStatus.Cancelled, db.Enrollments.Single(e => e.Id == renewedEnrollmentId).Status);
            Assert.All(db.Charges, c =>
            {
                Assert.Equal(ChargeStatus.Paid, c.Status);
                Assert.Equal(100m, c.PaidAmount);
                Assert.Equal(PaymentMethod.Pix, c.PaymentMethod);
            });
            Assert.Equal(2, db.FinancialAuditLogs.Count());
            Assert.All(db.FinancialAuditLogs, audit =>
            {
                Assert.Equal(factory.EnabledGym, audit.GymId);
                Assert.Equal(admin.Id, audit.ActorUserId);
                Assert.Equal(FinancialAuditAction.PaymentConfirmed, audit.Action);
            });
        });
    }

    [Fact]
    public Task G6Financial_EnabledAdmins_CannotReadOrMutateOtherTenantOrMixedOwnershipRecords() =>
        CheckG6FinancialTenantIsolation(false);

    [PostgresTheory]
    [InlineData(true)]
    public Task G6Financial_PostgreSql_EnabledAdmins_CannotReadOrMutateOtherTenantOrMixedOwnershipRecords(bool postgres) =>
        CheckG6FinancialTenantIsolation(postgres);

    private static async Task CheckG6FinancialTenantIsolation(bool postgres)
    {
        await using var factory = new TestApi(postgres);
        var ownAdmin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var otherAdmin = factory.AddUser(factory.OtherEnabledGym, UserRole.Admin);
        var own = SeedG6FinancialRecords(factory, factory.EnabledGym);
        var foreign = SeedG6FinancialRecords(factory, factory.OtherEnabledGym);
        var mixedOwnStudent = SeedG6FinancialMixedEnrollment(factory, own.StudentId, foreign.PlanId);
        var mixedForeignStudent = SeedG6FinancialMixedEnrollment(factory, foreign.StudentId, own.PlanId);
        var before = G6FinancialState(factory);
        using var client = factory.CreateClient();

        foreach (var (admin, current, other) in new[]
        {
            (ownAdmin, own, foreign), (otherAdmin, foreign, own)
        })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
            foreach (var (route, expectedId) in new[]
            {
                ("/api/plans", current.PlanId), ("/api/enrollments", current.EnrollmentId), ("/api/charges", current.ChargeId)
            })
            {
                var payload = await client.GetFromJsonAsync<JsonElement>(route);
                Assert.Equal(expectedId, Assert.Single(payload.EnumerateArray()).GetProperty("id").GetGuid());
            }

            using (var update = await client.PutAsJsonAsync($"/api/plans/{other.PlanId}", G6FinancialPlanBody("Foreign overwrite")))
                Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            using (var status = await client.PatchAsJsonAsync($"/api/plans/{other.PlanId}/status", new { isActive = false }))
                Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);

            foreach (var (studentId, planId) in new[]
            {
                (other.StudentId, current.PlanId), (current.StudentId, other.PlanId), (other.StudentId, other.PlanId)
            })
            {
                // A future, non-overlapping start ensures ownership is the rejection reason.
                using var create = await client.PostAsJsonAsync("/api/enrollments", new
                {
                    studentId, planId, startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2),
                    payment = G6FinancialPaymentBody()
                });
                Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
            }

            using (var wrongPlan = await client.PostAsJsonAsync($"/api/enrollments/{current.EnrollmentId}/renew", new { planId = other.PlanId }))
                Assert.Equal(HttpStatusCode.Conflict, wrongPlan.StatusCode);

            foreach (var invalid in new[] { other, mixedOwnStudent, mixedForeignStudent })
            {
                using (var cancel = await client.PatchAsync($"/api/enrollments/{invalid.EnrollmentId}/cancel", null))
                    Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
                using (var renew = await client.PostAsJsonAsync($"/api/enrollments/{invalid.EnrollmentId}/renew", new { planId = current.PlanId }))
                    Assert.Equal(HttpStatusCode.Conflict, renew.StatusCode);
                using (var payment = await client.PatchAsJsonAsync($"/api/charges/{invalid.ChargeId}/confirm-payment", G6FinancialPaymentBody()))
                    Assert.Equal(HttpStatusCode.Conflict, payment.StatusCode);
            }
        }
        Assert.Equal(before, G6FinancialState(factory));
    }

    private static async Task<HttpResponseMessage> SendG6FinancialRequest(
        HttpClient client, string operation, G6FinancialSeed seed) => operation switch
    {
        "plan-list" => await client.GetAsync("/api/plans"),
        "plan-create" => await client.PostAsJsonAsync("/api/plans", G6FinancialPlanBody("G6 denied creation")),
        "plan-update" => await client.PutAsJsonAsync($"/api/plans/{seed.PlanId}", G6FinancialPlanBody("G6 denied update")),
        "plan-status" => await client.PatchAsJsonAsync($"/api/plans/{seed.PlanId}/status", new { isActive = false }),
        "enrollment-list" => await client.GetAsync("/api/enrollments"),
        "enrollment-create" => await client.PostAsJsonAsync("/api/enrollments", new
        {
            studentId = seed.StudentId, planId = seed.PlanId,
            startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(2), payment = G6FinancialPaymentBody()
        }),
        "enrollment-cancel" => await client.PatchAsync($"/api/enrollments/{seed.EnrollmentId}/cancel", null),
        "enrollment-renew" => await client.PostAsJsonAsync($"/api/enrollments/{seed.EnrollmentId}/renew", new { planId = seed.PlanId }),
        "charge-list" => await client.GetAsync("/api/charges"),
        "charge-payment" => await client.PatchAsJsonAsync($"/api/charges/{seed.ChargeId}/confirm-payment", G6FinancialPaymentBody()),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown financial operation.")
    };

    private static object G6FinancialPlanBody(string name) => new
    {
        name, price = 100m, durationMonths = 1, billingCycle = PlanBillingCycle.Monthly
    };

    private static object G6FinancialPaymentBody() => new
    {
        paidAmount = 100m, discountAmount = 0m, paymentMethod = PaymentMethod.Pix, paidAt = DateTimeOffset.UtcNow
    };

    private static G6FinancialSeed SeedG6FinancialRecords(TestApi factory, Guid gymId)
    {
        var student = factory.AddUser(gymId);
        Guid studentId = default;
        var planId = Guid.NewGuid();
        factory.WithDb(db =>
        {
            studentId = db.Students.Single(s => s.UserId == student.Id).Id;
            db.Plans.Add(new Plan
            {
                Id = planId, GymId = gymId, Name = $"G6 plan {planId:N}", Price = 100m,
                DurationMonths = 1, BillingCycle = PlanBillingCycle.Monthly, IsActive = true
            });
        });
        return SeedG6FinancialMixedEnrollment(factory, studentId, planId);
    }

    private static G6FinancialSeed SeedG6FinancialMixedEnrollment(TestApi factory, Guid studentId, Guid planId)
    {
        var enrollmentId = Guid.NewGuid();
        var chargeId = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        factory.WithDb(db => db.Enrollments.Add(new Enrollment
        {
            Id = enrollmentId, StudentId = studentId, PlanId = planId,
            // Active records make renewal start after EndDate, so an ownership regression
            // cannot be masked by the overlap rejection of a pending enrollment.
            StartDate = today, EndDate = today.AddMonths(1), Status = EnrollmentStatus.Active,
            PlanName = "G6 snapshot", PlanPrice = 100m, PlanDurationMonths = 1, PlanBillingCycle = PlanBillingCycle.Monthly,
            Charge = new Charge
            {
                Id = chargeId, Amount = 100m, DueDate = today, Status = ChargeStatus.Pending
            }
        }));
        return new(studentId, planId, enrollmentId, chargeId);
    }

    private static string G6FinancialState(TestApi factory)
    {
        string snapshot = null!;
        factory.WithDb(db => snapshot = JsonSerializer.Serialize(new
        {
            Plans = db.Plans.OrderBy(p => p.Id).Select(p => new
            {
                p.Id, p.GymId, p.Name, p.Price, p.DurationMonths, p.BillingCycle, p.IsActive, p.UpdatedAt
            }).ToArray(),
            Enrollments = db.Enrollments.OrderBy(e => e.Id).Select(e => new
            {
                e.Id, e.StudentId, e.PlanId, e.Status, e.StartDate, e.EndDate, e.CancellationDate, e.UpdatedAt
            }).ToArray(),
            Charges = db.Charges.OrderBy(c => c.Id).Select(c => new
            {
                c.Id, c.EnrollmentId, c.Status, c.Amount, c.DiscountAmount, c.PaidAmount, c.PaidAt,
                c.PaymentMethod, c.PaymentOrigin, c.VerificationStatus, c.UpdatedAt
            }).ToArray(),
            Students = db.Students.OrderBy(s => s.Id).Select(s => new
            {
                s.Id, s.IsActive, s.ArchivedAt, s.InactivationReason, s.InactivatedAt, s.NoValidEnrollmentSince, s.UpdatedAt
            }).ToArray(),
            Users = db.Users.OrderBy(u => u.Id).Select(u => new
            {
                u.Id, u.GymId, u.Role, u.IsActive
            }).ToArray(),
            Audits = db.FinancialAuditLogs.OrderBy(a => a.Id).Select(a => new
            {
                a.Id, a.GymId, a.ActorUserId, a.EntityId, a.Action, a.PreviousValues, a.NewValues
            }).ToArray()
        }));
        return snapshot;
    }

    private sealed record G6FinancialSeed(Guid StudentId, Guid PlanId, Guid EnrollmentId, Guid ChargeId);
}
