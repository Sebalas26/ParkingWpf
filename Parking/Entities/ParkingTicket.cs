using System;
using System.Collections.Generic;
using Parking.Core.Enums;

namespace Parking.Entities;

public class ParkingTicket
{
    public Guid TicketId { get; set; } = Guid.NewGuid();
    public int? BranchId { get; set; }
    public int? CompanyId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string? CustomerPhone { get; set; }
    public string? BayNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime EntryTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExitTimeUtc { get; set; }
    public int TotalDurationMinutes { get; set; }
    public decimal HourlyRate { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeGiven { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public int? PaymentMethodId { get; set; }
    public string? ExitNotes { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Active;
    public Guid? OperatorEntryId { get; set; }
    public Guid? OperatorExitId { get; set; }
    public string OperatorName { get; set; } = "Operador General";
    public bool IsSynchronized { get; set; } = true;
    public Guid? ResolutionId { get; set; }
    public string? ResolutionName { get; set; }
    public string? InvoiceNumber { get; set; }
    public bool IsElectronicInvoice { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime EntryTime => EntryTimeUtc.ToLocalTime();
    public DateTime? ExitTime => ExitTimeUtc?.ToLocalTime();
    public decimal TotalAmount => NetAmount;

    public TimeSpan ElapsedDuration => (ExitTimeUtc ?? DateTime.UtcNow) - EntryTimeUtc;

    public string FormattedDuration
    {
        get
        {
            var span = ElapsedDuration;
            if (span.TotalDays >= 1)
            {
                return $"{(int)span.TotalDays}d {span.Hours}h {span.Minutes}min";
            }
            if (span.TotalHours >= 1)
            {
                return $"{(int)span.TotalHours}h {span.Minutes}min {span.Seconds}seg";
            }
            return $"{span.Minutes}min {span.Seconds}seg";
        }
    }

    public decimal CurrentEstimatedAmount
    {
        get
        {
            if (Status == TicketStatus.Completed)
            {
                return NetAmount;
            }
            var totalMinutes = Math.Max(0.01, ElapsedDuration.TotalMinutes);
            var billableHours = (int)Math.Max(1, Math.Ceiling(totalMinutes / 60.0));
            var rate = HourlyRate > 0 ? HourlyRate : 2000m;
            return billableHours * rate;
        }
    }

    public ICollection<TicketDiscount> Discounts { get; set; } = new List<TicketDiscount>();
}
