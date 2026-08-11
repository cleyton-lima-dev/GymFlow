using GymFlow.Domain.Enums;

namespace GymFlow.Application.DTOs.Auth;

public class RegisterRequest
{
    public Guid GymId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public UserRole Role { get; set; }
}