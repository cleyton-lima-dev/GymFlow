using GymFlow.Application.Interfaces.Persistence;
using GymFlow.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace GymFlow.Infrastructure.Persistence;

public sealed class EfApplicationTransaction :
    IApplicationTransaction
{
    private readonly AppDbContext _context;

    public EfApplicationTransaction(
        AppDbContext context)
    {
        _context = context;
    }

    public async Task ExecuteAsync(
        Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(
            operation);

        await using var transaction =
            await _context.Database
                .BeginTransactionAsync();

        try
        {
            await operation();

            await transaction
                .CommitAsync();
        }
        catch
        {
            await transaction
                .RollbackAsync();

            throw;
        }
    }
}