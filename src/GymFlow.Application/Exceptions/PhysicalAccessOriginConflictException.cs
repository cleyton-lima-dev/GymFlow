namespace GymFlow.Application.Exceptions;

public sealed class PhysicalAccessOriginConflictException : InvalidOperationException
{
    public PhysicalAccessOriginConflictException()
        : base("A origem do evento físico não corresponde ao agente ou dispositivo autenticado.")
    {
    }
}
