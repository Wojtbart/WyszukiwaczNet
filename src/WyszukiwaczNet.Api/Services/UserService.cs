using WyszukiwaczNet.Api.DTOs;
using WyszukiwaczNet.Api.Entities;
using WyszukiwaczNet.Api.Repositories;

namespace WyszukiwaczNet.Api.Services;

public interface IUserService
{
    Task<(bool Success, string? Message)> RegisterAsync(RegisterUserRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<UserResponse> GetUserByEmailAsync(string email);
    Task<UserResponse> GetUserByLoginAsync(string email);
    Task<List<UserPlatformSubscriptionDto>> GetUserPlatformSubscriptionsAsync(int userId);
    Task<(bool Success, string? Message)> UpdatePlatformSubscriptionAsync(PlatformSubscriptionRequest request);
    Task<List<UserNotificationSettingDto>> GetUserNotificationSettingsAsync(int userId);
    Task<(bool Success, string? Message)> UpdateNotificationSettingAsync(NotificationSettingRequest request);
    Task<UserNotificationConfigDto?> GetNotificationConfigAsync(int userId);
    Task<(bool Success, string? Message)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request);
    Task<List<NotificationFeedItemDto>> GetNotificationFeedAsync(int userId, int limit = 100);
    Task<int> GetUnreadNotificationCountAsync(int userId);
    Task MarkNotificationsReadAsync(int userId);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IOfferRepository _offerRepository;

    public UserService(IUserRepository userRepository, IJwtService jwtService, IOfferRepository offerRepository)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _offerRepository = offerRepository;
    }

    public async Task<(bool Success, string? Message)> RegisterAsync(RegisterUserRequest request)
    {
        var existingUser = await _userRepository.GetByLoginAsync(request.Login);
        if (existingUser != null)
            return (false, "Uzytkownik o tym loginie juz istnieje.");

        if (!string.IsNullOrEmpty(request.Email))
        {
            existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
                return (false, "Uzytkownik o tym adresie email juz istnieje.");
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            existingUser = await _userRepository.GetByPhoneAsync(request.Phone);
            if (existingUser != null)
                return (false, "Uzytkownik o tym numerze telefonu juz istnieje.");
        }

        var user = new User
        {
            Email = request.Email,
            Phone = request.Phone,
            Name = request.Name,
            Surname = request.Surname,
            Login = request.Login,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.CreateAsync(user);

        return (true, "Użytkownik został pomyślnie zarejestrowany.");
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.ValidateCredentialsAsync(request.Login, request.Password);
        
        if (user == null)
            return new LoginResponse(false, null, "Nieprawidłowe dane logowania.");

        if (!user.IsActive)
            return new LoginResponse(false, null, "Konto użytkownika jest nieaktywne.");

        var token = _jwtService.GenerateToken(user.Id, user.Email);
        return new LoginResponse(true, token, "Logowanie zakończone sukcesem.");
    }

    public async Task<UserResponse> GetUserByEmailAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        
        if (user == null)
            return new UserResponse(false, null, "Nie znaleziono użytkownika o podanym adresie email.");

        return new UserResponse(true, user.Id, $"Znaleziono użytkownika z adresem e-mail: {user.Email}");
    }

    public async Task<UserResponse> GetUserByLoginAsync(string login)
    {
        var user = await _userRepository.GetByLoginAsync(login);

        if (user == null)
            return new UserResponse(false, null, "Nie znaleziono użytkownika o podanym loginie.");

        return new UserResponse(true, user.Id, $"Znaleziono użytkownika z loginem: {user.Login}.");
    }

    public async Task<List<UserPlatformSubscriptionDto>> GetUserPlatformSubscriptionsAsync(int userId)
    {
        return await _userRepository.GetUserPlatformSubscriptionsAsync(userId);
    }

    public async Task<(bool Success, string? Message)> UpdatePlatformSubscriptionAsync(PlatformSubscriptionRequest request)
    {
        var subscription = new UserPlatformSubscription
        {
            UserId = request.UserId,
            PlatformId = request.PlatformId,
            Enabled = request.Enabled
        };

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return (false, "Użytkownik o podanym ID nie istnieje.");

        var platforms = await _offerRepository.GetAllPlatformsAsync();

        if (platforms.Select(q => q.Id).Contains(subscription.PlatformId))
        {
            await _userRepository.CreateOrUpdatePlatformSubscriptionAsync(subscription);
        }
        else
            return (false, "Nie znaleziono platformy o podanym ID.");

         return (true, "Subskrypcja platformy pomyślnie zaktualizowana.");
    }

    public async Task<List<UserNotificationSettingDto>> GetUserNotificationSettingsAsync(int userId)
    {
        return await _userRepository.GetUserNotificationSettingsAsync(userId);
    }

    public async Task<UserNotificationConfigDto?> GetNotificationConfigAsync(int userId)
    {
        var config = await _userRepository.GetNotificationConfigAsync(userId);
        if (config == null) return null;
        return new UserNotificationConfigDto
        {
            Phrase = config.Phrase,
            RequestCount = config.RequestCount,
            Schedule = config.Schedule
        };
    }

    public async Task<(bool Success, string? Message)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) return (false, "Użytkownik o podanym ID nie istnieje.");

        var config = new UserNotificationConfig
        {
            UserId = request.UserId,
            Phrase = request.Phrase,
            RequestCount = request.RequestCount,
            Schedule = request.Schedule
        };

        await _userRepository.SaveNotificationConfigAsync(config);
        return (true, "Konfiguracja powiadomień zapisana.");
    }

    public async Task<List<NotificationFeedItemDto>> GetNotificationFeedAsync(int userId, int limit = 100)
    {
        var items = await _userRepository.GetNotificationFeedAsync(userId, limit);
        return items.Select(n => new NotificationFeedItemDto
        {
            Id = n.Id,
            OfferId = n.OfferId,
            OfferTitle = n.Offer?.Title,
            OfferPrice = n.Offer?.Price,
            OfferUrl = n.Offer?.Url,
            OfferLocation = n.Offer?.Location,
            PlatformName = n.Offer?.Platform?.Name,
            Status = n.Status,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        return await _userRepository.GetUnreadNotificationCountAsync(userId);
    }

    public async Task MarkNotificationsReadAsync(int userId)
    {
        await _userRepository.MarkNotificationsReadAsync(userId);
    }

    public async Task<(bool Success, string? Message)> UpdateNotificationSettingAsync(NotificationSettingRequest request)
    {
        var setting = new UserNotificationSetting
        {
            UserId = request.UserId,
            ChannelId = request.ChannelId,
            Enabled = request.Enabled
        };

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return (false, "U�ytkownik o podanym ID nie istnieje.");

        var notificationChannels = await _offerRepository.GetAllNotificationChannelsAsync();

        if (notificationChannels.Select(q => q.Id).Contains(setting.ChannelId))
        {
            await _userRepository.CreateOrUpdateNotificationSettingAsync(setting);
        }
        else
            return (false, "Nie znaleziono kanału o podanym ID.");
        
        return (true, "Ustawienia powiadomień zostały pomyślnie zaktualizowane.");
    }
}
