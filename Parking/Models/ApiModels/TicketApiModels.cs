using System;
using System.Text.Json.Serialization;
using Parking.Core.Enums;

namespace Parking.Models.ApiModels;

public class CheckInApiRequest
{
    [JsonPropertyName("ticketId")]
    public Guid? TicketId { get; set; }

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("branch_id")]
    public int? BranchIdSnake { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("sedeId")]
    public int? SedeId { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("sede_id")]
    public int? SedeIdSnake { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("ticketNumber")]
    public string? TicketNumber { get; set; }

    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("vehicleType")]
    public VehicleType VehicleType { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get => PhoneNumber; set => PhoneNumber = value; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("hourlyRate")]
    public decimal? HourlyRate { get; set; }

    [JsonPropertyName("operatorName")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("entryTimeUtc")]
    public DateTime EntryTimeUtc { get; set; } = DateTime.UtcNow;
}

public class CheckOutApiRequest
{
    [JsonPropertyName("ticketId")]
    public Guid TicketId { get; set; }

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("branch_id")]
    public int? BranchIdSnake { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("sedeId")]
    public int? SedeId { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("sede_id")]
    public int? SedeIdSnake { get => BranchId; set => BranchId ??= value; }

    [JsonPropertyName("paymentMethod")]
    public PaymentMethod PaymentMethod { get; set; }

    [JsonPropertyName("paymentMethodId")]
    public int? PaymentMethodId { get; set; }

    [JsonPropertyName("payment_method_id")]
    public int? PaymentMethodIdSnake { get => PaymentMethodId; set => PaymentMethodId ??= value; }

    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }

    [JsonPropertyName("changeGiven")]
    public decimal ChangeGiven { get; set; }

    [JsonPropertyName("grossAmount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("netAmount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("storeId")]
    public Guid? StoreId { get; set; }

    [JsonPropertyName("agreementId")]
    public Guid? AgreementId { get; set; }

    [JsonPropertyName("invoiceNumber")]
    public string? InvoiceNumber { get; set; }

    [JsonPropertyName("purchaseAmount")]
    public decimal? PurchaseAmount { get; set; }

    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("exitNotes")]
    public string? ExitNotes { get; set; }

    [JsonPropertyName("exitTimeUtc")]
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
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsSuperAdmin { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public List<BranchModel> Branches { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
}

public class ActionRoleDto
{
    [System.Text.Json.Serialization.JsonPropertyName("actionId")]
    public int ActionId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("moduleId")]
    public int ModuleId { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("actionName")]
    public string? ActionName { get; set; }
}
