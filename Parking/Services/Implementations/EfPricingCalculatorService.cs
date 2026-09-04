using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class EfPricingCalculatorService : IPricingCalculatorService
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly ISessionService _sessionService;
    private readonly object _lock = new();
    private readonly List<VehicleRate> _activeBranchRates = new();
    private readonly ConcurrentDictionary<VehicleType, VehicleRate> _ratesCache = new();

    public EfPricingCalculatorService(
        IDbConnectionManager connectionManager, 
        ISyncEngineService syncEngine,
        ISessionService sessionService)
    {
        _connectionManager = connectionManager;
        _sessionService = sessionService;
        syncEngine.DataSynchronized += async () =>
        {
            await ReloadRatesAsync();
        };
        _sessionService.ActiveBranchChanged += async _ =>
        {
            await ReloadRatesAsync();
        };
    }

    public async Task ReloadRatesAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var currentBranchId = _sessionService.CurrentBranch?.Id;

        List<VehicleRate> rates;
        if (currentBranchId.HasValue)
        {
            // Cargar estrictamente las tarifas asignadas a la sede activa (sin fallback global)
            rates = await db.VehicleRates
                .Where(r => r.IsActive && r.BranchId == currentBranchId.Value)
                .OrderBy(r => r.DisplayName)
                .ToListAsync();
        }
        else
        {
            rates = await db.VehicleRates
                .Where(r => r.IsActive)
                .OrderBy(r => r.DisplayName)
                .ToListAsync();
        }

        lock (_lock)
        {
            _activeBranchRates.Clear();
            _activeBranchRates.AddRange(rates);

            _ratesCache.Clear();
            foreach (var rate in rates)
            {
                _ratesCache[rate.VehicleType] = rate;
            }
        }
    }

    public async Task<IReadOnlyList<VehicleRate>> GetAllRatesAsync()
    {
        await ReloadRatesAsync();

        lock (_lock)
        {
            return _activeBranchRates.ToList();
        }
    }

    public VehicleRate? GetRate(VehicleType vehicleType)
    {
        lock (_lock)
        {
            var match = _activeBranchRates.FirstOrDefault(r => r.VehicleType == vehicleType);
            if (match != null) return match;
        }

        if (_ratesCache.TryGetValue(vehicleType, out var rate))
        {
            return rate;
        }

        lock (_lock)
        {
            return _activeBranchRates.FirstOrDefault();
        }
    }

    public decimal CalculateFee(VehicleType vehicleType, DateTime entryTime, DateTime exitTime)
    {
        var rate = GetRate(vehicleType);
        if (rate == null)
        {
            return 0m;
        }

        var duration = exitTime - entryTime;
        if (duration.TotalSeconds < 0)
        {
            return 0m;
        }

        var totalMinutes = duration.TotalMinutes;
        var branch = _sessionService.CurrentBranch;

        // 1. Periodo de gracia
        var grace = rate.GracePeriodMinutes;
        if (totalMinutes <= grace)
        {
            return 0m;
        }

        bool allowMinute = branch == null || branch.AllowChargeByMinute;
        bool allowHour = branch == null || branch.AllowChargeByHour;
        bool allowDay = branch == null || branch.AllowChargeByDay;
        bool allowNight = branch != null && branch.AllowChargeByNight;

        // 2. Caso Nocturno
        if (allowNight && rate.NightRate > 0)
        {
            bool isNightEntry = entryTime.Hour >= 18 || entryTime.Hour < 6;
            bool isNightExit = exitTime.Hour >= 18 || exitTime.Hour < 6;
            if (isNightEntry && isNightExit && totalMinutes >= 360)
            {
                return rate.NightRate;
            }
        }

        // 3. Estancia Multidía (>= 1440 minutos y tarifa plena configurada)
        if (allowDay && rate.FullDayRate > 0 && totalMinutes >= 1440)
        {
            var days = (int)(totalMinutes / 1440);
            var remMinutes = totalMinutes % 1440;
            decimal remFee = 0m;

            if (allowMinute && rate.MinuteRate > 0 && allowHour && rate.HourRate > 0)
            {
                var remH = (int)(remMinutes / 60);
                var remM = (decimal)(remMinutes % 60);
                remFee = (remH * rate.HourRate) + Math.Min(rate.HourRate, remM * rate.MinuteRate);
            }
            else if (allowMinute && rate.MinuteRate > 0)
            {
                remFee = (decimal)Math.Ceiling(remMinutes) * rate.MinuteRate;
            }
            else if (allowHour && rate.HourRate > 0)
            {
                var remBillableHours = (int)Math.Max(1, Math.Ceiling(remMinutes / 60.0));
                remFee = remBillableHours * rate.HourRate;
            }
            else
            {
                remFee = rate.FullDayRate;
            }

            return (days * rate.FullDayRate) + Math.Min(rate.FullDayRate, remFee);
        }

        // 4. Estancia estándar (< 1440 minutos)
        decimal fee = 0m;
        if (allowMinute && rate.MinuteRate > 0 && allowHour && rate.HourRate > 0)
        {
            // Cobro progresivo: horas completas + minutos restantes con tope de la hora
            var hours = (int)(totalMinutes / 60);
            var remMinutes = (decimal)(totalMinutes % 60);
            fee = (hours * rate.HourRate) + Math.Min(rate.HourRate, remMinutes * rate.MinuteRate);
        }
        else if (allowMinute && rate.MinuteRate > 0)
        {
            var billableMinutes = (decimal)Math.Max(1, Math.Ceiling(totalMinutes));
            fee = billableMinutes * rate.MinuteRate;
        }
        else if (allowHour && rate.HourRate > 0)
        {
            var billableHours = (int)Math.Max(1, Math.Ceiling(Math.Max(0.01, totalMinutes) / 60.0));
            fee = billableHours * rate.HourRate;
        }
        else if (allowDay && rate.FullDayRate > 0)
        {
            fee = rate.FullDayRate;
        }

        // Tope de tarifa plena del día
        if (allowDay && rate.FullDayRate > 0 && fee > rate.FullDayRate)
        {
            fee = rate.FullDayRate;
        }

        return fee;
    }

    public async Task UpdateRateAsync(VehicleType vehicleType, decimal hourRate, decimal minuteRate, decimal fullDayRate, int gracePeriodMinutes)
    {
        using var db = _connectionManager.CreateDbContext();
        var rate = await db.VehicleRates.FirstOrDefaultAsync(r => r.VehicleType == vehicleType);
        if (rate != null)
        {
            rate.HourRate = hourRate;
            rate.MinuteRate = minuteRate;
            rate.FullDayRate = fullDayRate;
            rate.GracePeriodMinutes = gracePeriodMinutes;
            rate.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();

            _ratesCache[vehicleType] = rate;
        }
    }
}
