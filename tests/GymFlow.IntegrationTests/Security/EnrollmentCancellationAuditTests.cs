using System.Net;
using System.Text.Json;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [Fact]
    public async Task EnrollmentCancellation_RecordsOneTenantScopedAuditAndKeepsPaidCharge()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var ownEnrollmentId = SeedPaidEnrollment(factory, factory.EnabledGym);
        var foreignEnrollmentId = SeedPaidEnrollment(factory, factory.OtherEnabledGym);
        var transactionObserved = false;
        factory.AfterNextAsyncSave = db =>
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            Assert.Single(db.FinancialAuditLogs.AsNoTracking());
            transactionObserved = true;
        };

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));

        using (var cancel = await client.PatchAsync($"/api/enrollments/{ownEnrollmentId}/cancel", null))
            Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        Assert.True(transactionObserved);

        // Repeated cancellation is rejected and must not add another event.
        using (var retry = await client.PatchAsync($"/api/enrollments/{ownEnrollmentId}/cancel", null))
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);

        // A valid Admin token from Gym A cannot cancel a Gym B enrollment.
        using (var crossTenant = await client.PatchAsync($"/api/enrollments/{foreignEnrollmentId}/cancel", null))
            Assert.Equal(HttpStatusCode.Conflict, crossTenant.StatusCode);

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge)
                .Single(item => item.Id == ownEnrollmentId);
            Assert.Equal(EnrollmentStatus.Cancelled, enrollment.Status);
            Assert.NotNull(enrollment.CancellationDate);
            Assert.Equal(ChargeStatus.Paid, enrollment.Charge!.Status);
            Assert.Equal(100m, enrollment.Charge.PaidAmount);

            var foreignEnrollment = db.Enrollments.Single(item => item.Id == foreignEnrollmentId);
            Assert.Equal(EnrollmentStatus.Active, foreignEnrollment.Status);

            var audit = Assert.Single(db.FinancialAuditLogs.AsNoTracking());
            Assert.Equal(factory.EnabledGym, audit.GymId);
            Assert.Equal(admin.Id, audit.ActorUserId);
            Assert.Equal(nameof(Enrollment), audit.EntityType);
            Assert.Equal(ownEnrollmentId, audit.EntityId);
            Assert.Equal(FinancialAuditAction.Cancel, audit.Action);
            Assert.NotEqual(default, audit.OccurredAt);

            using var previous = JsonDocument.Parse(audit.PreviousValues);
            using var current = JsonDocument.Parse(audit.NewValues);
            Assert.Equal("Active", previous.RootElement.GetProperty("Status").GetString());
            Assert.Null(previous.RootElement.GetProperty("CancellationDate").GetString());
            Assert.Equal("Cancelled", current.RootElement.GetProperty("Status").GetString());
            Assert.Equal(enrollment.CancellationDate!.Value.ToString("yyyy-MM-dd"),
                current.RootElement.GetProperty("CancellationDate").GetString());
        });
    }

    [Fact]
    public async Task EnrollmentCancellation_AuditFailureRollsBackEnrollmentChange()
    {
        await using var factory = new TestApi();
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var enrollmentId = SeedPaidEnrollment(factory, factory.EnabledGym);
        factory.AfterNextAsyncSave = db =>
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            Assert.Single(db.FinancialAuditLogs.AsNoTracking());
            throw new InvalidOperationException("synthetic-audit-commit-failure");
        };

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        using var response = await client.PatchAsync($"/api/enrollments/{enrollmentId}/cancel", null);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("synthetic-audit-commit-failure", await response.Content.ReadAsStringAsync());
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Include(item => item.Charge)
                .Single(item => item.Id == enrollmentId);
            Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
            Assert.Null(enrollment.CancellationDate);
            Assert.Equal(ChargeStatus.Paid, enrollment.Charge!.Status);
            Assert.Empty(db.FinancialAuditLogs);
        });
    }

    private static Guid SeedPaidEnrollment(TestApi factory, Guid gymId)
    {
        var user = factory.AddUser(gymId);
        var enrollmentId = factory.AddEnrollment(user, gymId, EnrollmentStatus.Active, 0, 30);
        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Single(item => item.Id == enrollmentId);
            db.Charges.Add(new Charge
            {
                Id = Guid.NewGuid(),
                EnrollmentId = enrollmentId,
                Amount = 100m,
                PaidAmount = 100m,
                PaymentMethod = PaymentMethod.Cash,
                DueDate = enrollment.StartDate,
                Status = ChargeStatus.Paid,
                PaidAt = DateTime.UtcNow,
                Enrollment = enrollment
            });
        });
        return enrollmentId;
    }
}
