using System;
using Parking.Core.Enums;

namespace Parking.Models.ApiModels;

public class CheckInApiRequest
{
    public string PlateNumber { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Notes { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public DateTime EntryTimeUtc { get; set; } = DateTime.UtcNow;
}

public class CheckOutApiRequest
{
    public Guid TicketId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal AmountPaid { get; set; }
    public Guid? StoreId { get; set; }
    public Guid? AgreementId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal? PurchaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public DateTime ExitTimeUtc { get; set; } = DateTime.UtcNow;
}

public class LoginApiRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginApiResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
}
