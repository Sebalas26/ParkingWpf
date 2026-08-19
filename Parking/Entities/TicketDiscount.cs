using System;

namespace Parking.Entities;

public class TicketDiscount
{
    public Guid TicketDiscountId { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public Guid StoreId { get; set; }
    public Guid AgreementId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal PurchaseAmount { get; set; }
    public decimal AppliedDiscountAmount { get; set; }
    public DateTime ValidatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsSynchronized { get; set; } = true;

    public ParkingTicket Ticket { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public CommercialAgreement Agreement { get; set; } = null!;
}
