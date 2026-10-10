namespace GymFlow.AccessAgent.Services;

public sealed class InMemoryAccessOperationalHealthStore :
    IAccessOperationalHealthStore
{
    private readonly object _gate = new();

    private DateTime? _lastOfflineSyncAt;
    private DateTime? _lastFailureAt;
    private string? _lastFailureCode;

    public AccessOperationalHealthSnapshot
        GetSnapshot()
    {
        lock (_gate)
        {
            return new AccessOperationalHealthSnapshot(
                _lastOfflineSyncAt,
                _lastFailureAt,
                _lastFailureCode);
        }
    }

    public void RecordOfflineSync(
        DateTime occurredAtUtc)
    {
        var normalized =
            occurredAtUtc.ToUniversalTime();

        lock (_gate)
        {
            if (!_lastOfflineSyncAt.HasValue ||
                normalized >
                    _lastOfflineSyncAt.Value)
            {
                _lastOfflineSyncAt =
                    normalized;
            }
        }
    }

    public void RecordFailure(
        string code,
        DateTime occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException(
                "O código da falha é obrigatório.",
                nameof(code));
        }

        var normalized =
            occurredAtUtc.ToUniversalTime();

        lock (_gate)
        {
            if (_lastFailureAt.HasValue &&
                normalized <=
                    _lastFailureAt.Value)
            {
                return;
            }

            _lastFailureAt =
                normalized;

            _lastFailureCode =
                code.Trim();
        }
    }
}
