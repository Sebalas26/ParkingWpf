using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Core.Enums;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IPricingCalculatorService
{
    decimal CalculateFee(VehicleType vehicleType, DateTime entryTime, DateTime exitTime, int discountFreeMinutes = 0, bool isLostTicket = false);
    Task<IReadOnlyList<VehicleRate>> GetAllRatesAsync();
    VehicleRate? GetRate(VehicleType vehicleType, DayOfWeek? dayOfWeek = null);
    Task UpdateRateAsync(VehicleType vehicleType, decimal hourRate, decimal minuteRate, decimal fullDayRate, int gracePeriodMinutes);
    Task ReloadRatesAsync();
}
