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
    int RequestNumber
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
    bool Discord
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
    string? Schedule
);

public class UserNotificationConfigDto
{
    public string Phrase { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public string? Schedule { get; set; }
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
