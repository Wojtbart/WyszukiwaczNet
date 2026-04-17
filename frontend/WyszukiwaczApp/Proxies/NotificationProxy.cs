using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Proxies;

public class NotificationProxy
{
    private readonly HttpClient _httpClient;

    public NotificationProxy(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5012/api/");
    }

    public async Task<ApiResponse<object>?> setCronJob(NotificationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("notifications/cronJob", request);
            return new ApiResponse<object> { Success = response.IsSuccessStatusCode };
        }
        catch
        {
            return new ApiResponse<object> { Success = false, Message = "B³¹d podczas planowania zadania" };
        }
    }

    public async Task<DeleteJobResult> DeleteJobs(DeleteJobModel model)
    {
        try
        {
            var json = JsonConvert.SerializeObject(model.user_id);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("notifications/deleteJobsForUser", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new DeleteJobResult { success = true, message = "Zadania zosta³y pomyœlnie usuniête" };
            }
            return new DeleteJobResult { success = false, message = "Nie uda³o siê usun¹æ zadañ" };
        }
        catch
        {
            return new DeleteJobResult { success = false, message = "B³¹d podczas usuwania zadañ" };
        }
    }

}

public class DeleteJobResult
{
    public bool success { get; set; }
    public string? message { get; set; }
}
