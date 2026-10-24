using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace GymFlow.AccessAgent.Offline;

public sealed class SqlitePinAttemptLimiter : IPinAttemptLimiter
{
    private readonly AccessOfflineStoreOptions _options;
    private readonly TimeProvider _clock;

    public SqlitePinAttemptLimiter(
        IOptions<AccessOfflineStoreOptions> options,
        TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public async Task<bool> IsLockedAsync(
        Guid gymId,
        CancellationToken cancellationToken)
    {
        if (gymId == Guid.Empty)
            return true;

        await using var connection = await OpenInitializedConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT LockedUntil FROM PinAttemptState WHERE GymId = $gymId";
        command.Parameters.AddWithValue("$gymId", gymId.ToString("N"));
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string lockedUntil &&
            DateTimeOffset.Parse(lockedUntil, CultureInfo.InvariantCulture) > _clock.GetUtcNow();
    }

    public async Task<bool> TryConsumeAsync(
        Guid gymId,
        CancellationToken cancellationToken)
    {
        if (gymId == Guid.Empty)
            return false;

        await using var connection = await OpenInitializedConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var now = _clock.GetUtcNow();
        DateTimeOffset windowStart = now;
        DateTimeOffset? lockedUntil = null;
        var attempts = 0;

        await using (var read = connection.CreateCommand())
        {
            read.Transaction = (SqliteTransaction)transaction;
            read.CommandText = "SELECT WindowStart, Attempts, LockedUntil FROM PinAttemptState WHERE GymId = $gymId";
            read.Parameters.AddWithValue("$gymId", gymId.ToString("N"));
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                windowStart = DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
                attempts = reader.GetInt32(1);
                if (!reader.IsDBNull(2))
                    lockedUntil = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture);
            }
        }

        if (lockedUntil > now)
            return false;

        if (now - windowStart >= TimeSpan.FromMinutes(1))
        {
            windowStart = now;
            attempts = 0;
        }

        attempts++;
        if (attempts > 5)
            lockedUntil = now.AddMinutes(5);
        else
            lockedUntil = null;

        await using var write = connection.CreateCommand();
        write.Transaction = (SqliteTransaction)transaction;
        write.CommandText = """
            INSERT INTO PinAttemptState (GymId, WindowStart, Attempts, LockedUntil)
            VALUES ($gymId, $windowStart, $attempts, $lockedUntil)
            ON CONFLICT (GymId) DO UPDATE SET
                WindowStart = excluded.WindowStart,
                Attempts = excluded.Attempts,
                LockedUntil = excluded.LockedUntil;
            """;
        write.Parameters.AddWithValue("$gymId", gymId.ToString("N"));
        write.Parameters.AddWithValue("$windowStart", windowStart.ToString("O", CultureInfo.InvariantCulture));
        write.Parameters.AddWithValue("$attempts", attempts);
        write.Parameters.AddWithValue("$lockedUntil", lockedUntil?.ToString("O", CultureInfo.InvariantCulture) ?? (object)DBNull.Value);
        await write.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return attempts <= 5;
    }

    private async Task<SqliteConnection> OpenInitializedConnectionAsync(
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);

        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = """
                PRAGMA busy_timeout = 5000;
                CREATE TABLE IF NOT EXISTS PinAttemptState
                (
                    GymId TEXT NOT NULL PRIMARY KEY,
                    WindowStart TEXT NOT NULL,
                    Attempts INTEGER NOT NULL,
                    LockedUntil TEXT NULL
                );
                """;
            await setup.ExecuteNonQueryAsync(cancellationToken);
        }
        return connection;
    }
}
