using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class CommercialAgreement
{
    public Guid AgreementId { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal MinPurchaseAmount { get; set; }
    public decimal? DiscountPercentage { get; set; }
    public decimal? DiscountFixedAmount { get; set; }
    public int? MaxHoursApplicable { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Store Store { get; set; } = null!;
    public ICollection<TicketDiscount> TicketDiscounts { get; set; } = new List<TicketDiscount>();
}
