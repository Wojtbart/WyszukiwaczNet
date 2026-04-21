using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Proxies;

public class LoginProxy
{
    private readonly HttpClient _httpClient;

    public LoginProxy(HttpClient httpClient, ApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
    }

    public async Task<UserInfo?> GetUser(string username)
    {
        try
        {
            var response = await _httpClient.GetAsync($"users/getUserByLogin/{username}");
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
                return null;

            return JsonConvert.DeserializeObject<UserInfo>(content);
        }
        catch
        {
            return null;
        }
    }
}

public class UserInfo
{
    [JsonProperty("userId")]
    public int user_id { get; set; }
    public string? email { get; set; }
    public string? login { get; set; }
    public string? phone { get; set; }
}

public class ApiResponseModel<T>
{
    public bool success { get; set; }
    public T? Data { get; set; }
    public string? message { get; set; }
}
