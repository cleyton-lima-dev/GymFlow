using System.Net;
using GymFlow.Domain.Entities;
using GymFlow.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.IntegrationTests.Security;

public partial class TenantEnrollmentAccessTests
{
    [CancellationPostgresFact]
    public async Task PostgreSql_ConcurrentCancellation_UsesOnePersistedVersionAndWritesOneAudit()
    {
        await using var factory = new TestApi(usePostgres: true, cancellationPostgres: true);
        var admin = factory.AddUser(factory.EnabledGym, UserRole.Admin);
        var studentUser = factory.AddUser(factory.EnabledGym);
        var enrollmentId = factory.AddEnrollment(studentUser, factory.EnabledGym, EnrollmentStatus.Active, 0, 30);
        var originalInput = DateTime.UtcNow.AddYears(1).AddTicks(7);

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.Single(item => item.Id == enrollmentId);
            enrollment.UpdatedAt = originalInput;
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

        DateTime? originalPersistedVersion = null;
        factory.WithDb(db =>
        {
            originalPersistedVersion = db.Enrollments.AsNoTracking()
                .Single(item => item.Id == enrollmentId).UpdatedAt;
        });
        Assert.NotNull(originalPersistedVersion);
        Assert.Equal(0, originalPersistedVersion!.Value.Ticks % 10);

        var barrier = new TransactionStartBarrier(2);
        factory.BeforeEveryTransaction = _ => barrier.WaitAsync();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token(admin));
        var responses = await Task.WhenAll(
            client.PatchAsync($"/api/enrollments/{enrollmentId}/cancel", null),
            client.PatchAsync($"/api/enrollments/{enrollmentId}/cancel", null));

        try
        {
            Assert.Equal(2, barrier.Arrivals);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }

        factory.WithDb(db =>
        {
            var enrollment = db.Enrollments.AsNoTracking()
                .Include(item => item.Charge)
                .Single(item => item.Id == enrollmentId);
            Assert.Equal(EnrollmentStatus.Cancelled, enrollment.Status);
            Assert.Equal(originalPersistedVersion.Value.AddTicks(10), enrollment.UpdatedAt);
            Assert.Equal(0, enrollment.UpdatedAt!.Value.Ticks % 10);
            Assert.Equal(ChargeStatus.Paid, enrollment.Charge!.Status);
            Assert.Equal(100m, enrollment.Charge.PaidAmount);

            var audit = Assert.Single(db.FinancialAuditLogs.AsNoTracking()
                .Where(item => item.Action == FinancialAuditAction.Cancel));
            Assert.Equal(factory.EnabledGym, audit.GymId);
            Assert.Equal(admin.Id, audit.ActorUserId);
            Assert.Equal(nameof(Enrollment), audit.EntityType);
            Assert.Equal(enrollmentId, audit.EntityId);
        });
    }

    private sealed class TransactionStartBarrier(int participants)
    {
        private readonly TaskCompletionSource _released =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public int Arrivals => Volatile.Read(ref _arrivals);

        public async Task WaitAsync()
        {
            if (Interlocked.Increment(ref _arrivals) == participants)
                _released.TrySetResult();

            await _released.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
    }
}

public sealed class CancellationPostgresFactAttribute : FactAttribute
{
    public CancellationPostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_CANCELLATION_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GYMFLOW_CANCELLATION_POSTGRES_DATA_DIRECTORY")))
        {
            Skip = "The explicitly isolated PostgreSQL cancellation cluster is not configured.";
        }
    }
}
