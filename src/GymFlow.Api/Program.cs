using GymFlow.Application.DependencyInjection;
using GymFlow.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;
using GymFlow.Application.Interfaces.Repositories;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using GymFlow.Api.ExceptionHandling;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);


// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Informe o JWT para acessar endpoints protegidos."
    });

    options.AddSecurityRequirement(document =>
        new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = []
        });
});

var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException(
        "Jwt:Key não configurada.");
}

if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key deve possuir pelo menos 32 bytes.");
}

var jwtIssuer = builder.Configuration["Jwt:Issuer"];

if (string.IsNullOrWhiteSpace(jwtIssuer))
{
    throw new InvalidOperationException(
        "Jwt:Issuer não configurado.");
}

var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrWhiteSpace(jwtAudience))
{
    throw new InvalidOperationException(
        "Jwt:Audience não configurado.");
}

var jwtExpirationRaw =
    builder.Configuration["Jwt:ExpirationMinutes"];

if (!int.TryParse(
        jwtExpirationRaw,
        out var jwtExpirationMinutes) ||
    jwtExpirationMinutes <= 0)
{
    throw new InvalidOperationException(
        "Jwt:ExpirationMinutes deve ser um inteiro maior que zero.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,

            ValidateAudience = true,
            ValidAudience = jwtAudience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var logger =
                    context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtValidation");

                var userIdClaim =
                    context.Principal?
                        .FindFirst(ClaimTypes.NameIdentifier)?
                        .Value;

                var gymIdClaim =
                    context.Principal?
                        .FindFirst("gym_id")?
                        .Value;

                if (!Guid.TryParse(
                        userIdClaim,
                        out var userId) ||
                    !Guid.TryParse(
                        gymIdClaim,
                        out var gymId))


                {
                    logger.LogWarning(
                    "Token rejeitado por claims inválidas. IP: {ClientIp}",
                    GetClientIp(context.HttpContext));

                    context.Fail(
                        "Token sem identificação válida.");

                    return;
                }

                var userRepository =
                    context.HttpContext
                        .RequestServices
                        .GetRequiredService<IUserRepository>();

                var isActive =
                    await userRepository.IsActiveAsync(
                        userId,
                        gymId);

                if (!isActive)
                {
                    logger.LogWarning(
                        "Token rejeitado para usuário inativo ou inexistente. UserId: {UserId}, GymId: {GymId}",
                    userId,
                     gymId);
                    context.Fail(
                        "Usuário inativo ou inexistente.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode =
        StatusCodes.Status429TooManyRequests;

    options.AddPolicy(
        "login",
        httpContext =>
        {
            var clientIp = GetClientIp(httpContext);

            return RateLimitPartition
                .GetFixedWindowLimiter(
                    partitionKey: clientIp,
                    factory: _ =>
                        new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            QueueProcessingOrder =
                                QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true
                        });
        });

    options.OnRejected =
        async (context, cancellationToken) =>
        {
            var logger =
                context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("LoginRateLimiter");

            logger.LogWarning(
            "Rate limit de login acionado. IP: {ClientIp}",
             GetClientIp(context.HttpContext));

            context.HttpContext.Response.StatusCode =
                StatusCodes.Status429TooManyRequests;

            await context.HttpContext.Response
                .WriteAsJsonAsync(
                    new
                    {
                        message =
                            "Muitas tentativas de login. Tente novamente em instantes."
                    },
                    cancellationToken);
        };
});

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment() &&
    string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("FLY_APP_NAME")))
{
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();

app.Run();

static string GetClientIp(HttpContext httpContext)
{
    if (!string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("FLY_APP_NAME")))
    {
        var flyClientIp =
            httpContext.Request.Headers["Fly-Client-IP"].ToString();

        if (!string.IsNullOrWhiteSpace(flyClientIp))
        {
            return flyClientIp;
        }
    }

    return httpContext.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
