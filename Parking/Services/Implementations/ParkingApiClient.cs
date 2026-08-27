using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
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
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public string BaseUrl { get; set; } = "https://localhost:7023";

    public ParkingApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/health", cts.Token);
            if (response.IsSuccessStatusCode) return true;
        }
        catch { }

        var fallbackUrl = BaseUrl.Contains("7023") ? "http://localhost:5135" : "https://localhost:7023";
        try
        {
            using var ctsFallback = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync($"{fallbackUrl}/api/health", ctsFallback.Token);
            if (response.IsSuccessStatusCode)
            {
                BaseUrl = fallbackUrl;
                return true;
            }
        }
        catch { }

        return false;
    }

    public async Task<BootstrapSyncResponse?> GetBootstrapAsync(int? branchId = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            var url = branchId.HasValue 
                ? $"{BaseUrl}/api/sync/bootstrap?branchId={branchId.Value}" 
                : $"{BaseUrl}/api/sync/bootstrap";

            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cts.Token);
                return JsonSerializer.Deserialize<BootstrapSyncResponse>(content, JsonOptions);
            }
            System.Diagnostics.Debug.WriteLine($"[ParkingApiClient] GetBootstrapAsync HTTP Error: {response.StatusCode}");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ParkingApiClient] GetBootstrapAsync Exception: {ex.Message} -> {ex.StackTrace}");
            return null;
        }
    }

    public async Task<ParkingTicket?> CheckInAsync(CheckInApiRequest request)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/tickets/check-in", request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ParkingTicket>(JsonOptions, cts.Token);
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/tickets/check-out", request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ParkingTicket>(JsonOptions, cts.Token);
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/analytics/daily-summary", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FinancialSummary>(JsonOptions, cts.Token);
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
        var request = new LoginApiRequest { Username = username, Password = password };

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/auth/login", request, cts.Token);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginApiResponse>(JsonOptions, cts.Token);
                if (result != null && !string.IsNullOrEmpty(result.Token))
                {
                    SetAuthToken(result.Token);
                }
                return result;
            }
            else
            {
                var errorResult = await response.Content.ReadFromJsonAsync<LoginApiResponse>(JsonOptions, cts.Token);
                return errorResult ?? new LoginApiResponse 
                { 
                    Success = false, 
                    ErrorMessage = "Credenciales incorrectas o usuario inactivo." 
                };
            }
        }
        catch (HttpRequestException)
        {
            var fallbackUrl = BaseUrl.Contains("7023") ? "http://localhost:5135" : "https://localhost:7023";
            try
            {
                using var ctsFallback = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                var response = await _httpClient.PostAsJsonAsync($"{fallbackUrl}/api/auth/login", request, ctsFallback.Token);
                if (response.IsSuccessStatusCode)
                {
                    BaseUrl = fallbackUrl;
                    var result = await response.Content.ReadFromJsonAsync<LoginApiResponse>(JsonOptions, ctsFallback.Token);
                    if (result != null && !string.IsNullOrEmpty(result.Token))
                    {
                        SetAuthToken(result.Token);
                    }
                    return result;
                }
                else
                {
                    var errorResult = await response.Content.ReadFromJsonAsync<LoginApiResponse>(JsonOptions, ctsFallback.Token);
                    return errorResult ?? new LoginApiResponse { Success = false, ErrorMessage = "Credenciales incorrectas o usuario inactivo." };
                }
            }
            catch { }
        }
        catch { }

        return null;
    }

    public async Task LogoutAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await _httpClient.PostAsync($"{BaseUrl}/api/auth/logout", null, cts.Token);
        }
        catch { }
        finally
        {
            ClearAuthToken();
        }
    }

    public async Task<WorkShift?> OpenShiftAsync(OpenShiftApiRequest request)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/shifts/open", request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WorkShift>(JsonOptions, cts.Token);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<WorkShift?> GetActiveShiftAsync(int? userId = null, int? branchId = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var queryParams = new List<string>();
            if (userId.HasValue) queryParams.Add($"userId={userId.Value}");
            if (branchId.HasValue) queryParams.Add($"branchId={branchId.Value}");
            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;

            var url = $"{BaseUrl}/api/shifts/active{queryString}";
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WorkShift>(JsonOptions, cts.Token);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<ShiftSummaryModel?> GetShiftSummaryAsync(Guid shiftId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/shifts/summary/{shiftId}", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ShiftSummaryModel>(JsonOptions, cts.Token);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<WorkShift?> CloseShiftAsync(CloseShiftApiRequest request)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/shifts/close", request, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WorkShift>(JsonOptions, cts.Token);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<WorkShift>> GetShiftHistoryAsync(DateTime? fromDate = null, DateTime? toDate = null, int? branchId = null)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            var queryParams = new List<string>();
            if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:O}");
            if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:O}");
            if (branchId.HasValue) queryParams.Add($"branchId={branchId.Value}");
            var queryString = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty;

            var url = $"{BaseUrl}/api/shifts/history{queryString}";
            var response = await _httpClient.GetAsync(url, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<WorkShift>>(JsonOptions, cts.Token);
                return list ?? new List<WorkShift>();
            }
            return new List<WorkShift>();
        }
        catch
        {
            return new List<WorkShift>();
        }
    }

    public async Task<IReadOnlyList<ApiUserSyncDto>> GetBranchUsersAsync(int branchId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/branches/{branchId}/users", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<ApiUserSyncDto>>(JsonOptions, cts.Token);
                return list ?? new List<ApiUserSyncDto>();
            }
            return new List<ApiUserSyncDto>();
        }
        catch
        {
            return new List<ApiUserSyncDto>();
        }
    }

    public async Task<List<string>> GetRolePermissionsAsync(int roleId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/api/RoleActions/PermissionRole/{roleId}", cts.Token);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<List<ActionRoleDto>>(JsonOptions, cts.Token);
                if (list != null)
                {
                    return list
                        .Where(a => a.IsActive && !string.IsNullOrWhiteSpace(a.ActionName))
                        .Select(a => a.ActionName!)
                        .ToList();
                }
            }
            return new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }
}
