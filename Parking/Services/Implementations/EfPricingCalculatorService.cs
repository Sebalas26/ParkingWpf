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
            // 1. Cargar tarifas asignadas a la sede activa
            rates = await db.VehicleRates
                .Where(r => r.IsActive && r.BranchId == currentBranchId.Value)
                .OrderBy(r => r.DisplayName)
                .ToListAsync();

            // 2. Si la sede activa no tiene tarifas propias parametrizadas, fallback a globales
            if (rates.Count == 0)
            {
                rates = await db.VehicleRates
                    .Where(r => r.IsActive && r.BranchId == null)
                    .OrderBy(r => r.DisplayName)
                    .ToListAsync();
            }
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

        // Si tiene tarifa por minuto configurada (> 0), liquidar por minutos exactos
        if (rate.MinuteRate > 0)
        {
            var billableMinutes = (decimal)Math.Max(1, Math.Ceiling(totalMinutes));

            // Si excede 24 horas (1440 min) y tiene tarifa de día completo
            if (rate.FullDayRate > 0 && totalMinutes >= 1440)
            {
                var days = (int)(totalMinutes / 1440);
                var remainingMinutes = (decimal)Math.Ceiling(totalMinutes % 1440);
                var remainingFee = Math.Min(remainingMinutes * rate.MinuteRate, rate.FullDayRate);
                return (days * rate.FullDayRate) + remainingFee;
            }

            var fee = billableMinutes * rate.MinuteRate;
            if (rate.FullDayRate > 0 && fee > rate.FullDayRate)
            {
                return rate.FullDayRate;
            }
            return fee;
        }
        else
        {
            // Cobro por horas redondeadas
            var billableHours = (int)Math.Max(1, Math.Ceiling(Math.Max(0.01, totalMinutes) / 60.0));
            return billableHours * rate.HourRate;
        }
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
