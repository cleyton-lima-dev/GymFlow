namespace GymFlow.Application.DTOs.Students;

public sealed record SetStudentAppAccessRequest(bool Enabled, string? Email = null);
public sealed record ValidateStudentAppInviteRequest(string Token);
public sealed record ActivateStudentAppRequest(string Token, string Password);
