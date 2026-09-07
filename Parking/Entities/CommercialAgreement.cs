using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class CommercialAgreement
{
    public Guid AgreementId { get; set; } = Guid.NewGuid();
    public int? CompanyId { get; set; }
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinPurchaseAmount { get; set; }
    public int DiscountType { get; set; } = 0; // 0=Percentage, 1=FixedAmount, 2=FreeTime
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountFixedAmount { get; set; }
    public int? FreeMinutes { get; set; }
    public int? FreeHours { get; set; }
    public int? MaxHoursApplicable { get; set; }
    public int? MaxMinutesApplicable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? ImageUrl { get; set; }

    public Store Store { get; set; } = null!;
    public ICollection<TicketDiscount> TicketDiscounts { get; set; } = new List<TicketDiscount>();
}
