using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Services;

public class SubscriptionService
{
    private readonly HttpClient _httpClient;

    public SubscriptionService(HttpClient httpClient, ApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<SubscriptionPlanDto>?> GetPlansAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("subscriptions/plans");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<List<SubscriptionPlanDto>>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<UserPlanDto?> GetUserPlanAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"subscriptions/{userId}/plan");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<UserPlanDto>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<string?> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("subscriptions/checkout", content);
            var body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<dynamic>(body);
            return result?.url?.ToString();
        }
        catch { return null; }
    }

    public async Task<PlanLimitsDto?> GetUserLimitsAsync(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"subscriptions/{userId}/limits");
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            var result = JsonConvert.DeserializeObject<ApiResponse<PlanLimitsDto>>(content);
            return result?.Data;
        }
        catch { return null; }
    }

    public async Task<bool> CancelSubscriptionAsync(int userId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"subscriptions/{userId}/cancel", null);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
