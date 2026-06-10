using System.Net.Http.Headers;
using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;
using WyszukiwaczApp.Services;

namespace WyszukiwaczApp.Proxies;

public class NotificationProxy
{
    private readonly HttpClient _httpClient;
    private readonly AuthState _authState;

    public NotificationProxy(HttpClient httpClient, ApiConfig apiConfig, AuthState authState)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(apiConfig.BaseUrl);
        _authState = authState;
    }

    private void Auth() =>
        _httpClient.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(_authState.AuthToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", _authState.AuthToken);

    public async Task<ApiResponse<object>?> setCronJob(NotificationRequest request)
    {
        try
        {
            Auth();
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
            Auth();
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
            Auth();
            var json = JsonConvert.SerializeObject(model.user_id);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("notifications/deleteJobsForUser", content);

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

    public async Task<DeleteJobResult> DeleteSingleJob(string jobId)
    {
        try
        {
            Auth();
            var encodedId = Uri.EscapeDataString(jobId);
            var response = await _httpClient.DeleteAsync($"notifications/job/{encodedId}");
            return response.IsSuccessStatusCode
                ? new DeleteJobResult { success = true }
                : new DeleteJobResult { success = false, message = "Nie udało się usunąć zadania" };
        }
        catch
        {
            return new DeleteJobResult { success = false, message = "Błąd podczas usuwania zadania" };
        }
    }

}

public class DeleteJobResult
{
    public bool success { get; set; }
    public string? message { get; set; }
}
