using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Services;

namespace WyszukiwaczNet.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ILogger<SubscriptionsController> _logger;

    public SubscriptionsController(ISubscriptionService subscriptionService, ILogger<SubscriptionsController> logger)
    {
        _subscriptionService = subscriptionService;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.GetAllPlansAsync();
        return Ok(new { success = true, data = plans });
    }

    [Authorize]
    [HttpGet("{userId}/plan")]
    public async Task<IActionResult> GetUserPlan(int userId)
    {
        var plan = await _subscriptionService.GetUserPlanAsync(userId);
        return Ok(new { success = true, data = plan });
    }

    [Authorize]
    [HttpGet("{userId}/limits")]
    public async Task<IActionResult> GetUserLimits(int userId)
    {
        var limits = await _subscriptionService.GetUserLimitsAsync(userId);
        return Ok(new { success = true, data = limits });
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> CreateCheckoutSession([FromBody] CreateCheckoutSessionRequest request)
    {
        try
        {
            var url = await _subscriptionService.CreateCheckoutSessionAsync(
                request.UserId, request.PlanSlug, request.SuccessUrl, request.CancelUrl);
            return Ok(new { success = true, url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{userId}/cancel")]
    public async Task<IActionResult> CancelSubscription(int userId)
    {
        var result = await _subscriptionService.CancelSubscriptionAsync(userId);
        return Ok(new { success = result, message = result ? "Subskrypcja anulowana." : "Brak aktywnej subskrypcji." });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var payload = await new StreamReader(Request.Body).ReadToEndAsync();
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var sig))
            return BadRequest();

        try
        {
            await _subscriptionService.HandleStripeWebhookAsync(payload, sig!);
            return Ok();
        }
        catch (Stripe.StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook error");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook processing error");
            return StatusCode(500);
        }
    }
}
