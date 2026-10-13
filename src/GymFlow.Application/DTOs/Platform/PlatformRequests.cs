using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace GymFlow.Application.DTOs.Platform;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlatformLoginRequest(
    [Required, EmailAddress, MaxLength(200)] string Email,
    [Required, MaxLength(128)] string Password,
    [Required, RegularExpression("^[0-9]{6}$")] string Code);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProvisionGymRequest(
    [Required, MaxLength(150)] string AdministrativeName,
    [Required, MaxLength(100)] string TimeZoneId,
    [Required, MaxLength(150)] string AdminName,
    [Required, EmailAddress, MaxLength(200)] string AdminEmail,
    bool PhysicalAccessContracted);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OnboardingStepRequest(
    [Range(1, long.MaxValue)] long Version,
    [Required] string Step,
    [MaxLength(1000)] string? Evidence);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReissueInvitationRequest([Range(1, long.MaxValue)] long Version);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ActivateAdminRequest(
    [Required, MaxLength(100)] string Token,
    [Required, MinLength(8), MaxLength(128)] string Password);
