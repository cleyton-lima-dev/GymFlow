namespace GymFlow.Application.Exceptions;
public sealed class GymSettingsConflictException : Exception
{
    public GymSettingsConflictException() : base("As configurações foram alteradas por outra operação. Recarregue os dados antes de salvar.") { }
}
