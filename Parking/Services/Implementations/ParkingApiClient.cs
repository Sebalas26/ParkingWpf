using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class ParkingApiClient : IApiClientService
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string BaseUrl { get; set; } = "http://localhost:5135";

    public ParkingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public void SetAuthToken(string token)
    {
        if (!string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void ClearAuthToken()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<bool> PingAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<BootstrapSyncResponse?> GetBootstrapAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/sync/bootstrap");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<BootstrapSyncResponse>(JsonOptions);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ParkingTicket?> CheckInAsync(CheckInApiRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/tickets/check-in", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ParkingTicket>(JsonOptions);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ParkingTicket?> CheckOutAsync(CheckOutApiRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/tickets/check-out", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ParkingTicket>(JsonOptions);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<FinancialSummary?> GetFinancialSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/analytics/daily-summary");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FinancialSummary>(JsonOptions);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<LoginApiResponse?> LoginAsync(string username, string password)
    {
        try
        {
            var request = new LoginApiRequest { Username = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/login", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginApiResponse>(JsonOptions);
                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    SetAuthToken(result.Token);
                }
                return result;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            await _httpClient.PostAsync($"{BaseUrl}/api/auth/logout", null);
        }
        catch
        {
        }
        finally
        {
            ClearAuthToken();
        }
    }
}
