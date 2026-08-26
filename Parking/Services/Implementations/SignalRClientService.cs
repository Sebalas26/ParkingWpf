using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SignalRClientService : ISignalRClientService, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private HubConnection? _hubConnection;
    private int? _currentBranchId;
    private bool _isStarting;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<ConfigNotificationDto>? ConfigUpdateRequired;
    public event Action<bool>? ConnectionStatusChanged;

    public SignalRClientService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task StartAsync()
    {
        if (_hubConnection != null && _hubConnection.State == HubConnectionState.Connected)
        {
            return;
        }

        if (_isStarting) return;

        try
        {
            _isStarting = true;

            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5135";
            var hubUrl = $"{apiBaseUrl.TrimEnd('/')}/hubs/parking";

            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
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

            await _hubConnection.StartAsync();
            ConnectionStatusChanged?.Invoke(true);

            if (_currentBranchId.HasValue)
            {
                await JoinBranchGroupAsync(_currentBranchId.Value);
            }
        }
        catch (Exception)
        {
            // En modo offline o servidor apagado, continuar de forma silenciosa sin romper el inicio de la app
            ConnectionStatusChanged?.Invoke(false);
        }
        finally
        {
            _isStarting = false;
        }
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

    public async Task SetCurrentBranchAsync(int branchId)
    {
        _currentBranchId = branchId;
        if (IsConnected)
        {
            await JoinBranchGroupAsync(branchId);
        }
        else if (_hubConnection == null || _hubConnection.State == HubConnectionState.Disconnected)
        {
            _ = StartAsync();
        }
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
    }
}
