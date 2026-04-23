using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using WyszukiwaczAppDTO;

namespace WyszukiwaczApp.Other;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;

    public ApiClient(IConfiguration configuration, NavigationManager navigationManager)
    {
        _navigationManager = navigationManager;

        var baseAddress = configuration?["ApiBaseUrl"] ?? "http://localhost:5012/api/";
        
        if (!baseAddress.EndsWith("/"))
            baseAddress += "/";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseAddress)
        };
    }

    public void SetAuthToken(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<ApiResponse<T>?> PostAsync<T>(string resource, object? data)
    {
        return await MakeRequestAsync<T>(HttpMethod.Post, resource, data);
    }

    public async Task<ApiResponse<T>?> GetAsync<T>(string resource)
    {
        return await MakeRequestAsync<T>(HttpMethod.Get, resource, null);
    }

    public async Task<ApiResponse<T>?> PutAsync<T>(string resource, object? data)
    {
        return await MakeRequestAsync<T>(HttpMethod.Put, resource, data);
    }

    public async Task<ApiResponse<T>?> DeleteAsync<T>(string resource)
    {
        return await MakeRequestAsync<T>(HttpMethod.Delete, resource, null);
    }

    private async Task<ApiResponse<T>?> MakeRequestAsync<T>(HttpMethod method, string resource, object? data)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(method, resource);
            requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (data != null)
            {
                var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
                requestMessage.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            var responseMessage = await _httpClient.SendAsync(requestMessage);
            var responseContent = await responseMessage.Content.ReadAsStringAsync();

            if (!responseMessage.IsSuccessStatusCode)
            {
                Console.WriteLine($"B³¹d API: {responseMessage.StatusCode} - {responseContent}");
                return new ApiResponse<T>
                {
                    Success = false,
                    Message = $"Error: {responseMessage.StatusCode}"
                };
            }

            return JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request wyj¹tek: {ex.Message}");
            return new ApiResponse<T>
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<LoginResponse?> LoginAsync(string login, string password)
    {
        try
        {
            var request = new { Login = login, Password = password };
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("users/login", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Login failed: {response.StatusCode}"
                };
            }

            return JsonConvert.DeserializeObject<LoginResponse>(responseContent);
        }
        catch (Exception ex)
        {
            return new LoginResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<LoginResponse?> RegisterAsync(RegisterUserRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("users/registerUser", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Registration failed: {response.StatusCode}"
                };
            }

            var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
            return new LoginResponse
            {
                Success = result?.success ?? true,
                Message = result?.message?.ToString() ?? "Registration successful"
            };
        }
        catch (Exception ex)
        {
            return new LoginResponse
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}


