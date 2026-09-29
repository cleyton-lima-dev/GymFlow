using GymFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using GymFlow.Application.DTOs.Charges;

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
    public async Task<IActionResult> ConfirmPayment(
    Guid id,
    PaymentDetailsRequest request)
    {
        if (!TryGetGymId(out var gymId) ||
            !TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var confirmed = await _chargeService
            .ConfirmPaymentAsync(
        gymId,
        userId,
        id,
        request);

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

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User
            .FindFirst(ClaimTypes.NameIdentifier)
            ?.Value;

        return Guid.TryParse(
            userIdClaim,
            out userId);
    }
}