namespace GymFlow.Application.Exceptions;

public sealed class PlatformConflictException : Exception
{
    public PlatformConflictException() : base("A operação conflita com os dados atuais. Atualize os dados antes de continuar.") { }
}
