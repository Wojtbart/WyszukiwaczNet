using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Proxies;

public class HistoryProxy
{
    private readonly HttpClient _httpClient;

    public HistoryProxy(HttpClient httpClient, ApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
    }

    public async Task<HistoryApiResponse?> getHistory(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"offers/history/{userId}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                return new HistoryApiResponse { success = false, Data = new List<HistoryModel>() };

            var historyItems = JsonConvert.DeserializeObject<List<HistoryItem>>(content) ?? new List<HistoryItem>();
            var models = historyItems.Select(h => new HistoryModel
            {
                title = h.Title ?? "",
                price = h.Price?.ToString() ?? "",
                image = h.ImageUrl ?? "",
                link = h.Url ?? "",
                createDate = h.CreatedAt
            }).ToList();

            return new HistoryApiResponse { success = true, Data = models };
        }
        catch
        {
            return new HistoryApiResponse { success = false, Data = new List<HistoryModel>() };
        }
    }
}

public class HistoryApiResponse
{
    public bool success { get; set; }
    public List<HistoryModel>? Data { get; set; }
}

public class HistoryModel
{
    public string title { get; set; } = string.Empty;
    public string price { get; set; } = string.Empty;
    public string image { get; set; } = string.Empty;
    public string link { get; set; } = string.Empty;
    public DateTime createDate { get; set; }
}

public class HistoryItem
{
    public string? Title { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
}
