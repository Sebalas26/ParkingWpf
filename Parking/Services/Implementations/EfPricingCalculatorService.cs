using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Converters;
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

        // Registro de resolución dinámica de nombres de categorías e importe estimado
        VehicleTypeToStringConverter.CustomNameResolver = vt => GetRate(vt)?.DisplayName;
        ParkingTicket.EstimatedFeeCalculator = ticket => CalculateFee(ticket.VehicleType, ticket.EntryTimeUtc, DateTime.UtcNow);

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

    public VehicleRate? GetRate(VehicleType vehicleType, DayOfWeek? dayOfWeek = null)
    {
        lock (_lock)
        {
            if (dayOfWeek.HasValue)
            {
                var specificRate = _activeBranchRates.FirstOrDefault(r => r.VehicleType == vehicleType && r.DayOfWeek == dayOfWeek.Value);
                if (specificRate != null) return specificRate;
            }

            var generalRate = _activeBranchRates.FirstOrDefault(r => r.VehicleType == vehicleType && r.DayOfWeek == null);
            if (generalRate != null) return generalRate;

            var anyMatch = _activeBranchRates.FirstOrDefault(r => r.VehicleType == vehicleType);
            if (anyMatch != null) return anyMatch;
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

    public decimal CalculateFee(VehicleType vehicleType, DateTime entryTime, DateTime exitTime, int discountFreeMinutes = 0, bool isLostTicket = false)
    {
        var cotExitTime = exitTime.AddHours(-5);
        var cotEntryTime = entryTime.AddHours(-5);
        var cotDayOfWeek = cotExitTime.DayOfWeek;

        var rate = GetRate(vehicleType, cotDayOfWeek);
        if (rate == null)
        {
            return 0m;
        }

        var duration = exitTime - entryTime;
        if (duration.TotalSeconds < 0)
        {
            return 0m;
        }

        var rawMinutes = (int)Math.Max(0, duration.TotalMinutes);
        var effectiveMinutes = Math.Max(0, rawMinutes - discountFreeMinutes);
        var branch = _sessionService.CurrentBranch;

        decimal fee = 0m;

        // 1. Periodo de gracia (se evalúa sobre los minutos efectivos con descuento)
        var grace = rate.GracePeriodMinutes;
        if (effectiveMinutes <= grace)
        {
            fee = 0m;
        }
        else
        {
            bool allowMinute = branch == null || branch.AllowChargeByMinute;
            bool allowHour = branch == null || branch.AllowChargeByHour;
            bool allowDay = branch == null || branch.AllowChargeByDay;
            bool allowNight = branch != null && branch.AllowChargeByNight;

            // 2. Tarifa Nocturna (Pernocta) - 100% Data-Driven (Hierarchical VehicleRate -> Branch)
            bool isNightStay = false;
            var nightStart = rate.NightStartTime ?? branch?.NightStartTime;
            var nightEnd = rate.NightEndTime ?? branch?.NightEndTime;
            int minNightStay = rate.NightStayMinMinutes ?? branch?.NightStayMinMinutes ?? 0;
            string? nightDays = branch?.NightApplicableDays;
            bool nightDayApplies = IsDayApplicable(nightDays, cotDayOfWeek);

            if (allowNight && rate.NightRate > 0 && nightStart.HasValue && nightEnd.HasValue && nightDayApplies)
            {
                var start = nightStart.Value;
                var end = nightEnd.Value;

                bool enteredDuringNight;
                bool exitedDuringNightOrMorning;

                if (start > end) // Cruce de medianoche (ej: 20:00 a 06:00)
                {
                    enteredDuringNight = cotEntryTime.TimeOfDay >= start || cotEntryTime.TimeOfDay < end;
                    exitedDuringNightOrMorning = cotExitTime.TimeOfDay >= start || cotExitTime.TimeOfDay < end || cotExitTime.Date > cotEntryTime.Date;
                }
                else // Mismo día (ej: 01:00 a 05:00)
                {
                    enteredDuringNight = cotEntryTime.TimeOfDay >= start && cotEntryTime.TimeOfDay < end;
                    exitedDuringNightOrMorning = cotExitTime.TimeOfDay >= start && cotExitTime.TimeOfDay < end;
                }

                if (enteredDuringNight && exitedDuringNightOrMorning && effectiveMinutes >= minNightStay)
                {
                    isNightStay = true;
                    fee = rate.NightRate;
                }
            }

            // 3. Tarifa Plena Cíclica (si no aplicó pernocta) - 100% Data-Driven
            if (!isNightStay)
            {
                int fullDayThreshold = (rate.FullDayThresholdMinutes.HasValue && rate.FullDayThresholdMinutes.Value > 0)
                    ? rate.FullDayThresholdMinutes.Value
                    : (branch?.FullDayThresholdMinutes ?? 0);

                bool fullDayConfigured = allowDay && rate.FullDayRate > 0 && fullDayThreshold > 0;
                bool fullDayApplies = fullDayConfigured && IsDayApplicable(branch?.FullDayApplicableDays, cotDayOfWeek);

                if (fullDayApplies && fullDayThreshold > 0 && effectiveMinutes >= fullDayThreshold)
                {
                    int fullDaysCount = effectiveMinutes / fullDayThreshold;
                    int remMins = effectiveMinutes % fullDayThreshold;
                    decimal remFee = 0m;

                    if (remMins > 0)
                    {
                        if (allowMinute && allowHour && rate.MinuteRate > 0 && rate.HourRate > 0)
                        {
                            var remH = remMins / 60;
                            var remM = remMins % 60;
                            remFee = (remH * rate.HourRate) + Math.Min(rate.HourRate, remM * rate.MinuteRate);
                        }
                        else if (allowMinute && rate.MinuteRate > 0)
                        {
                            remFee = remMins * rate.MinuteRate;
                        }
                        else if (allowHour && rate.HourRate > 0)
                        {
                            var remH = (int)Math.Max(1, Math.Ceiling(remMins / 60.0));
                            remFee = remH * rate.HourRate;
                        }
                        else
                        {
                            remFee = rate.FullDayRate;
                        }

                        if (remFee > rate.FullDayRate)
                        {
                            remFee = rate.FullDayRate;
                        }
                    }

                    fee = (fullDaysCount * rate.FullDayRate) + remFee;
                }
                else
                {
                    // 4. Cobro regular por minuto / hora
                    if (allowMinute && allowHour && rate.MinuteRate > 0 && rate.HourRate > 0)
                    {
                        var hours = effectiveMinutes / 60;
                        var rem = effectiveMinutes % 60;
                        fee = (hours * rate.HourRate) + Math.Min(rate.HourRate, rem * rate.MinuteRate);
                    }
                    else if (allowMinute && rate.MinuteRate > 0)
                    {
                        fee = effectiveMinutes * rate.MinuteRate;
                    }
                    else if (allowHour && rate.HourRate > 0)
                    {
                        var billableHours = (int)Math.Max(1, Math.Ceiling(effectiveMinutes / 60.0));
                        fee = billableHours * rate.HourRate;
                    }
                    else if (allowDay && rate.FullDayRate > 0)
                    {
                        fee = rate.FullDayRate;
                    }

                    // Tope de tarifa plena del día si aplica
                    if (fullDayApplies && rate.FullDayRate > 0 && fee > rate.FullDayRate)
                    {
                        fee = rate.FullDayRate;
                    }
                }
            }
        }

        // 5. Recargo de Tiquete Extraviado (si aplica y la sede tiene tarifa configurada)
        if (isLostTicket && branch != null && branch.LostTicketFee > 0)
        {
            fee += branch.LostTicketFee;
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

    private static bool IsDayApplicable(string? applicableDays, DayOfWeek day)
    {
        if (string.IsNullOrWhiteSpace(applicableDays)) return true;
        if (applicableDays.Equals("All", StringComparison.OrdinalIgnoreCase)) return true;

        var tokens = applicableDays.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int dayNum = (int)day; // 0=Sunday, 1=Monday... 6=Saturday
        string dayNumStr = dayNum.ToString();
        string dayName = day.ToString(); // e.g. "Monday"

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (trimmed.Equals(dayNumStr, StringComparison.OrdinalIgnoreCase) ||
                trimmed.Equals(dayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
