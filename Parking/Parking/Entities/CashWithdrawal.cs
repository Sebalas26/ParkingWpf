using System;

namespace Parking.Entities;

public class CashWithdrawal
{
    public Guid WithdrawalId { get; set; } = Guid.NewGuid();
    public Guid ShiftId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string AuthorizedByAdminName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
