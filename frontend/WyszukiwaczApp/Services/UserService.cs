using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Services;

public class UserService
{
    private readonly HttpClient _httpClient;

    public UserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5012/api/");
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<LoginResponse?> LoginAsync(string login, string password)
    {
        try
        {
            var request = new LoginRequest { Login = login, Password = password };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("users/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Login failed: {response.StatusCode}"
                };
            }

            return JsonConvert.DeserializeObject<LoginResponse>(responseContent);
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

    public async Task<LoginResponse?> RegisterAsync(string email, string? phone, string password)
    {
        try
        {
            var request = new RegisterUserRequest
            {
                Email = email,
                Phone = phone,
                Password = password
            };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("users/registerUser", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Registration failed: {response.StatusCode}"
                };
            }

            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            return new LoginResponse
            {
                Success = result?.success ?? true,
                Message = result?.message?.ToString() ?? "Registration successful"
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

    public async Task<LoginResponse?> RegisterAsync(RegisterUserRequest request)
    {
        return await RegisterAsync(request.Email, request.Phone, request.Password);
    }

    public async Task<UserResponse?> GetUserAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"users/{userId}");
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
            var response = await _httpClient.GetAsync($"users/{userId}/notifications");
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
            var response = await _httpClient.GetAsync($"users/{userId}/platforms");
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

            var response = await _httpClient.PostAsync("users/platforms", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
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

            var response = await _httpClient.PostAsync("users/notifications", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
