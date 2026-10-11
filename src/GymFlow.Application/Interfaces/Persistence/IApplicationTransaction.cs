namespace GymFlow.Application.Interfaces.Persistence;

public interface IApplicationTransaction
{
    Task ExecuteAsync(
        Func<Task> operation);
}