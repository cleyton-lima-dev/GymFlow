namespace GymFlow.Application.DTOs.Auth;

public sealed record ProfessorResponse(
    Guid Id,
    string Name,
    string Email,
    bool IsActive,
    DateTime CreatedAt);