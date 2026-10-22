namespace GymFlow.Application.DTOs.Students;

public sealed record SetStudentAppAccessRequest(bool Enabled);
public sealed record ValidateStudentAppInviteRequest(string Token);
public sealed record ActivateStudentAppRequest(string Token, string Password);
