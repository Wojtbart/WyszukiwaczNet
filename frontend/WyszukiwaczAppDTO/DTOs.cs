namespace WyszukiwaczAppDTO;

public class RegisterUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? Message { get; set; }
}

public class UserResponse
{
    public bool Success { get; set; }
    public int? UserId { get; set; }
    public string? Message { get; set; }
}

public class PlatformSubscriptionRequest
{
    public int UserId { get; set; }
    public int PlatformId { get; set; }
    public bool Enabled { get; set; }
}

public class NotificationSettingRequest
{
    public int UserId { get; set; }
    public int ChannelId { get; set; }
    public bool Enabled { get; set; }
}

public class GetDataRequest
{
    public List<string> Websites { get; set; } = new();
    public string Phrase { get; set; } = string.Empty;
    public string? AdditionalPhrase { get; set; }
    public int RequestNumber { get; set; }
}

public class NotificationRequest
{
    public int UserId { get; set; }
    public List<string> Websites { get; set; } = new();
    public string Phrase { get; set; } = string.Empty;
    public string? AdditionalPhrase { get; set; }
    public int RequestNumber { get; set; }
    public string? HourToSendMail { get; set; }
    public int? RepeatAfterSpecifiedTime { get; set; }
    public bool Email { get; set; }
    public bool Sms { get; set; }
    public bool Discord { get; set; }
    public bool InApp { get; set; }
}

public class UserNotificationSettingDto
{
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class UserPlatformSubscriptionDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class OfferResponse
{
    public int Id { get; set; }
    public int PlatformId { get; set; }
    public string? PlatformName { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? SellerName { get; set; }
    public string? Location { get; set; }
    public string? AdditionalInfo { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = "active";
    public VehicleDetailResponse? VehicleDetail { get; set; }
}

public class VehicleDetailResponse
{
    public int OfferId { get; set; }
    public short? ProductionYear { get; set; }
    public int? Mileage { get; set; }
    public string? FuelType { get; set; }
    public string? Gearbox { get; set; }
    public int? EnginePower { get; set; }
    public string? BodyType { get; set; }
}

public class PlatformResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

public class UserPlatformSubscription
{
    public int UserId { get; set; }
    public int PlatformId { get; set; }
    public bool Enabled { get; set; }
}

public class UserNotificationSetting
{
    public int UserId { get; set; }
    public int ChannelId { get; set; }
    public bool Enabled { get; set; }
}

public class NotificationChannel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class UserNotificationConfigDto
{
    public string Phrase { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public string? Schedule { get; set; }
    public string? Category { get; set; }
    public bool Enabled { get; set; }
}

public class SaveNotificationConfigRequest
{
    public int UserId { get; set; }
    public string Phrase { get; set; } = string.Empty;
    public int RequestCount { get; set; }
    public string? Schedule { get; set; }
    public string? Category { get; set; }
}

public class SetConfigEnabledRequest
{
    public int UserId { get; set; }
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

public class NotificationFeedResponse
{
    public bool Success { get; set; }
    public List<NotificationFeedItemDto>? Data { get; set; }
    public int UnreadCount { get; set; }
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

public class UserJobsResponse
{
    public bool Success { get; set; }
    public List<UserJobDto>? Data { get; set; }
}
