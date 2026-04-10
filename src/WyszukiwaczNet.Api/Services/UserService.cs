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
            return (false, "U¿ytkownik o tym loginie ju¿ istnieje.");

        if (!string.IsNullOrEmpty(request.Email))
        {
            existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
                return (false, "U¿ytkownik o tym adresie e-mail ju¿ istnieje.");
        }

        if (!string.IsNullOrEmpty(request.Phone))
        {
            existingUser = await _userRepository.GetByPhoneAsync(request.Phone);
            if (existingUser != null)
                return (false, "U¿ytkownik o tym numerze telefonu ju¿ istnieje.");
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

        return (true, "U¿ytkownik zosta³ pomyœlnie zarejestrowany.");
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await _userRepository.ValidateCredentialsAsync(request.Login, request.Password);
        
        if (user == null)
            return new LoginResponse(false, null, "Nieprawid³owe dane logowania.");

        if (!user.IsActive)
            return new LoginResponse(false, null, "Konto u¿ytkownika jest nieaktywne.");

        var token = _jwtService.GenerateToken(user.Id, user.Email);
        return new LoginResponse(true, token, "Logowanie zakoñczone sukcesem.");
    }

    public async Task<UserResponse> GetUserByEmailAsync(string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        
        if (user == null)
            return new UserResponse(false, null, "Nie znaleziono u¿ytkownika o podanym adresie e-mail.");

        return new UserResponse(true, user.Id, $"Znaleziono u¿ytkownika z adresem e-mail: {user.Email}");
    }

    public async Task<UserResponse> GetUserByLoginAsync(string login)
    {
        var user = await _userRepository.GetByLoginAsync(login);

        if (user == null)
            return new UserResponse(false, null, "Nie znaleziono u¿ytkownika o podanym loginie.");

        return new UserResponse(true, user.Id, $"Znaleziono u¿ytkownika z loginem: {user.Login}.");
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
            return (false, "U¿ytkownik o podanym ID nie istnieje.");

        var platforms = await _offerRepository.GetAllPlatformsAsync();

        if (platforms.Select(q => q.Id).Contains(subscription.PlatformId))
        {
            await _userRepository.CreateOrUpdatePlatformSubscriptionAsync(subscription);
        }
        else
            return (false, "Nie znaleziono platformy o podanym ID.");

         return (true, "Subskrypcja platformy pomyœlnie zaktualizowana.");
    }

    public async Task<List<UserNotificationSettingDto>> GetUserNotificationSettingsAsync(int userId)
    {
        return await _userRepository.GetUserNotificationSettingsAsync(userId);
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
            return (false, "U¿ytkownik o podanym ID nie istnieje.");

        var notificationChannels = await _offerRepository.GetAllNotificationChannelsAsync();

        if (notificationChannels.Select(q => q.Id).Contains(setting.ChannelId))
        {
            await _userRepository.CreateOrUpdateNotificationSettingAsync(setting);
        }
        else
            return (false, "Nie znaleziono kana³u o podanym ID.");
        
        return (true, "Ustawienia powiadomieñ zosta³y pomyœlnie zaktualizowane.");
    }
}
