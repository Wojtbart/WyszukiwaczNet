using System.Net.Http.Json;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Services;

public class NotificationServiceClient
{
    private readonly HttpClient _httpClient;

    public NotificationServiceClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5012/api/");
    }

    public async Task<bool> ScheduleNotificationJobAsync(NotificationRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("notifications/cronJob", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteJobsForUserAsync(int userId)
    {
        try
        {
            var content = new StringContent(userId.ToString(), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("notifications/deleteJobsForUser", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendSmsAsync(string to, string message)
    {
        try
        {
            var request = new { To = to, Message = message };
            var response = await _httpClient.PostAsJsonAsync("notifications/sms", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendDiscordMessageAsync(string message)
    {
        try
        {
            var request = new { Message = message };
            var response = await _httpClient.PostAsJsonAsync("notifications/discord", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var request = new EmailRequest { To = to, Subject = subject, Body = body };
            var response = await _httpClient.PostAsJsonAsync("notifications/email", request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<NotificationChannel>?> GetChannelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("notifications/channels");
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return null;

            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<NotificationChannel>>>(content);
            return apiResponse?.Data;
        }
        catch
        {
            return null;
        }
    }
}

public class EmailRequest
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
