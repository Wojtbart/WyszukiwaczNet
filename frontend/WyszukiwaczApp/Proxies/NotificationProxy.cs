using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Proxies;

public class NotificationProxy
{
    private readonly HttpClient _httpClient;

    public NotificationProxy(HttpClient httpClient, ApiConfig apiConfig)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
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
            return new ApiResponse<object> { Success = false, Message = "Blad podczas planowania zadania" };
        }
    }

    public async Task<UserJobsResponse?> GetJobsForUser(int userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"notifications/jobsForUser/{userId}");
            if (!response.IsSuccessStatusCode) return null;
            var content = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserJobsResponse>(content);
        }
        catch
        {
            return null;
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
                return new DeleteJobResult { success = true, message = "Zadania zostaly pomyslnie usuniete" };
            }
            return new DeleteJobResult { success = false, message = "Nie udalo sie usunac zadan" };
        }
        catch
        {
            return new DeleteJobResult { success = false, message = "Blad podczas usuwania zadan" };
        }
    }

}

public class DeleteJobResult
{
    public bool success { get; set; }
    public string? message { get; set; }
}
