using System.Text;
using GymFlow.Api.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GymFlow.IntegrationTests.ExceptionHandling;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WhenUnexpectedExceptionOccurs_ShouldReturnSafe500Response()
    {
        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance);

        var context = new DefaultHttpContext();

        context.TraceIdentifier = "test-trace-id";
        context.Response.Body = new MemoryStream();

        var exception = new InvalidOperationException(
            "SEGREDO_INTERNO_DO_SERVIDOR");

        var handled = await handler.TryHandleAsync(
            context,
            exception,
            CancellationToken.None);

        context.Response.Body.Position = 0;

        using var reader = new StreamReader(
            context.Response.Body,
            Encoding.UTF8);

        var body = await reader.ReadToEndAsync();

        Assert.True(handled);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.Contains(
            "Ocorreu um erro interno no servidor.",
            body);

        Assert.Contains(
            "test-trace-id",
            body);

        Assert.DoesNotContain(
            "SEGREDO_INTERNO_DO_SERVIDOR",
            body);
    }
}