using GymFlow.Application.DTOs.Auth;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthenticationService _authenticationService;

    public AuthController(AuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var result = await _authenticationService.LoginAsync(request);

        if (result is null)
        {
            return Unauthorized(new
            {
                message = "E-mail ou senha inválidos."
            });
        }

        return Ok(result);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
    RegisterRequest request)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var created =
            await _authenticationService
                .RegisterAsync(gymId, request);

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

        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(role))
        {
            return Unauthorized();
        }

        return Ok(new
        {
            userId,
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