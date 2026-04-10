namespace WyszukiwaczNet.Api.DTOs;

public record RegisterUserRequest(
    string Email,
    string? Phone,
    string Password,
    string Name,
    string Surname,
    string Login
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
