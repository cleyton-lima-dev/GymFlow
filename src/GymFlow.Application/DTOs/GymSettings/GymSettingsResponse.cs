namespace GymFlow.Application.DTOs.GymSettings;

// No writable tenant identifier, ERP switches or device credentials.
public sealed record GymSettingsResponse(string? TimeZoneId, string TimeZoneStatus,
    string? AdministrativeName = null, long? Version = null, string TimeZoneSource = "legacy");
