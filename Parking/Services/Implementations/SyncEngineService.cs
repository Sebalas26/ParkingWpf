using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SyncEngineService : ISyncEngineService
{
    private readonly IApiClientService _apiClient;
    private readonly IDbConnectionManager _dbManager;
    private bool _isOnline;
    private int _pendingItemsCount;
    private DateTime? _lastSyncTime;

    public event EventHandler<string>? SyncStatusChanged;

    public bool IsOnline => _isOnline;
    public int PendingItemsCount => _pendingItemsCount;
    public DateTime? LastSyncTime => _lastSyncTime;

    public string SyncStatusDescription => _isOnline
        ? (_lastSyncTime.HasValue ? $"API Central Online • Sincronizado ({_lastSyncTime.Value:HH:mm})" : "API Central Online • Sincronizado")
        : (_pendingItemsCount > 0 ? $"Modo Offline Local • {_pendingItemsCount} pendientes" : "Modo Offline Local (Sin Conexión)");

    public SyncEngineService(
        IApiClientService apiClient,
        IDbConnectionManager dbManager)
    {
        _apiClient = apiClient;
        _dbManager = dbManager;
    }

    public async Task<bool> PerformFullSyncAsync()
    {
        var isApiAvailable = await _apiClient.PingAsync();
        _isOnline = isApiAvailable;

        if (isApiAvailable)
        {
            var bootstrap = await _apiClient.GetBootstrapAsync();
            if (bootstrap != null)
            {
                using var db = _dbManager.CreateDbContext();

                // 1. Sincronizar Tarifas
                if (bootstrap.Rates != null && bootstrap.Rates.Count > 0)
                {
                    foreach (var rate in bootstrap.Rates)
                    {
                        var existing = await db.VehicleRates.FirstOrDefaultAsync(r => r.VehicleType == rate.VehicleType);
                        if (existing != null)
                        {
                            existing.HourRate = rate.HourRate;
                            existing.MinuteRate = rate.MinuteRate;
                            existing.FullDayRate = rate.FullDayRate;
                            existing.GracePeriodMinutes = rate.GracePeriodMinutes;
                            existing.DisplayName = rate.DisplayName;
                        }
                        else
                        {
                            db.VehicleRates.Add(rate);
                        }
                    }
                }

                // 2. Sincronizar Comercios Aliados
                if (bootstrap.Stores != null)
                {
                    foreach (var store in bootstrap.Stores)
                    {
                        var existing = await db.Stores.FirstOrDefaultAsync(s => s.StoreId == store.StoreId);
                        if (existing != null)
                        {
                            existing.Name = store.Name;
                            existing.TaxId = store.TaxId;
                            existing.PhoneNumber = store.PhoneNumber;
                            existing.IsActive = store.IsActive;
                        }
                        else
                        {
                            db.Stores.Add(store);
                        }
                    }
                }

                // 3. Sincronizar Convenios
                if (bootstrap.Agreements != null)
                {
                    foreach (var ag in bootstrap.Agreements)
                    {
                        var existing = await db.CommercialAgreements.FirstOrDefaultAsync(a => a.AgreementId == ag.AgreementId);
                        if (existing != null)
                        {
                            existing.Name = ag.Name;
                            existing.StoreId = ag.StoreId;
                            existing.DiscountPercentage = ag.DiscountPercentage;
                            existing.DiscountFixedAmount = ag.DiscountFixedAmount;
                            existing.MaxHoursApplicable = ag.MaxHoursApplicable;
                            existing.MinPurchaseAmount = ag.MinPurchaseAmount;
                            existing.IsActive = ag.IsActive;
                        }
                        else
                        {
                            db.CommercialAgreements.Add(ag);
                        }
                    }
                }

                // 4. Sincronizar Usuarios (para inicio de sesión local)
                if (bootstrap.Users != null)
                {
                    foreach (var user in bootstrap.Users)
                    {
                        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserId == user.UserId || u.Username == user.Username);
                        if (existing != null)
                        {
                            existing.FullName = user.FullName;
                            existing.Email = user.Email;
                            existing.PasswordHash = user.PasswordHash;
                            existing.RoleId = user.RoleId;
                            existing.IsActive = user.IsActive;
                        }
                        else
                        {
                            db.Users.Add(user);
                        }
                    }
                }

                await db.SaveChangesAsync();

                _lastSyncTime = DateTime.Now;
                await ProcessPendingQueueAsync();
            }
        }
        else
        {
            await RefreshPendingCountAsync();
        }

        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
        return _isOnline;
    }

    public async Task EnqueueOfflineCheckInAsync(ParkingTicket ticket)
    {
        using var db = _dbManager.CreateDbContext();
        var pending = new PendingSyncItem
        {
            PendingSyncItemId = Guid.NewGuid(),
            OperationType = "CheckIn",
            PayloadJson = JsonSerializer.Serialize(new CheckInApiRequest
            {
                TicketId = ticket.TicketId,
                TicketNumber = ticket.TicketNumber,
                PlateNumber = ticket.PlateNumber,
                VehicleType = ticket.VehicleType,
                PhoneNumber = ticket.CustomerPhone,
                Notes = ticket.Notes,
                OperatorName = ticket.OperatorName,
                EntryTimeUtc = ticket.EntryTimeUtc
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
    }

    public async Task ClearLocalTicketsMemoryAsync()
    {
        try
        {
            using var db = _dbManager.CreateDbContext();
            db.PendingSyncItems.RemoveRange(db.PendingSyncItems);
            db.TicketDiscounts.RemoveRange(db.TicketDiscounts);
            db.ParkingTickets.RemoveRange(db.ParkingTickets);
            await db.SaveChangesAsync();

            await RefreshPendingCountAsync();
            SyncStatusChanged?.Invoke(this, SyncStatusDescription);
        }
        catch { }
    }

    public async Task EnqueueOfflineCheckOutAsync(ParkingTicket ticket)
    {
        using var db = _dbManager.CreateDbContext();
        var pending = new PendingSyncItem
        {
            PendingSyncItemId = Guid.NewGuid(),
            OperationType = "CheckOut",
            PayloadJson = JsonSerializer.Serialize(new CheckOutApiRequest
            {
                TicketId = ticket.TicketId,
                PaymentMethod = ticket.PaymentMethod ?? PaymentMethod.Cash,
                AmountPaid = ticket.AmountPaid,
                ExitTimeUtc = ticket.ExitTimeUtc ?? DateTime.UtcNow
            }),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        db.PendingSyncItems.Add(pending);
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
    }

    public async Task ProcessPendingQueueAsync()
    {
        if (!_isOnline) return;

        using var db = _dbManager.CreateDbContext();
        var items = await db.PendingSyncItems
            .Where(p => !p.IsProcessed)
            .OrderBy(p => p.CreatedAtUtc)
            .ToListAsync();

        foreach (var item in items)
        {
            try
            {
                if (item.OperationType == "CheckIn")
                {
                    var req = JsonSerializer.Deserialize<CheckInApiRequest>(item.PayloadJson);
                    if (req != null)
                    {
                        var result = await _apiClient.CheckInAsync(req);
                        if (result != null) item.IsProcessed = true;
                    }
                }
                else if (item.OperationType == "CheckOut")
                {
                    var req = JsonSerializer.Deserialize<CheckOutApiRequest>(item.PayloadJson);
                    if (req != null)
                    {
                        var result = await _apiClient.CheckOutAsync(req);
                        if (result != null) item.IsProcessed = true;
                    }
                }
            }
            catch (Exception ex)
            {
                item.RetryCount++;
                item.LastError = ex.Message;
            }
        }

        db.PendingSyncItems.RemoveRange(db.PendingSyncItems.Where(p => p.IsProcessed));
        await db.SaveChangesAsync();

        await RefreshPendingCountAsync();
        SyncStatusChanged?.Invoke(this, SyncStatusDescription);
    }

    private async Task RefreshPendingCountAsync()
    {
        using var db = _dbManager.CreateDbContext();
        _pendingItemsCount = await db.PendingSyncItems.CountAsync(p => !p.IsProcessed);
    }
}
