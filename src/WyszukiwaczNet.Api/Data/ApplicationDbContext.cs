using Microsoft.EntityFrameworkCore;
using WyszukiwaczNet.Api.Entities;

namespace WyszukiwaczNet.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Platform> Platforms => Set<Platform>();
    public DbSet<UserPlatformSubscription> UserPlatformSubscriptions => Set<UserPlatformSubscription>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<UserNotificationSetting> UserNotificationSettings => Set<UserNotificationSetting>();
    public DbSet<Offer> Offers => Set<Offer>();
    public DbSet<VehicleDetail> VehicleDetails => Set<VehicleDetail>();
    public DbSet<JobDetail> JobDetails => Set<JobDetail>();
    public DbSet<OfferHistory> OfferHistories => Set<OfferHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();
    public DbSet<UserNotificationConfig> UserNotificationConfigs => Set<UserNotificationConfig>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<UserSearchHistory> UserSearchHistories => Set<UserSearchHistory>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserPlatformSubscription>(entity =>
        {
            entity.HasKey(ups => new { ups.UserId, ups.PlatformId });
        });

        modelBuilder.Entity<NotificationChannel>(entity =>
        {
            entity.HasIndex(nc => nc.Name).IsUnique();
        });

        modelBuilder.Entity<UserNotificationSetting>(entity =>
        {
            entity.HasKey(uns => new { uns.UserId, uns.ChannelId });
        });

        modelBuilder.Entity<Offer>()
            .HasOne(o => o.VehicleDetail)
            .WithOne(v => v.Offer)
            .HasForeignKey<VehicleDetail>(v => v.OfferId);

        modelBuilder.Entity<Offer>()
            .HasOne(o => o.JobDetail)
            .WithOne(j => j.Offer)
            .HasForeignKey<JobDetail>(j => j.OfferId);

        modelBuilder.Entity<Offer>()
            .HasIndex(o => o.PlatformId);

        modelBuilder.Entity<Offer>()
            .HasIndex(o => o.CreatedAt);

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
        });

        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.HasIndex(p => p.Slug).IsUnique();
        });

        modelBuilder.Entity<UserSubscription>(entity =>
        {
            entity.HasIndex(us => us.UserId);
            entity.HasIndex(us => us.StripeSubscriptionId);
        });
    }
}
