using Microsoft.AspNetCore.Diagnostics;

using Microsoft.EntityFrameworkCore;

namespace GymFlow.Api.ExceptionHandling;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is DbUpdateConcurrencyException)
        {
            logger.LogWarning("Conflito de concorrência na requisição. TraceId: {TraceId}", httpContext.TraceIdentifier);
            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                message = "Os dados foram alterados por outra operação. Atualize os dados antes de tentar novamente.",
                traceId = httpContext.TraceIdentifier
            }, cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Erro não tratado na requisição. TraceId: {TraceId}",
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode =
            StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                message =
                    "Ocorreu um erro interno no servidor.",
                traceId = httpContext.TraceIdentifier
            },
            cancellationToken);

        return true;
    }
}
