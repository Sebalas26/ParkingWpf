namespace Parking.Entities;

public class BranchPaymentMethodEntity
{
    public int Id { get; set; }
    public int BranchId { get; set; }
    public int PaymentMethodId { get; set; }
    public bool RequiresCashTender { get; set; }
    public bool IsActive { get; set; } = true;
}
