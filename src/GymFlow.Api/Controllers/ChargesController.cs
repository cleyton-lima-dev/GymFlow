using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GymFlow.Api.Controllers;

[ApiController]
[Route("api/charges")]
[Authorize(Roles = "Admin")]
public class ChargesController : ControllerBase
{
    private readonly ChargeService _chargeService;

    public ChargesController(ChargeService chargeService)
    {
        _chargeService = chargeService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var charges = await _chargeService
            .ListAsync(gymId);

        return Ok(charges);
    }

    [HttpPatch("{id:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid id)
    {
        if (!TryGetGymId(out var gymId))
            return Unauthorized();

        var confirmed = await _chargeService
            .ConfirmPaymentAsync(gymId, id);

        if (!confirmed)
        {
            return Conflict(new
            {
                message = "Não foi possível confirmar o pagamento."
            });
        }

        return NoContent();
    }

    private bool TryGetGymId(out Guid gymId)
    {
        var gymIdClaim = User.FindFirst("gym_id")?.Value;

        return Guid.TryParse(gymIdClaim, out gymId);
    }
}