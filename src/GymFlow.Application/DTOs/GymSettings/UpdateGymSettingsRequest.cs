using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace GymFlow.Application.DTOs.GymSettings;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UpdateGymSettingsRequest
{
    [Required, MaxLength(150)]
    public string AdministrativeName { get; set; } = string.Empty;
    [Required, MaxLength(100)]
    public string TimeZoneId { get; set; } = string.Empty;
    [Range(1, long.MaxValue)]
    public long? Version { get; set; }
    public bool ConfirmTimeZoneChange { get; set; }
}
