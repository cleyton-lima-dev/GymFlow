using System.Text.Json;
using GymFlow.Application.DTOs.GymSettings;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Validation;
using GymFlow.Domain.Entities;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Time;

namespace GymFlow.Application.Services;

public sealed class GymSettingsService(IGymSettingsRepository repository, IGymTimeZoneProvider timeZones, TimeProvider clock)
{
    public async Task<GymSettingsResponse> GetAsync(Guid gymId)
    {
        if (gymId == Guid.Empty) throw new ArgumentException("Academia inválida.", nameof(gymId));
        var settings = await repository.GetAsync(gymId);
        try
        {
            return new(GymTimeZonePolicy.ToIana(timeZones.GetTimeZone(gymId).Id), "configured", settings?.AdministrativeName,
                settings?.Version, settings is null ? "legacy" : "persisted");
        }
        catch (GymTimeZoneNotConfiguredException)
        {
            return new(null, "missing", settings?.AdministrativeName, settings?.Version, "missing");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            // A malformed server value is not exposed or silently replaced with UTC.
            return new(null, "invalid", settings?.AdministrativeName, settings?.Version, settings is null ? "legacy" : "persisted");
        }
    }

    public async Task<GymSettingsResponse> UpdateAsync(Guid gymId, Guid actorId, UpdateGymSettingsRequest request)
    {
        if (gymId == Guid.Empty || actorId == Guid.Empty) throw new ArgumentException("Identidade inválida.");
        var name = request.AdministrativeName.Trim();
        if (name.Length is 0 or > 150) throw new ArgumentException("Informe um nome fantasia de até 150 caracteres.");
        var zone = GymTimeZonePolicy.ValidateIana(request.TimeZoneId);
        var previous = await repository.GetAsync(gymId);
        if (request.Version != previous?.Version) throw new GymSettingsConflictException();
        var effective = await GetAsync(gymId);
        if (zone != effective.TimeZoneId && !request.ConfirmTimeZoneChange)
            throw new ArgumentException("Confirme os impactos da alteração do fuso horário antes de salvar.");
        var now = clock.GetUtcNow().UtcDateTime;
        var settings = new GymSettings
        {
            GymId = gymId, AdministrativeName = name, TimeZoneId = zone,
            Version = checked((previous?.Version ?? 0) + 1), CreatedAt = previous?.CreatedAt ?? now, UpdatedAt = now
        };
        var audit = new GymSettingsAuditLog
        {
            Id = Guid.NewGuid(), GymId = gymId, ActorUserId = actorId, OccurredAt = now,
            PreviousValues = JsonSerializer.Serialize(new { AdministrativeName = previous?.AdministrativeName, effective.TimeZoneId, effective.TimeZoneSource, Version = previous?.Version }),
            NewValues = JsonSerializer.Serialize(new { settings.AdministrativeName, settings.TimeZoneId, settings.Version })
        };
        await repository.SaveAsync(settings, request.Version, audit);
        return new(zone, "configured", name, settings.Version, "persisted");
    }
}
