using Microsoft.EntityFrameworkCore;
using WyszukiwaczNet.Api.Data;
using WyszukiwaczNet.Api.Entities;

namespace WyszukiwaczNet.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByLoginAsync(string login);
    Task<User?> GetByPhoneAsync(string phone);
    Task<User> CreateAsync(User user);
    Task<UserPlatformSubscription?> GetPlatformSubscriptionAsync(int userId, int platformId);
    Task<UserPlatformSubscription> CreateOrUpdatePlatformSubscriptionAsync(UserPlatformSubscription subscription);
    Task<List<UserPlatformSubscriptionDto>> GetUserPlatformSubscriptionsAsync(int userId);
    Task<UserNotificationSetting?> GetNotificationSettingAsync(int userId, int channelId);
    Task<UserNotificationSetting> CreateOrUpdateNotificationSettingAsync(UserNotificationSetting setting);
    Task<List<UserNotificationSettingDto>> GetUserNotificationSettingsAsync(int userId);
    Task<UserNotificationConfig?> GetNotificationConfigAsync(int userId, string? category = null);
    Task<List<UserNotificationConfig>> GetAllNotificationConfigsAsync(int userId);
    Task<UserNotificationConfig> SaveNotificationConfigAsync(UserNotificationConfig config);
    Task<bool> SetNotificationConfigEnabledAsync(int userId, string? category, bool enabled);
    Task SaveNotificationFeedItemsAsync(List<Notification> items);
    Task<(List<Notification> Items, int TotalCount)> GetNotificationFeedAsync(int userId, int page = 0, int pageSize = 30);
    Task<int> GetUnreadNotificationCountAsync(int userId);
    Task MarkNotificationsReadAsync(int userId);
    Task MarkSingleNotificationReadAsync(int notificationId);
    Task<User?> ValidateCredentialsAsync(string login, string password);
    Task UpdateAsync(User user);
    Task<PasswordResetToken> CreatePasswordResetTokenAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token);
    Task InvalidatePasswordResetTokenAsync(PasswordResetToken token);
}

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users
            .Include(u => u.PlatformSubscriptions)
            .Include(u => u.NotificationSettings)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .Include(u => u.PlatformSubscriptions)
            .Include(u => u.NotificationSettings)
            .FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<User?> GetByLoginAsync(string login)
    {
        return await _context.Users
            .Include(u => u.PlatformSubscriptions)
            .Include(u => u.NotificationSettings)
            .FirstOrDefaultAsync(u => u.Login == login);
    }

    public async Task<User?> GetByPhoneAsync(string phone)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<UserPlatformSubscription?> GetPlatformSubscriptionAsync(int userId, int platformId)
    {
        return await _context.UserPlatformSubscriptions
            .FirstOrDefaultAsync(ups => ups.UserId == userId && ups.PlatformId == platformId);
    }

    public async Task<UserPlatformSubscription> CreateOrUpdatePlatformSubscriptionAsync(UserPlatformSubscription subscription)
    {
        var existing = await _context.UserPlatformSubscriptions
            .FirstOrDefaultAsync(ups => ups.UserId == subscription.UserId && ups.PlatformId == subscription.PlatformId);

        if (existing != null)
        {
            existing.Enabled = subscription.Enabled;
            _context.UserPlatformSubscriptions.Update(existing);
        }
        else
        {
            _context.UserPlatformSubscriptions.Add(subscription);
        }

        await _context.SaveChangesAsync();
        return existing ?? subscription;
    }

    public async Task<UserNotificationSetting?> GetNotificationSettingAsync(int userId, int channelId)
    {
        return await _context.UserNotificationSettings
            .FirstOrDefaultAsync(uns => uns.UserId == userId && uns.ChannelId == channelId);
    }

    public async Task<UserNotificationSetting> CreateOrUpdateNotificationSettingAsync(UserNotificationSetting setting)
    {
        var existing = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(uns => uns.UserId == setting.UserId && uns.ChannelId == setting.ChannelId);

        if (existing != null)
        {
            existing.Enabled = setting.Enabled;
            _context.UserNotificationSettings.Update(existing);
        }
        else
        {
            _context.UserNotificationSettings.Add(setting);
        }

        await _context.SaveChangesAsync();
        return existing ?? setting;
    }

    public async Task<UserNotificationConfig?> GetNotificationConfigAsync(int userId, string? category = null)
    {
        return await _context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Category == category);
    }

    public async Task<List<UserNotificationConfig>> GetAllNotificationConfigsAsync(int userId)
    {
        return await _context.UserNotificationConfigs
            .Where(c => c.UserId == userId)
            .ToListAsync();
    }

    public async Task<UserNotificationConfig> SaveNotificationConfigAsync(UserNotificationConfig config)
    {
        var existing = await _context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.UserId == config.UserId && c.Category == config.Category);

        if (existing != null)
        {
            existing.Phrase = config.Phrase;
            existing.RequestCount = config.RequestCount;
            existing.Schedule = config.Schedule;
            existing.Enabled = config.Enabled;
            existing.FiltersJson = config.FiltersJson;
            existing.UpdatedAt = DateTime.UtcNow;
            _context.UserNotificationConfigs.Update(existing);
        }
        else
        {
            _context.UserNotificationConfigs.Add(config);
        }

        await _context.SaveChangesAsync();
        return existing ?? config;
    }

    public async Task<bool> SetNotificationConfigEnabledAsync(int userId, string? category, bool enabled)
    {
        var existing = await _context.UserNotificationConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Category == category);
        if (existing == null) return false;
        existing.Enabled = enabled;
        existing.UpdatedAt = DateTime.UtcNow;
        _context.UserNotificationConfigs.Update(existing);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task SaveNotificationFeedItemsAsync(List<Notification> items)
    {
        _context.Notifications.AddRange(items);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetNotificationFeedAsync(int userId, int page = 0, int pageSize = 30)
    {
        var query = _context.Notifications
            .Include(n => n.Offer).ThenInclude(o => o!.Platform)
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip(page * pageSize).Take(pageSize).ToListAsync();
        return (items, total);
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && n.Status == "new");
    }

    public async Task MarkNotificationsReadAsync(int userId)
    {
        await _context.Notifications
            .Where(n => n.UserId == userId && n.Status == "new")
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, "read"));
    }

    public async Task MarkSingleNotificationReadAsync(int notificationId)
    {
        await _context.Notifications
            .Where(n => n.Id == notificationId && n.Status == "new")
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.Status, "read"));
    }

    public async Task UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }

    public async Task<User?> ValidateCredentialsAsync(string login, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Login == login);
        
        if (user == null)
            return null;

        if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return user;

        return null;
    }

    public async Task<List<Repositories.UserPlatformSubscriptionDto>> GetUserPlatformSubscriptionsAsync(int userId)
    {
        return await _context.UserPlatformSubscriptions
            .Where(ups => ups.UserId == userId)
            .Select(ups => new UserPlatformSubscriptionDto
            {
                PlatformId = ups.PlatformId,
                PlatformName = ups.Platform!.Name,
                Enabled = ups.Enabled
            })
            .ToListAsync();
    }

    public async Task<List<Repositories.UserNotificationSettingDto>> GetUserNotificationSettingsAsync(int userId)
    {
        return await _context.UserNotificationSettings
            .Where(uns => uns.UserId == userId)
            .Select(ups => new UserNotificationSettingDto
            {
                ChannelId = ups.ChannelId,
                ChannelName = ups.Channel!.Name,
                Enabled = ups.Enabled
            })
            .ToListAsync();
    }

    public async Task<PasswordResetToken> CreatePasswordResetTokenAsync(PasswordResetToken token)
    {
        _context.PasswordResetTokens.Add(token);
        await _context.SaveChangesAsync();
        return token;
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token)
    {
        return await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token);
    }

    public async Task InvalidatePasswordResetTokenAsync(PasswordResetToken token)
    {
        token.Used = true;
        await _context.SaveChangesAsync();
    }
}

public class UserPlatformSubscriptionDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class UserNotificationSettingDto
{
    public int ChannelId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
