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
    Task<UserNotificationConfigDto?> GetNotificationConfigAsync(int userId, string? category = null);
    Task<List<UserNotificationConfigDto>> GetAllNotificationConfigsAsync(int userId);
    Task<(bool Success, string? Message)> SaveNotificationConfigAsync(SaveNotificationConfigRequest request);
    Task<bool> SetNotificationConfigEnabledAsync(int userId, string? category, bool enabled);
    Task<(List<NotificationFeedItemDto> Items, int TotalCount)> GetNotificationFeedAsync(int userId, int page = 0, int pageSize = 30, string? category = null);
    Task<int> GetUnreadNotificationCountAsync(int userId);
    Task MarkNotificationsReadAsync(int userId);
    Task MarkSingleNotificationReadAsync(int notificationId);
    Task<UserProfileResponse> GetUserProfileAsync(int userId);
    Task<(bool Success, string? Message)> UpdatePasswordAsync(UpdatePasswordRequest request);
    Task<(bool Success, string? Message)> UpdateEmailAsync(UpdateEmailRequest request);
    Task<(bool Success, string? Message)> UpdatePhoneAsync(UpdatePhoneRequest request);
    Task<(bool Success, string? Message)> ForgotPasswordAsync(string email, string frontendBaseUrl);
    Task<(bool Success, string? Message)> ResetPasswordByTokenAsync(string token, string newPassword);
}

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IOfferRepository _offerRepository;
    private readonly IEmailService _emailService;

    public UserService(IUserRepository userRepository, IJwtService jwtService, IOfferRepository offerRepository, IEmailService emailService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _offerRepository = offerRepository;
        _emailService = emailService;
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

    public async Task<UserNotificationConfigDto?> GetNotificationConfigAsync(int userId, string? category = null)
    {
        var config = await _userRepository.GetNotificationConfigAsync(userId, category);
        if (config == null) return null;
        return new UserNotificationConfigDto
        {
            Phrase = config.Phrase,
            RequestCount = config.RequestCount,
            Schedule = config.Schedule,
            Category = config.Category,
            Enabled = config.Enabled,
            FiltersJson = config.FiltersJson
        };
    }

    public async Task<List<UserNotificationConfigDto>> GetAllNotificationConfigsAsync(int userId)
    {
        var configs = await _userRepository.GetAllNotificationConfigsAsync(userId);
        return configs.Select(c => new UserNotificationConfigDto
        {
            Phrase = c.Phrase,
            RequestCount = c.RequestCount,
            Schedule = c.Schedule,
            Category = c.Category,
            Enabled = c.Enabled,
            FiltersJson = c.FiltersJson
        }).ToList();
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
            Schedule = request.Schedule,
            Category = request.Category,
            Enabled = true,
            FiltersJson = request.FiltersJson
        };

        await _userRepository.SaveNotificationConfigAsync(config);
        return (true, "Konfiguracja powiadomień zapisana.");
    }

    public async Task<bool> SetNotificationConfigEnabledAsync(int userId, string? category, bool enabled)
    {
        return await _userRepository.SetNotificationConfigEnabledAsync(userId, category, enabled);
    }

    public async Task<(List<NotificationFeedItemDto> Items, int TotalCount)> GetNotificationFeedAsync(int userId, int page = 0, int pageSize = 30, string? category = null)
    {
        var (notifications, total) = await _userRepository.GetNotificationFeedAsync(userId, page, pageSize, category);
        var items = notifications.Select(n => new NotificationFeedItemDto
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
        return (items, total);
    }

    public async Task<int> GetUnreadNotificationCountAsync(int userId)
    {
        return await _userRepository.GetUnreadNotificationCountAsync(userId);
    }

    public async Task MarkNotificationsReadAsync(int userId)
    {
        await _userRepository.MarkNotificationsReadAsync(userId);
    }

    public async Task MarkSingleNotificationReadAsync(int notificationId)
    {
        await _userRepository.MarkSingleNotificationReadAsync(notificationId);
    }

    public async Task<UserProfileResponse> GetUserProfileAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            return new UserProfileResponse(false, null, null, null, null, "Nie znaleziono użytkownika.");
        return new UserProfileResponse(true, user.Id, user.Login, user.Email, user.Phone, null);
    }

    public async Task<(bool Success, string? Message)> UpdatePasswordAsync(UpdatePasswordRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) return (false, "Nie znaleziono użytkownika.");
        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return (false, "Nieprawidłowe aktualne hasło.");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdateAsync(user);
        return (true, "Hasło zostało zmienione.");
    }

    public async Task<(bool Success, string? Message)> UpdateEmailAsync(UpdateEmailRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) return (false, "Nie znaleziono użytkownika.");
        var existing = await _userRepository.GetByEmailAsync(request.NewEmail);
        if (existing != null && existing.Id != request.UserId)
            return (false, "Podany adres email jest już zajęty.");
        user.Email = request.NewEmail;
        await _userRepository.UpdateAsync(user);
        return (true, "Adres email został zmieniony.");
    }

    public async Task<(bool Success, string? Message)> UpdatePhoneAsync(UpdatePhoneRequest request)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) return (false, "Nie znaleziono użytkownika.");
        if (!string.IsNullOrEmpty(request.NewPhone))
        {
            var existing = await _userRepository.GetByPhoneAsync(request.NewPhone);
            if (existing != null && existing.Id != request.UserId)
                return (false, "Podany numer telefonu jest już zajęty.");
        }
        user.Phone = request.NewPhone;
        await _userRepository.UpdateAsync(user);
        return (true, "Numer telefonu został zmieniony.");
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

    public async Task<(bool Success, string? Message)> ForgotPasswordAsync(string email, string frontendBaseUrl)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
            return (true, "Jeśli konto z tym adresem istnieje, wyślemy link do resetu hasła.");

        var token = Guid.NewGuid().ToString("N");
        var resetToken = new Entities.PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };
        await _userRepository.CreatePasswordResetTokenAsync(resetToken);

        var resetLink = $"{frontendBaseUrl.TrimEnd('/')}/reset-password?token={token}";
        var body = $@"<p>Otrzymaliśmy prośbę o reset hasła do Twojego konta w Wyszukiwaczu.</p>
<p>Kliknij poniższy link, aby zresetować hasło (link ważny przez 1 godzinę):</p>
<p><a href=""{resetLink}"">{resetLink}</a></p>
<p>Jeśli nie prosiłeś o reset hasła, zignoruj tę wiadomość.</p>";

        await _emailService.SendEmailAsync(user.Email, "Reset hasła – Wyszukiwacz", body);
        return (true, "Jeśli konto z tym adresem istnieje, wyślemy link do resetu hasła.");
    }

    public async Task<(bool Success, string? Message)> ResetPasswordByTokenAsync(string token, string newPassword)
    {
        var resetToken = await _userRepository.GetPasswordResetTokenAsync(token);
        if (resetToken == null || resetToken.Used || resetToken.ExpiresAt < DateTime.UtcNow)
            return (false, "Link jest nieprawidłowy lub wygasł.");

        var user = await _userRepository.GetByIdAsync(resetToken.UserId);
        if (user == null) return (false, "Nie znaleziono użytkownika.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _userRepository.UpdateAsync(user);
        await _userRepository.InvalidatePasswordResetTokenAsync(resetToken);

        return (true, "Hasło zostało zmienione. Możesz się teraz zalogować.");
    }
}
