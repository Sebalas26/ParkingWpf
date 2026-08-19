using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class Store
{
    public Guid StoreId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CommercialAgreement> Agreements { get; set; } = new List<CommercialAgreement>();
    public ICollection<TicketDiscount> TicketDiscounts { get; set; } = new List<TicketDiscount>();
}
