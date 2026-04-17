using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WyszukiwaczNet.Api.Entities;

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(30)]
    [Column("phone")]
    public string? Phone { get; set; }

    [MaxLength(255)]
    [Column("name")]
    public string? Name { get; set; }

    [MaxLength(255)]
    [Column("surname")]
    public string? Surname { get; set; }

    [MaxLength(255)]
    [Column("login")]
    public string? Login { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    public ICollection<UserPlatformSubscription> PlatformSubscriptions { get; set; } = new List<UserPlatformSubscription>();
    public ICollection<UserNotificationSetting> NotificationSettings { get; set; } = new List<UserNotificationSetting>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<BackgroundJob> BackgroundJobs { get; set; } = new List<BackgroundJob>();
}

[Table("platforms")]
public class Platform
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    [Column("type")]
    public string Type { get; set; } = string.Empty;

    public ICollection<UserPlatformSubscription> UserSubscriptions { get; set; } = new List<UserPlatformSubscription>();
    public ICollection<Offer> Offers { get; set; } = new List<Offer>();
}

[Table("user_platform_subscriptions")]
public class UserPlatformSubscription
{
    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [ForeignKey("PlatformId")]
    public Platform? Platform { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    public override string ToString()
    {
        return Platform?.Name ?? string.Empty;
    }
}

[Table("notification_channels")]
public class NotificationChannel
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    public ICollection<UserNotificationSetting> UserSettings { get; set; } = new List<UserNotificationSetting>();
}

[Table("user_notification_settings")]
public class UserNotificationSetting
{
    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("channel_id")]
    public int ChannelId { get; set; }

    [ForeignKey("ChannelId")]
    public NotificationChannel? Channel { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;
}

[Table("offers")]
public class Offer
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("platform_id")]
    public int PlatformId { get; set; }

    [ForeignKey("PlatformId")]
    public Platform? Platform { get; set; }

    [MaxLength(255)]
    [Column("external_id")]
    public string? ExternalId { get; set; }

    [Required]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("price", TypeName = "numeric(10,2)")]
    public decimal? Price { get; set; }

    [MaxLength(10)]
    [Column("currency")]
    public string? Currency { get; set; } = "PLN";

    [Required]
    [Column("url")]
    public string Url { get; set; } = string.Empty;

    [Column("image_url")]
    public string? ImageUrl { get; set; }

    [MaxLength(255)]
    [Column("seller_name")]
    public string? SellerName { get; set; }

    [MaxLength(255)]
    [Column("location")]
    public string? Location { get; set; }

    [MaxLength(255)]
    [Column("additional_info")]
    public string? AdditionalInfo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    [Column("status")]
    public string Status { get; set; } = "active";

    public VehicleDetail? VehicleDetail { get; set; }
    public ICollection<OfferHistory> OfferHistories { get; set; } = new List<OfferHistory>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

[Table("vehicle_details")]
public class VehicleDetail
{
    [Key]
    [Column("offer_id")]
    public int OfferId { get; set; }

    [ForeignKey("OfferId")]
    public Offer? Offer { get; set; }

    [Column("production_year")]
    public short? ProductionYear { get; set; }

    [Column("mileage")]
    public int? Mileage { get; set; }

    [MaxLength(50)]
    [Column("fuel_type")]
    public string? FuelType { get; set; }

    [MaxLength(50)]
    [Column("gearbox")]
    public string? Gearbox { get; set; }

    [Column("engine_power")]
    public int? EnginePower { get; set; }

    [MaxLength(50)]
    [Column("body_type")]
    public string? BodyType { get; set; }
}

[Table("offer_history")]
public class OfferHistory
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("offer_id")]
    public int OfferId { get; set; }

    [ForeignKey("OfferId")]
    public Offer? Offer { get; set; }

    [MaxLength(50)]
    [Column("action")]
    public string? Action { get; set; }

    [MaxLength(50)]
    [Column("source")]
    public string? Source { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [Column("offer_id")]
    public int? OfferId { get; set; }

    [ForeignKey("OfferId")]
    public Offer? Offer { get; set; }

    [MaxLength(50)]
    [Column("channel")]
    public string? Channel { get; set; }

    [MaxLength(20)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("message")]
    public string? Message { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("user_notification_configs")]
public class UserNotificationConfig
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [MaxLength(255)]
    [Column("phrase")]
    public string Phrase { get; set; } = string.Empty;

    [Column("request_count")]
    public int RequestCount { get; set; }

    [MaxLength(100)]
    [Column("schedule")]
    public string? Schedule { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("background_jobs")]
public class BackgroundJob
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey("UserId")]
    public User? User { get; set; }

    [MaxLength(50)]
    [Column("job_type")]
    public string? JobType { get; set; }

    [MaxLength(20)]
    [Column("status")]
    public string? Status { get; set; }

    [Column("last_run_at")]
    public DateTime? LastRunAt { get; set; }

    [Column("next_run_at")]
    public DateTime? NextRunAt { get; set; }

    [MaxLength(100)]
    [Column("hangfire_job_id")]
    public string? HangfireJobId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
