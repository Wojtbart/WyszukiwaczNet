using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Services;

public class UserService
{
    private readonly HttpClient _httpClient;
    private readonly AuthState _authState;

    public UserService(HttpClient httpClient, ApiConfig apiConfig, AuthState authState)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
        _authState = authState;
    }

    private HttpClient HttpC
    {
        get
        {
            _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(_authState.AuthToken)
                ? null
                : new AuthenticationHeaderValue("Bearer", _authState.AuthToken);
            return _httpClient;
        }
    }

    public void SetAuthToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void ClearAuthToken() =>
        _httpClient.DefaultRequestHeaders.Authorization = null;

    public async Task<LoginResponse?> LoginAsync(string login, string password)
    {
        try
        {
            var request = new LoginRequest { Login = login, Password = password };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpC.PostAsync("users/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "Niepoprawne dane logowania"
                };
            }

            return JsonConvert.DeserializeObject<LoginResponse>(responseContent);
        }
        catch (HttpRequestException)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Brak połączenia z serwerem. Serwer jest niedostępny."
            };
        }
        catch (TaskCanceledException)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Przekroczono czas oczekiwania na odpowiedź serwera."
            };
        }
        catch (Exception ex)
        {
            return new LoginResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<LoginResponse?> RegisterAsync(string email, string? phone, string password, string login = "")
    {
        try
        {
            var request = new RegisterUserRequest
            {
                Email = email,
                Phone = phone,
                Password = password,
                Login = login
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpC.PostAsync("users/registerUser", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errResult = JsonConvert.DeserializeObject<dynamic>(responseContent);
                string errMsg = errResult?.message?.ToString() ?? "Rejestracja nie powiodła się";
                return new LoginResponse { Success = false, Message = errMsg };
            }

            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            return new LoginResponse
            {
                Success = result?.success ?? true,
                Message = result?.message?.ToString() ?? "Rejestracja przebiegła pomyślnie"
            };
        }
        catch
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Rejestracja nie powiodła się"
            };
        }
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterUserRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpC.PostAsync("users/registerUser", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var errResult = JsonConvert.DeserializeObject<dynamic>(responseContent);
                string errMsg = errResult?.message?.ToString() ?? "Rejestracja nie powiodła się";
                return new LoginResponse { Success = false, Message = errMsg };
            }

            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            return new LoginResponse
            {
                Success = result?.success ?? true,
                Message = result?.message?.ToString() ?? "Rejestracja zakończona pomyślnie"
            };
        }
        catch
        {
            return new LoginResponse { Success = false, Message = "Rejestracja nie powiodła się" };
        }
    }

    public async Task<UserResponse?> GetUserAsync(int userId)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new UserResponse
                {
                    Success = false,
                    Message = $"Failed: {response.StatusCode}"
                };
            }

            return JsonConvert.DeserializeObject<UserResponse>(content);
        }
        catch (Exception ex)
        {
            return new UserResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<List<UserNotificationSettingDto>?> GetUserNotificationSettingsAsync(int userId)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}/notifications");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<UserNotificationSettingDto>>>(content);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<UserPlatformSubscriptionDto>?> GetUserPlatformSubscriptionsAsync(int userId)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}/platforms");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<UserPlatformSubscriptionDto>>>(content);
            return result?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdatePlatformSubscriptionAsync(int userId, int platformId, bool enabled)
    {
        try
        {
            var request = new PlatformSubscriptionRequest
            {
                UserId = userId,
                PlatformId = platformId,
                Enabled = enabled
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpC.PostAsync("users/platforms", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<UserNotificationConfigDto?> GetNotificationConfigAsync(int userId, string? category = null)
    {
        try
        {
            var url = string.IsNullOrEmpty(category)
                ? $"users/{userId}/config"
                : $"users/{userId}/config?category={Uri.EscapeDataString(category)}";
            var response = await HttpC.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<UserNotificationConfigDto>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<bool> SaveNotificationConfigAsync(SaveNotificationConfigRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpC.PostAsync("users/config", content);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<UserNotificationConfigDto>?> GetAllNotificationConfigsAsync(int userId)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}/configs");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<UserNotificationConfigDto>>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<bool> SetNotificationConfigEnabledAsync(int userId, string? category, bool enabled)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new SetConfigEnabledRequest { UserId = userId, Category = category, Enabled = enabled });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpC.PatchAsync("users/config/enabled", content);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<List<NotificationChannel>?> GetAllChannelsAsync()
    {
        try
        {
            var response = await HttpC.GetAsync("offers/channels");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<NotificationChannel>>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<List<PlatformResponse>?> GetAllPlatformsAsync()
    {
        try
        {
            var response = await HttpC.GetAsync("offers/platforms");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<PlatformResponse>>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<NotificationFeedResponse?> GetNotificationFeedAsync(int userId, int page = 0, int pageSize = 30)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}/feed?page={page}&pageSize={pageSize}");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return JsonConvert.DeserializeObject<NotificationFeedResponse>(content);
        }
        catch { return null; }
    }

    public async Task MarkNotificationsReadAsync(int userId)
    {
        try
        {
            await HttpC.PostAsync($"users/{userId}/feed/read", null);
        }
        catch { }
    }

    public async Task MarkNotificationReadAsync(int userId, int notificationId)
    {
        try
        {
            await HttpC.PostAsync($"users/{userId}/feed/{notificationId}/read", null);
        }
        catch { }
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(int userId)
    {
        try
        {
            var response = await HttpC.GetAsync($"users/{userId}");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return JsonConvert.DeserializeObject<UserProfileDto>(content);
        }
        catch { return null; }
    }

    public async Task<(bool Success, string? Message)> UpdatePasswordAsync(int userId, string currentPassword, string newPassword)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new { UserId = userId, CurrentPassword = currentPassword, NewPassword = newPassword });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpC.PatchAsync($"users/{userId}/password", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            return (response.IsSuccessStatusCode, result?.message?.ToString());
        }
        catch { return (false, "Błąd połączenia."); }
    }

    public async Task<(bool Success, string? Message)> UpdateEmailAsync(int userId, string newEmail)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new { UserId = userId, NewEmail = newEmail });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpC.PatchAsync($"users/{userId}/email", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            return (response.IsSuccessStatusCode, result?.message?.ToString());
        }
        catch { return (false, "Błąd połączenia."); }
    }

    public async Task<(bool Success, string? Message)> UpdatePhoneAsync(int userId, string? newPhone)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new { UserId = userId, NewPhone = newPhone });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await HttpC.PatchAsync($"users/{userId}/phone", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            return (response.IsSuccessStatusCode, result?.message?.ToString());
        }
        catch { return (false, "Błąd połączenia."); }
    }

    public async Task<(bool Success, string? Message)> ForgotPasswordAsync(string email, string frontendBaseUrl)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new { Email = email, FrontendBaseUrl = frontendBaseUrl });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("users/forgot-password", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            return (true, result?.message?.ToString() ?? "Jeśli konto istnieje, wyślemy link.");
        }
        catch { return (false, "Błąd połączenia z serwerem."); }
    }

    public async Task<(bool Success, string? Message)> ResetPasswordByTokenAsync(string token, string newPassword)
    {
        try
        {
            var json = JsonConvert.SerializeObject(new { Token = token, NewPassword = newPassword });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("users/reset-password", content);
            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            bool success = response.IsSuccessStatusCode;
            return (success, result?.message?.ToString());
        }
        catch { return (false, "Błąd połączenia z serwerem."); }
    }

    public async Task<bool> UpdateNotificationSettingAsync(int userId, int channelId, bool enabled)
    {
        try
        {
            var request = new NotificationSettingRequest
            {
                UserId = userId,
                ChannelId = channelId,
                Enabled = enabled
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await HttpC.PostAsync("users/notifications", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
