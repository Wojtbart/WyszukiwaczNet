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
    public DbSet<OfferHistory> OfferHistories => Set<OfferHistory>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<BackgroundJob> BackgroundJobs => Set<BackgroundJob>();

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
            .HasIndex(o => o.PlatformId);

        modelBuilder.Entity<Offer>()
            .HasIndex(o => o.CreatedAt);

        modelBuilder.Entity<Platform>(entity =>
        {
            entity.HasIndex(p => p.Name).IsUnique();
        });
    }
}
