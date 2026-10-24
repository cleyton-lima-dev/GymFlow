namespace GymFlow.AccessAgent.Offline;

/// <summary>
/// Correlates one authorization with one live passage callback from the same
/// configured device. The hardware callback itself does not contain a request
/// or student identifier, so concurrent outstanding releases are rejected.
/// </summary>
internal sealed class PhysicalPassageConfirmationCoordinator
{
    private readonly object _sync = new();
    private readonly IPhysicalPassageOfflineStore _passageStore;
    private readonly TimeProvider _clock;
    private readonly bool _enabled;
    private Authorization? _authorization;

    public PhysicalPassageConfirmationCoordinator(
        IPhysicalPassageOfflineStore passageStore,
        TimeProvider clock,
        bool enabled)
    {
        _passageStore = passageStore;
        _clock = clock;
        _enabled = enabled;
    }

    public bool TryArm(
        object deviceInstance,
        string deviceKey,
        Guid requestId,
        DateTime authorizedAt)
    {
        if (!_enabled ||
            deviceInstance is null ||
            string.IsNullOrWhiteSpace(deviceKey) ||
            requestId == Guid.Empty)
            return false;

        lock (_sync)
        {
            if (_authorization is not null)
                return false;

            _authorization = new Authorization(
                deviceInstance,
                deviceKey.Trim(),
                requestId,
                authorizedAt.ToUniversalTime());
            return true;
        }
    }

    public void Cancel(Guid requestId)
    {
        lock (_sync)
        {
            if (_authorization?.RequestId == requestId)
                _authorization = null;
        }
    }

    public void HandleReleaseTimeout(object deviceInstance)
    {
        ClearForDevice(deviceInstance);
    }

    public void ClearForDevice(object deviceInstance)
    {
        lock (_sync)
        {
            if (_authorization is not null &&
                ReferenceEquals(_authorization.DeviceInstance, deviceInstance))
                _authorization = null;
        }
    }

    public void Clear()
    {
        lock (_sync)
            _authorization = null;
    }

    public async Task<bool> ConfirmAsync(
        object deviceInstance,
        string deviceKey,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        Authorization authorization;
        var normalizedOccurrence = occurredAt.ToUniversalTime();

        lock (_sync)
        {
            if (!_enabled ||
                _authorization is null ||
                !ReferenceEquals(_authorization.DeviceInstance, deviceInstance) ||
                !string.Equals(_authorization.DeviceKey, deviceKey?.Trim(), StringComparison.Ordinal) ||
                normalizedOccurrence < _authorization.AuthorizedAt)
                return false;

            authorization = _authorization;

            // Consume before the asynchronous write so duplicate callbacks
            // cannot enqueue more than one confirmation for this authorization.
            _authorization = null;
        }

        var passageTime = normalizedOccurrence == default
            ? _clock.GetUtcNow().UtcDateTime
            : normalizedOccurrence;

        await _passageStore.EnqueueAsync(
            new PendingPhysicalPassage(
                authorization.RequestId,
                passageTime,
                _clock.GetUtcNow().UtcDateTime,
                null),
            cancellationToken);

        return true;
    }

    private sealed record Authorization(
        object DeviceInstance,
        string DeviceKey,
        Guid RequestId,
        DateTime AuthorizedAt);
}
