namespace GymFlow.Application.DTOs.PhysicalAccess;

public sealed record IssuedPhysicalAccessPinResponse(
    PhysicalAccessCredentialResponse Credential,
    string Pin);
