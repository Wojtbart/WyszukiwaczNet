using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using WyszukiwaczNet.Api.Data;
using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Entities;

namespace WyszukiwaczNet.Api.Services;

public interface ISubscriptionService
{
    Task<UserPlanDto> GetUserPlanAsync(int userId);
    Task<PlanLimits> GetUserLimitsAsync(int userId);
    Task<string> CreateCheckoutSessionAsync(int userId, string planSlug, string successUrl, string cancelUrl);
    Task HandleStripeWebhookAsync(string payload, string stripeSignature);
    Task<bool> CancelSubscriptionAsync(int userId);
    Task<List<SubscriptionPlanDto>> GetAllPlansAsync();
}

public class SubscriptionService : ISubscriptionService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(ApplicationDbContext db, IConfiguration config, ILogger<SubscriptionService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        StripeConfiguration.ApiKey = config["Stripe:SecretKey"];
    }

    public async Task<UserPlanDto> GetUserPlanAsync(int userId)
    {
        var sub = await _db.UserSubscriptions
            .Include(s => s.Plan)
            .Where(s => s.UserId == userId && s.Status == "active")
            .OrderByDescending(s => s.CurrentPeriodEnd)
            .FirstOrDefaultAsync();

        if (sub?.Plan == null)
            return new UserPlanDto("free", "Free", 0, null);

        return new UserPlanDto(
            sub.Plan.Slug,
            sub.Plan.Name,
            (double)sub.Plan.PricePln,
            sub.CurrentPeriodEnd
        );
    }

    public async Task<PlanLimits> GetUserLimitsAsync(int userId)
    {
        var plan = await GetUserPlanAsync(userId);
        return PlanLimits.ForSlug(plan.Slug);
    }

    public async Task<string> CreateCheckoutSessionAsync(int userId, string planSlug, string successUrl, string cancelUrl)
    {
        var plan = await _db.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.Slug == planSlug && p.IsActive)
            ?? throw new InvalidOperationException($"Plan '{planSlug}' not found.");

        if (string.IsNullOrEmpty(plan.StripePriceId))
            throw new InvalidOperationException($"Plan '{planSlug}' has no Stripe price configured.");

        var user = await _db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException("User not found.");

        var existingSub = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "active" && s.StripeCustomerId != null)
            .FirstOrDefaultAsync();

        var sessionOptions = new SessionCreateOptions
        {
            Mode = "subscription",
            LineItems = new List<SessionLineItemOptions>
            {
                new() { Price = plan.StripePriceId, Quantity = 1 }
            },
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            ClientReferenceId = userId.ToString(),
            CustomerEmail = existingSub?.StripeCustomerId == null ? user.Email : null,
            Customer = existingSub?.StripeCustomerId,
            Metadata = new Dictionary<string, string> { { "user_id", userId.ToString() }, { "plan_slug", planSlug } },
            AllowPromotionCodes = true,
            PaymentMethodTypes = new List<string> { "card", "p24" },
        };

        var service = new SessionService();
        var session = await service.CreateAsync(sessionOptions);
        return session.Url;
    }

    public async Task HandleStripeWebhookAsync(string payload, string stripeSignature)
    {
        var webhookSecret = _config["Stripe:WebhookSecret"] ?? throw new InvalidOperationException("Stripe webhook secret not configured.");
        var stripeEvent = EventUtility.ConstructEvent(payload, stripeSignature, webhookSecret);

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await HandleCheckoutCompleted((Session)stripeEvent.Data.Object);
                break;
            case EventTypes.CustomerSubscriptionUpdated:
                await HandleSubscriptionUpdated((Stripe.Subscription)stripeEvent.Data.Object);
                break;
            case EventTypes.CustomerSubscriptionDeleted:
                await HandleSubscriptionDeleted((Stripe.Subscription)stripeEvent.Data.Object);
                break;
            case EventTypes.InvoicePaymentFailed:
                await HandlePaymentFailed((Invoice)stripeEvent.Data.Object);
                break;
        }
    }

    private async Task HandleCheckoutCompleted(Session session)
    {
        if (!int.TryParse(session.ClientReferenceId, out var userId)) return;

        var planSlug = session.Metadata.GetValueOrDefault("plan_slug") ?? string.Empty;
        var plan = await _db.SubscriptionPlans.FirstOrDefaultAsync(p => p.Slug == planSlug);
        if (plan == null) return;

        var stripeSvc = new Stripe.SubscriptionService();
        var stripeSub = await stripeSvc.GetAsync(session.SubscriptionId);

        var existing = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "active")
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.PlanId = plan.Id;
            existing.StripeCustomerId = session.CustomerId;
            existing.StripeSubscriptionId = session.SubscriptionId;
            existing.Status = "active";
            existing.CurrentPeriodStart = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodStart ?? DateTime.UtcNow).ToUniversalTime();
            existing.CurrentPeriodEnd = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd ?? DateTime.UtcNow).ToUniversalTime();
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.UserSubscriptions.Add(new UserSubscription
            {
                UserId = userId,
                PlanId = plan.Id,
                StripeCustomerId = session.CustomerId,
                StripeSubscriptionId = session.SubscriptionId,
                Status = "active",
                CurrentPeriodStart = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodStart ?? DateTime.UtcNow).ToUniversalTime(),
                CurrentPeriodEnd = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd ?? DateTime.UtcNow).ToUniversalTime(),
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionUpdated(Stripe.Subscription stripeSub)
    {
        var dbSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);
        if (dbSub == null) return;

        dbSub.Status = stripeSub.Status == "active" ? "active" : stripeSub.Status;
        dbSub.CurrentPeriodStart = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodStart ?? DateTime.UtcNow).ToUniversalTime();
        dbSub.CurrentPeriodEnd = (stripeSub.Items?.Data?.FirstOrDefault()?.CurrentPeriodEnd ?? DateTime.UtcNow).ToUniversalTime();
        dbSub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task HandleSubscriptionDeleted(Stripe.Subscription stripeSub)
    {
        var dbSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);
        if (dbSub == null) return;

        dbSub.Status = "canceled";
        dbSub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task HandlePaymentFailed(Invoice invoice)
    {
        var subId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        var dbSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subId);
        if (dbSub == null) return;

        dbSub.Status = "past_due";
        dbSub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogWarning("Payment failed for user subscription {SubId}", dbSub.Id);
    }

    public async Task<bool> CancelSubscriptionAsync(int userId)
    {
        var sub = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == "active" && s.StripeSubscriptionId != null)
            .FirstOrDefaultAsync();
        if (sub == null) return false;

        var stripeSvc = new Stripe.SubscriptionService();
        await stripeSvc.CancelAsync(sub.StripeSubscriptionId!);

        sub.Status = "canceled";
        sub.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<SubscriptionPlanDto>> GetAllPlansAsync()
    {
        return await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.PricePln)
            .Select(p => new SubscriptionPlanDto(
                p.Id, p.Slug, p.Name, (double)p.PricePln,
                p.MaxAlerts, p.MaxPortals, p.RefreshIntervalMin,
                p.InstantAlerts, p.PriceHistory, p.ExportCsv, p.ApiAccess, p.WebhookSupport))
            .ToListAsync();
    }
}

public record PlanLimits(
    int MaxAlerts,
    int MaxPortals,
    int RefreshIntervalMin,
    bool InstantAlerts,
    bool PriceHistory,
    bool ExportCsv,
    bool ApiAccess,
    bool WebhookSupport
)
{
    public static PlanLimits ForSlug(string slug) => slug switch
    {
        "premium" => new(int.MaxValue, int.MaxValue, 15, true,  true,  true,  false, false),
        "pro"     => new(int.MaxValue, int.MaxValue, 5,  true,  true,  true,  true,  true),
        _         => new(1,            2,            360, false, false, false, false, false)
    };
}
