namespace GymFlow.AccessAgent.Abstractions;

public interface IAccessDeviceAdapter
{
    string ProviderKey { get; }

    Task StartAsync(
        Func<DeviceAccessAttempt, CancellationToken, Task<AccessDeviceDecision>>
            handleAttemptAsync,
        CancellationToken cancellationToken);

    Task ApplyDecisionAsync(
        DeviceAccessAttempt attempt,
        AccessDeviceDecision decision,
        CancellationToken cancellationToken);
}

public sealed record DeviceAccessAttempt(
    Guid RequestId,
    string ExternalIdentifier,
    AccessCredentialType CredentialType,
    DateTime OccurredAt);

public enum AccessCredentialType
{
    Card = 1,
    QrCode = 2,
    BiometricExternalId = 3,
    FacialExternalId = 4,
    PinExternalId = 5,
    Other = 99
}

public sealed record AccessDeviceDecision(
    bool Allowed,
    string Reason);