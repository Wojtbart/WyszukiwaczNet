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
    Task<User?> ValidateCredentialsAsync(string login, string password);
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

    //public async Task<List<UserNotificationSetting>> GetUserNotificationSettingsAsync(int userId)
    //{
    //    return await _context.UserNotificationSettings
    //        .Where(uns => uns.UserId == userId)
    //        .ToListAsync();
    //}

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
