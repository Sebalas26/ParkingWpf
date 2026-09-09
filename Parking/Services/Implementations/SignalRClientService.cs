using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SignalRClientService : ISignalRClientService, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ISessionService? _sessionService;
    private readonly IApiClientService? _apiClient;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;
    private int? _currentBranchId;
    private int? _currentCompanyId;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<ConfigNotificationDto>? ConfigUpdateRequired;
    public event Action<bool>? ConnectionStatusChanged;

    public SignalRClientService(
        IConfiguration configuration,
        ISessionService? sessionService = null,
        IApiClientService? apiClient = null)
    {
        _configuration = configuration;
        _sessionService = sessionService;
        _apiClient = apiClient;
    }

    public Task StartAsync() => EnsureConnectedAsync();

    public Task SetCurrentBranchAsync(int branchId) => EnsureConnectedAsync(branchId, null);

    public async Task EnsureConnectedAsync(int? branchId = null, int? companyId = null)
    {
        if (branchId.HasValue) _currentBranchId = branchId.Value;
        if (companyId.HasValue) _currentCompanyId = companyId.Value;

        if (!_currentBranchId.HasValue && _sessionService?.CurrentBranch != null)
        {
            _currentBranchId = _sessionService.CurrentBranch.Id;
        }

        if (!_currentCompanyId.HasValue && _sessionService != null)
        {
            _currentCompanyId = _sessionService.CurrentCompanyId;
        }

        await _connectionLock.WaitAsync();
        try
        {
            if (_hubConnection == null)
            {
                BuildHubConnection();
            }

            if (_hubConnection != null && _hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
                ConnectionStatusChanged?.Invoke(true);
            }

            if (IsConnected)
            {
                if (_currentBranchId.HasValue)
                {
                    await JoinBranchGroupAsync(_currentBranchId.Value);
                }
                if (_currentCompanyId.HasValue)
                {
                    await JoinCompanyGroupAsync(_currentCompanyId.Value);
                }
            }
        }
        catch (Exception)
        {
            // Modo offline o servidor inalcanzable, continuar de forma silenciosa sin congelar la app
            ConnectionStatusChanged?.Invoke(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private void BuildHubConnection()
    {
        var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5135";
        var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/parking";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling;

                options.AccessTokenProvider = () =>
                {
                    var token = _sessionService?.CurrentUser?.SessionToken ?? _apiClient?.AuthToken ?? string.Empty;
                    return Task.FromResult<string?>(string.IsNullOrWhiteSpace(token) ? null : token);
                };

                options.HttpMessageHandlerFactory = handler =>
                {
                    if (handler is System.Net.Http.HttpClientHandler clientHandler)
                    {
                        clientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                    }
                    return handler;
                };
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        _hubConnection.On<ConfigNotificationDto>("OnConfigUpdateRequired", notification =>
        {
            ConfigUpdateRequired?.Invoke(notification);
        });

        _hubConnection.Reconnected += async connectionId =>
        {
            ConnectionStatusChanged?.Invoke(true);
            if (_currentBranchId.HasValue)
            {
                await JoinBranchGroupAsync(_currentBranchId.Value);
            }
            if (_currentCompanyId.HasValue)
            {
                await JoinCompanyGroupAsync(_currentCompanyId.Value);
            }
        };

        _hubConnection.Reconnecting += ex =>
        {
            ConnectionStatusChanged?.Invoke(false);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += ex =>
        {
            ConnectionStatusChanged?.Invoke(false);
            return Task.CompletedTask;
        };
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.StopAsync();
            }
            catch { }
        }
        ConnectionStatusChanged?.Invoke(false);
    }

    private async Task JoinBranchGroupAsync(int branchId)
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinBranchGroup", branchId);
            }
            catch { }
        }
    }

    private async Task JoinCompanyGroupAsync(int companyId)
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            try
            {
                await _hubConnection.InvokeAsync("JoinCompanyGroup", companyId);
            }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            try
            {
                await _hubConnection.DisposeAsync();
            }
            catch { }
        }
        _connectionLock.Dispose();
    }
}
