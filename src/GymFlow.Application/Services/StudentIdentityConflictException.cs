namespace GymFlow.Application.Services;

public sealed class StudentIdentityConflictException(string? message = null)
    : Exception(message);
