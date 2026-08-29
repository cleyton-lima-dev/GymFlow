using GymFlow.Application.DTOs.Auth;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationService _authenticationService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthenticationService authenticationService,
        ILogger<AuthController> logger)
    {
        _authenticationService = authenticationService;
        _logger = logger;
    }

    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authenticationService.LoginAsync(request);

        if (result is null)
        {
            _logger.LogWarning(
                "Tentativa de login rejeitada. IP: {ClientIp}",
                HttpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown");

            return Unauthorized(new
            {
                message = "E-mail ou senha inválidos."
            });
        }

        _logger.LogInformation(
            "Login realizado com sucesso. UserId: {UserId}, GymId: {GymId}, Role: {Role}",
            result.UserId,
            result.GymId,
            result.Role);

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
    RegisterRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        bool created;

        try
        {
            created = await _authenticationService
                .RegisterAsync(gymId, request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }

        if (!created)
        {
            return Conflict(new
            {
                message = "Já existe um usuário com este e-mail."
            });
        }

        return StatusCode(201, new
        {
            message = "Professor criado com sucesso."
        });
    }


    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var name = User.FindFirst(ClaimTypes.Name)?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var gymId = User.FindFirst("gym_id")?.Value;

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(role) ||
            string.IsNullOrWhiteSpace(gymId))
        {
            return Unauthorized();
        }

        return Ok(new
        {
            userId,
            gymId,
            name,
            email,
            role
        });
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public IActionResult AdminOnly()
    {
        return Ok(new
        {
            message = "Acesso de administrador autorizado."
        });
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim =
            User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(
            gymIdClaim,
            out gymId);
    }
}