using System.Net.Http.Headers;
using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Services;

public class OfferService
{
    private readonly HttpClient _httpClient;
    private readonly AuthState _authState;

    public OfferService(HttpClient httpClient, ApiConfig apiConfig, AuthState authState)
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

    public async Task<List<OfferResponse>?> GetOffersByPlatformAsync(string platform)
    {
        try
        {
            var response = await HttpC.GetAsync($"offers/platform/{platform}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            // Try wrapped ApiResponse first, fall back to plain list
            var wrapped = JsonConvert.DeserializeObject<ApiResponse<List<OfferResponse>>>(content);
            if (wrapped?.Data != null)
                return wrapped.Data;

            return JsonConvert.DeserializeObject<List<OfferResponse>>(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"B��d funkcji GetOffersByPlatformAsync: {ex.Message}");
            return null;
        }
    }

    public async Task<List<OfferResponse>?> GetUserHistoryAsync(int userId, string? platform = null)
    {
        try
        {
            var url = $"offers/history/{userId}";
            if (!string.IsNullOrEmpty(platform)) url += $"?platform={platform}";
            var response = await HttpC.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) return null;
            return JsonConvert.DeserializeObject<List<OfferResponse>>(content);
        }
        catch { return null; }
    }

    public async Task<List<OfferResponse>?> GetOffersAsync(int? platformId = null, string? status = null)
    {
        try
        {
            var url = "offers";
            var queryParams = new List<string>();
            
            if (platformId.HasValue)
                queryParams.Add($"platformId={platformId}");
            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={status}");
            
            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await HttpC.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<OfferResponse>>>(content);
            return apiResponse?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<OfferResponse?> GetOfferByIdAsync(int id)
    {
        try
        {
            var response = await HttpC.GetAsync($"offers/{id}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            return JsonConvert.DeserializeObject<OfferResponse>(content);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<OfferResponse>?> SearchOffersAsync(string phrase, List<string>? websites = null)
    {
        try
        {
            var request = new GetDataRequest
            {
                Phrase = phrase,
                Websites = websites ?? new List<string>(),
                RequestNumber = 1
            };

            var response = await HttpC.PostAsJsonAsync("data/getData", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<OfferResponse>>>(content);
            return apiResponse?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<PlatformResponse>?> GetPlatformsAsync()
    {
        try
        {
            var response = await HttpC.GetAsync("offers/platforms");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<PlatformResponse>>>(content);
            return apiResponse?.Data;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ApiResponse<List<OfferResponse>>?> GetRecentOffersAsync(int count)
    {
        try
        {
            var offers = await GetOffersAsync();
            if (offers == null)
                return new ApiResponse<List<OfferResponse>> { Success = false };

            var recentOffers = offers.Take(count).ToList();
            return new ApiResponse<List<OfferResponse>> { Success = true, Data = recentOffers };
        }
        catch
        {
            return new ApiResponse<List<OfferResponse>> { Success = false };
        }
    }
}
