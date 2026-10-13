using GymFlow.Api.Platform;
using GymFlow.Application.DTOs.Platform;
using GymFlow.Application.Exceptions;
using GymFlow.Application.Interfaces.Repositories;
using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/platform")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class PlatformController(PlatformOnboardingService service, IPlatformRepository repository, PlatformAuthOptions options) : ControllerBase
{
    [AllowAnonymous, HttpPost("auth/login"), EnableRateLimiting("login")]
    public async Task<IActionResult> Login(PlatformLoginRequest request)
    {
        if (!options.Enabled) return NotFound();
        var result = await service.LoginAsync(request);
        return result is null ? Unauthorized(new { message = "Credenciais ou código de autenticação inválidos." }) : Ok(result);
    }

    [Authorize(Policy = PlatformAuthentication.Policy), HttpPost("gym-onboardings")]
    public Task<IActionResult> Provision(ProvisionGymRequest request, [FromHeader(Name = "Idempotency-Key")] string? key) =>
        Execute(async () => Ok(await service.ProvisionAsync(ActorId(), key ?? "", request)));

    [Authorize(Policy = PlatformAuthentication.Policy), HttpGet("gym-onboardings")]
    public async Task<IActionResult> List(int page = 1, int pageSize = 25)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) return BadRequest(new { message = "Paginação inválida." });
        return Ok(await repository.ListAsync(page, pageSize));
    }

    [Authorize(Policy = PlatformAuthentication.Policy), HttpGet("gym-onboardings/{gymId:guid}")]
    public async Task<IActionResult> Detail(Guid gymId)
    {
        var result = await repository.DetailAsync(gymId);
        return result is null ? NotFound() : Ok(result);
    }

    [Authorize(Policy = PlatformAuthentication.Policy), HttpPatch("gym-onboardings/{gymId:guid}/steps")]
    public Task<IActionResult> Step(Guid gymId, OnboardingStepRequest request) =>
        Execute(async () => await service.StepAsync(ActorId(), gymId, request) ? NoContent() : NotFound());

    [Authorize(Policy = PlatformAuthentication.Policy), HttpPost("gym-onboardings/{gymId:guid}/admin-invitation")]
    public Task<IActionResult> Reissue(Guid gymId, ReissueInvitationRequest request) => Execute(async () =>
    {
        var result = await service.ReissueAsync(ActorId(), gymId, request.Version);
        return result is null ? NotFound() : Ok(result);
    });
    private Guid ActorId() => Guid.Parse(User.FindFirst("sub")!.Value);
    private async Task<IActionResult> Execute(Func<Task<IActionResult>> operation)
    {
        try { return await operation(); }
        catch (PlatformConflictException) { return Conflict(new { message = "Conflito com os dados atuais. Atualize antes de continuar." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }
}

[ApiController]
[Route("api/onboarding/admin-activation")]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class AdminActivationController(PlatformOnboardingService service, PlatformAuthOptions options) : ControllerBase
{
    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Activate(ActivateAdminRequest request)
    {
        if (!options.Enabled) return NotFound();
        try
        {
            return await service.ActivateAsync(request) ? NoContent() : BadRequest(new { message = "Convite inválido ou expirado." });
        }
        catch (PlatformConflictException) { return Conflict(new { message = "Convite já processado ou alterado. Solicite orientação ao operador." }); }
        catch (ArgumentException) { return BadRequest(new { message = "Verifique os dados de ativação." }); }
    }
}
