namespace GymFlow.Domain.Enums;

public enum PhysicalAccessCredentialType
{
    Card = 1,
    QrCode = 2,
    BiometricExternalId = 3,
    FacialExternalId = 4,
    PinExternalId = 5,
    Other = 99
}