using System.Data.Common;
using GymFlow.Application.Interfaces.Security;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Security;

// Each reservation is one atomic database statement. A pending reservation
// occupies a slot until completion or the one-minute window expires.
public sealed class PersistentPhysicalAccessPinAttemptLimiter : IPhysicalAccessPinAttemptLimiter
{
    private const long WindowMilliseconds = 60_000;
    private const long LockMilliseconds = 300_000;
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public PersistentPhysicalAccessPinAttemptLimiter(AppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PhysicalAccessPinReservation?> TryReserveAsync(Guid gymId)
    {
        if (gymId == Guid.Empty)
            return null;

        await _db.Database.OpenConnectionAsync();
        try
        {
            var now = await GetNowAsync();
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                INSERT INTO "PhysicalAccessPinAttemptStates"
                    ("GymId", "WindowStart", "Failures", "Pending", "LockedUntil")
                VALUES (@gymId, @now, 0, 1, 0)
                ON CONFLICT ("GymId") DO UPDATE SET
                    "WindowStart" = CASE WHEN "PhysicalAccessPinAttemptStates"."WindowStart" <= @cutoff
                        THEN @now ELSE "PhysicalAccessPinAttemptStates"."WindowStart" END,
                    "Failures" = CASE WHEN "PhysicalAccessPinAttemptStates"."WindowStart" <= @cutoff
                        THEN 0 ELSE "PhysicalAccessPinAttemptStates"."Failures" END,
                    "Pending" = CASE WHEN "PhysicalAccessPinAttemptStates"."WindowStart" <= @cutoff
                        THEN 1 ELSE "PhysicalAccessPinAttemptStates"."Pending" + 1 END,
                    "LockedUntil" = 0
                WHERE "PhysicalAccessPinAttemptStates"."LockedUntil" <= @now
                  AND ("PhysicalAccessPinAttemptStates"."WindowStart" <= @cutoff
                    OR "PhysicalAccessPinAttemptStates"."Failures" + "PhysicalAccessPinAttemptStates"."Pending" < 5)
                RETURNING "WindowStart";
                """;
            Add(command, "@gymId", gymId);
            Add(command, "@now", now);
            Add(command, "@cutoff", now - WindowMilliseconds);
            var result = await command.ExecuteScalarAsync();
            return result is null || result is DBNull
                ? null
                : new PhysicalAccessPinReservation(gymId, Convert.ToInt64(result));
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    public async Task CompleteAsync(PhysicalAccessPinReservation reservation, bool failed)
    {
        await _db.Database.OpenConnectionAsync();
        try
        {
            var now = await GetNowAsync();
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                UPDATE "PhysicalAccessPinAttemptStates" SET
                    "Pending" = "Pending" - 1,
                    "Failures" = "Failures" + @failed,
                    "LockedUntil" = CASE
                        WHEN @failed = 1 AND "Failures" + 1 >= 5
                            THEN @lockUntil
                        ELSE "LockedUntil" END
                WHERE "GymId" = @gymId AND "WindowStart" = @windowStart
                  AND "Pending" > 0;
                """;
            Add(command, "@gymId", reservation.GymId);
            Add(command, "@windowStart", reservation.WindowStart);
            Add(command, "@failed", failed ? 1 : 0);
            Add(command, "@lockUntil", now + LockMilliseconds);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("A reserva de PIN expirou; a decisão foi negada.");
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }

    private async Task<long> GetNowAsync()
    {
        // API replicas use one authoritative PostgreSQL clock. The injected
        // clock is retained for isolated SQLite tests.
        if (_db.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) != true)
            return _clock.GetUtcNow().ToUnixTimeMilliseconds();

        await using var command = _db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT (EXTRACT(EPOCH FROM clock_timestamp()) * 1000)::bigint";
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static void Add(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
