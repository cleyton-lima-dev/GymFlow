namespace GymFlow.Application.Exceptions;

public sealed class GymTimeZoneNotConfiguredException
    : Exception
{
    public GymTimeZoneNotConfiguredException(Guid gymId)
        : base(
            $"Fuso horário não configurado para a academia '{gymId}'.")
    {
    }
}