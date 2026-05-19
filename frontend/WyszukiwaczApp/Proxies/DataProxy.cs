using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;
using WyszukiwaczApp.Models;

namespace WyszukiwaczApp.Proxies;

public class DataProxy
{
    private readonly HttpClient _httpClient;

    public DataProxy(HttpClient httpClient, ApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
    }

    public async Task<ApiResponse<UserConfigurationData>?> GetUserConfiguration(string username)
    {
        try
        {
            var response = await _httpClient.GetAsync($"users/configuration/{username}");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new ApiResponse<UserConfigurationData> { Success = false, Message = "Nie uda�o si� pobra� konfiguracji" };

            return new ApiResponse<UserConfigurationData> { Success = true, Data = JsonConvert.DeserializeObject<UserConfigurationData>(content) };
        }
        catch
        {
            return new ApiResponse<UserConfigurationData> { Success = false, Message = "yst�pi� b��d podczas pobierania konfiguracji" };
        }
    }

    public async Task<ApiResponse<object>?> SaveConfiguration(NotificationModel model)
    {
        try
        {
            var json = JsonConvert.SerializeObject(model);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("users/configuration", content);

            return new ApiResponse<object> { Success = response.IsSuccessStatusCode };
        }
        catch
        {
            return new ApiResponse<object> { Success = false, Message = "Wyst�pi� b��d podczas zapisywania konfiguracji" };
        }
    }

    public async Task<GetDataResponse?> getData(DataModel model)
    {
        try
        {
            var request = new GetDataRequest
            {
                Websites = model.websites,
                Phrase = model.phrase,
                AdditionalPhrase = model.additional_phrase,
                RequestNumber = int.TryParse(model.request_number, out var num) ? num : 30,
                WorkLocation = model.work_location,
                EmploymentLevel = model.employment_level,
                ContractType = model.contract_type,
                Fuel = model.fuel,
                Gearbox = model.gearbox,
                EngineCapacityFrom = model.engine_capacity_from,
                EngineCapacityTo = model.engine_capacity_to,
                PriceFrom = model.price_from,
                PriceTo = model.price_to,
                AreaFrom = model.area_from,
                AreaTo = model.area_to,
            };

            var response = await _httpClient.PostAsJsonAsync("data/getData", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new GetDataResponse { Success = false };

            var result = JsonConvert.DeserializeObject<GetDataResponse>(content);
            return result ?? new GetDataResponse { Success = false };
        }
        catch
        {
            return new GetDataResponse { Success = false };
        }
    }
}

public class UserConfigurationData
{
    public string? Godzina_Maila { get; set; }
    public int repeat_after_specified_time { get; set; }
    public bool Sms { get; set; }
    public bool Discord { get; set; }
    public bool Email { get; set; }
    public bool olx { get; set; }
    public bool pepper { get; set; }
    public bool amazon { get; set; }
    public bool otoMoto { get; set; }
    public bool otoDom { get; set; }
    public bool sprzedajemy { get; set; }
}

public class NotificationModel
{
    public int user_id { get; set; }
    public string phrase { get; set; } = string.Empty;
    public string? additional_phrase { get; set; }
    public int request_number { get; set; }
    public string? godzina_maila { get; set; }
    public int repeat_after_specified_time { get; set; }
    public bool sms { get; set; }
    public bool discord { get; set; }
    public bool email { get; set; }
    public bool olx { get; set; }
    public bool pepper { get; set; }
    public bool amazon { get; set; }
    public bool otoMoto { get; set; }
    public bool otoDom { get; set; }
    public bool sprzedajemy { get; set; }
}

public class DeleteJobModel
{
    public int user_id { get; set; }
}
