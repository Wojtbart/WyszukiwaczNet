using System.Net.Http.Headers;
using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;
using WyszukiwaczApp.Models;
using WyszukiwaczApp.Services;

namespace WyszukiwaczApp.Proxies;

public class DataProxy
{
    private readonly HttpClient _httpClient;
    private readonly AuthState _authState;

    public DataProxy(HttpClient httpClient, ApiConfig apiConfig, AuthState authState)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
        _authState = authState;
    }

    private void Auth() =>
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(_authState.AuthToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", _authState.AuthToken);

    public async Task<GetDataResponse?> getData(DataModel model)
    {
        try
        {
            Auth();
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
                Rooms = model.rooms,
            };

            var response = await _httpClient.PostAsJsonAsync("data/getData", request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new GetDataResponse { Success = false };

            var result = JsonConvert.DeserializeObject<GetDataResponse>(content);
            return result ?? new GetDataResponse { Success = false };
        }
        catch (HttpRequestException)
        {
            return new GetDataResponse { Success = false, Message = "Brak połączenia z serwerem. Serwer jest niedostępny." };
        }
        catch (TaskCanceledException)
        {
            return new GetDataResponse { Success = false, Message = "Przekroczono czas oczekiwania na odpowiedź serwera." };
        }
        catch
        {
            return new GetDataResponse { Success = false, Message = "Wystąpił błąd podczas komunikacji z serwerem." };
        }
    }
}

public class DeleteJobModel
{
    public int user_id { get; set; }
}
