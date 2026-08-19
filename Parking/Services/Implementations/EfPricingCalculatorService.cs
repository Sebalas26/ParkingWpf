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
    private readonly ConcurrentDictionary<VehicleType, VehicleRate> _ratesCache = new();

    public EfPricingCalculatorService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task ReloadRatesAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        var rates = await db.VehicleRates.Where(r => r.IsActive).ToListAsync();
        _ratesCache.Clear();
        foreach (var rate in rates)
        {
            _ratesCache[rate.VehicleType] = rate;
        }
    }

    public async Task<IReadOnlyList<VehicleRate>> GetAllRatesAsync()
    {
        if (_ratesCache.IsEmpty)
        {
            await ReloadRatesAsync();
        }

        return _ratesCache.Values.OrderBy(r => (int)r.VehicleType).ToList();
    }

    public VehicleRate GetRate(VehicleType vehicleType)
    {
        if (_ratesCache.TryGetValue(vehicleType, out var rate))
        {
            return rate;
        }

        return new VehicleRate
        {
            VehicleType = vehicleType,
            DisplayName = vehicleType.ToString(),
            HourRate = 3000m,
            MinuteRate = 50m,
            FullDayRate = 25000m,
            GracePeriodMinutes = 15,
            IconKey = "IconCar"
        };
    }

    public decimal CalculateFee(VehicleType vehicleType, DateTime entryTime, DateTime exitTime)
    {
        var rate = GetRate(vehicleType);
        var duration = exitTime - entryTime;
        if (duration.TotalSeconds < 0)
        {
            return 0m;
        }

        var totalMinutes = duration.TotalMinutes;
        if (totalMinutes <= rate.GracePeriodMinutes)
        {
            return 0m;
        }

        var billableHours = (int)Math.Ceiling(totalMinutes / 60.0);
        if (billableHours < 1)
        {
            billableHours = 1;
        }

        return billableHours * rate.HourRate;
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
