using System;
using System.ComponentModel.DataAnnotations;
using Parking.Core.Enums;

namespace Parking.Entities;

public class MonthlySubscription
{
    [Key]
    public Guid SubscriptionId { get; set; } = Guid.NewGuid();
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerDocument { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; } = VehicleType.Car;
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;
    public DateTime EndDateUtc { get; set; } = DateTime.UtcNow.AddMonths(1);
    public decimal MonthlyFee { get; set; }
    public decimal AmountPaid { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime StartDate => StartDateUtc.ToLocalTime();
    public DateTime EndDate => EndDateUtc.ToLocalTime();
    public bool IsCurrentlyValid => IsActive && EndDateUtc >= DateTime.UtcNow;
    public int DaysRemaining => (int)Math.Max(0, Math.Ceiling((EndDateUtc - DateTime.UtcNow).TotalDays));
    public string StatusLabel => !IsActive ? "Cancelada" : (EndDateUtc < DateTime.UtcNow ? "Vencida" : (DaysRemaining <= 5 ? $"Por vencer ({DaysRemaining}d)" : "Activa"));
}
