namespace WyszukiwaczNet.Api.DTOs;

public record RegisterUserRequest(
    string Email,
    string? Phone,
    string Password,
    string Login,
    string Name = "",
    string Surname = ""
);

public record LoginRequest(
    string Login,
    string Password
);

public record LoginResponse(
    bool Success,
    string? Token,
    string? Message
);

public record UserResponse(
    bool Success,
    int? UserId,
    string? Message
);

public record PlatformSubscriptionRequest(
    int UserId,
    int PlatformId,
    bool Enabled
);

public record NotificationSettingRequest(
    int UserId,
    int ChannelId,
    bool Enabled
);

public record GetDataRequest(
    List<string> Websites,
    string Phrase,
    string? AdditionalPhrase,
    int RequestNumber,
    string? WorkLocation = null,
    int? EmploymentLevel = null,
    int? ContractType = null,
    string? Fuel = null,
    string? Gearbox = null,
    int? EngineCapacityFrom = null,
    int? EngineCapacityTo = null,
    decimal? PriceFrom = null,
    decimal? PriceTo = null,
    int? AreaFrom = null,
    int? AreaTo = null
);

public record NotificationRequest(
    int UserId,
    List<string> Websites,
    string Phrase,
    string? AdditionalPhrase,
    int RequestNumber,
    string? HourToSendMail,
    int? RepeatAfterSpecifiedTime,
    bool Email,
    bool Sms,
    bool Discord,
    bool InApp
);

public record OfferResponse(
    int Id,
    int PlatformId,
    string? PlatformName,
    string Title,
    decimal? Price,
    string? Currency,
    string Url,
    string? ImageUrl,
    string? SellerName,
    string? Location,
    string? AdditionalInfo,
    DateTime CreatedAt,
    string Status,
    VehicleDetailResponse? VehicleDetail
);

public record VehicleDetailResponse(
    int OfferId,
    short? ProductionYear,
    int? Mileage,
    string? FuelType,
    string? Gearbox,
    int? EnginePower,
    string? BodyType
);

public record PlatformResponse(
    int Id,
    string Name,
    string Type
);

public record SaveNotificationConfigRequest(
    int UserId,
    string Phrase,
    int RequestCount,
    string? Schedule,
    string? Category
);

public record SetConfigEnabledRequest(
    int UserId,
    string? Category,
    bool Enabled
);

public class UserNotificationConfigDto
{
    public string Phrase { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public string? Schedule { get; set; }
    public string? Category { get; set; }
    public bool Enabled { get; set; }
}

public class NotificationFeedItemDto
{
    public int Id { get; set; }
    public int? OfferId { get; set; }
    public string? OfferTitle { get; set; }
    public decimal? OfferPrice { get; set; }
    public string? OfferUrl { get; set; }
    public string? OfferLocation { get; set; }
    public string? PlatformName { get; set; }
    public string? Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UserJobDto
{
    public string Id { get; set; } = string.Empty;
    public string Phrase { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public DateTime? NextExecution { get; set; }
    public DateTime? LastExecution { get; set; }
    public string? LastState { get; set; }
}

public record UserPlanDto(
    string Slug,
    string Name,
    double PricePln,
    DateTime? ExpiresAt
);

public record SubscriptionPlanDto(
    int Id,
    string Slug,
    string Name,
    double PricePln,
    int MaxAlerts,
    int MaxPortals,
    int RefreshIntervalMin,
    bool InstantAlerts,
    bool PriceHistory,
    bool ExportCsv,
    bool ApiAccess,
    bool WebhookSupport
);

public record CreateCheckoutSessionRequest(
    int UserId,
    string PlanSlug,
    string SuccessUrl,
    string CancelUrl
);

public record UserProfileResponse(
    bool Success,
    int? UserId,
    string? Login,
    string? Email,
    string? Phone,
    string? Message
);

public record UpdatePasswordRequest(
    int UserId,
    string CurrentPassword,
    string NewPassword
);

public record UpdateEmailRequest(
    int UserId,
    string NewEmail
);

public record UpdatePhoneRequest(
    int UserId,
    string? NewPhone
);
