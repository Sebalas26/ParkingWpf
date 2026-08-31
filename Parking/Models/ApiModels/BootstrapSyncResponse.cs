using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Parking.Core.Enums;

namespace Parking.Models.ApiModels;

public class ApiBranchSyncDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("totalCapacity")]
    public int TotalCapacity { get; set; } = 100;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("logoBase64")]
    public string? LogoBase64 { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ApiUserSyncDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("userRoleId")]
    public int UserRoleId { get; set; }

    [JsonPropertyName("identificationNumber")]
    public string? IdentificationNumber { get; set; }

    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("firstSurname")]
    public string? FirstSurname { get; set; }

    [JsonPropertyName("fullName")]
    public string? FullName { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class ApiPaymentMethodSyncDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("state")]
    public bool? State { get; set; }

    [JsonPropertyName("requiresCashTender")]
    public bool? RequiresCashTender { get; set; }

    public bool GetEffectiveActive() => State ?? IsActive;
}

public class ApiVehicleRateSyncDto
{
    [JsonPropertyName("rateId")]
    public Guid RateId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("vehicleType")]
    public object? VehicleType { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("minuteRate")]
    public decimal MinuteRate { get; set; }

    [JsonPropertyName("hourRate")]
    public decimal HourRate { get; set; }

    [JsonPropertyName("fullDayRate")]
    public decimal FullDayRate { get; set; }

    [JsonPropertyName("gracePeriodMinutes")]
    public int GracePeriodMinutes { get; set; } = 15;

    [JsonPropertyName("iconKey")]
    public string? IconKey { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime? UpdatedAtUtc { get; set; }

    public VehicleType GetVehicleType()
    {
        if (VehicleType is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(VehicleType), elem.GetInt32()))
                return (VehicleType)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String && Enum.TryParse<VehicleType>(elem.GetString(), true, out var vt))
                return vt;
        }
        else if (VehicleType is string s && Enum.TryParse<VehicleType>(s, true, out var vt))
        {
            return vt;
        }
        else if (VehicleType is int i && Enum.IsDefined(typeof(VehicleType), i))
        {
            return (VehicleType)i;
        }
        return Parking.Core.Enums.VehicleType.Car;
    }
}

public class ApiStoreSyncDto
{
    [JsonPropertyName("storeId")]
    public Guid StoreId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("taxId")]
    public string? TaxId { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class ApiCommercialAgreementSyncDto
{
    [JsonPropertyName("agreementId")]
    public Guid AgreementId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("storeId")]
    public Guid StoreId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("minPurchaseAmount")]
    public decimal MinPurchaseAmount { get; set; }

    [JsonPropertyName("discountPercentage")]
    public decimal? DiscountPercentage { get; set; }

    [JsonPropertyName("discountFixedAmount")]
    public decimal? DiscountFixedAmount { get; set; }

    [JsonPropertyName("maxHoursApplicable")]
    public int? MaxHoursApplicable { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }
}

public class ApiBillingResolutionSyncDto
{
    [JsonPropertyName("resolutionId")]
    public Guid ResolutionId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("companyId")]
    public int? CompanyId { get; set; }

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = string.Empty;

    [JsonPropertyName("resolutionNumber")]
    public string ResolutionNumber { get; set; } = string.Empty;

    [JsonPropertyName("fromNumber")]
    public long FromNumber { get; set; }

    [JsonPropertyName("toNumber")]
    public long ToNumber { get; set; }

    [JsonPropertyName("currentNumber")]
    public long CurrentNumber { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime ValidFrom { get; set; }

    [JsonPropertyName("validTo")]
    public DateTime ValidTo { get; set; }

    [JsonPropertyName("technicalKey")]
    public string? TechnicalKey { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class ApiWorkShiftSyncDto
{
    [JsonPropertyName("shiftId")]
    public Guid ShiftId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("operatorName")]
    public string OperatorName { get; set; } = string.Empty;

    [JsonPropertyName("startTimeUtc")]
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endTimeUtc")]
    public DateTime? EndTimeUtc { get; set; }

    [JsonPropertyName("baseAmount")]
    public decimal BaseAmount { get; set; }

    [JsonPropertyName("totalCashCollected")]
    public decimal TotalCashCollected { get; set; }

    [JsonPropertyName("totalCardCollected")]
    public decimal TotalCardCollected { get; set; }

    [JsonPropertyName("totalTransferCollected")]
    public decimal TotalTransferCollected { get; set; }

    [JsonPropertyName("totalDiscounts")]
    public decimal TotalDiscounts { get; set; }

    [JsonPropertyName("totalCashWithdrawals")]
    public decimal TotalCashWithdrawals { get; set; }

    [JsonPropertyName("expectedCash")]
    public decimal ExpectedCash { get; set; }

    [JsonPropertyName("actualCashCounted")]
    public decimal ActualCashCounted { get; set; }

    [JsonPropertyName("cashDifference")]
    public decimal CashDifference { get; set; }

    [JsonPropertyName("totalTicketsProcessed")]
    public int TotalTicketsProcessed { get; set; }

    [JsonPropertyName("totalVehiclesEntered")]
    public int TotalVehiclesEntered { get; set; }

    [JsonPropertyName("status")]
    public object? Status { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("handoverToUserId")]
    public Guid? HandoverToUserId { get; set; }

    [JsonPropertyName("handoverToUserName")]
    public string? HandoverToUserName { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("closedAtUtc")]
    public DateTime? ClosedAtUtc { get; set; }

    public int GetNormalizedStatus()
    {
        if (Status is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number) return elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String)
            {
                var s = elem.GetString();
                return string.Equals(s, "Closed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            }
        }
        else if (Status is int i)
        {
            return i;
        }
        else if (Status is string str)
        {
            return string.Equals(str, "Closed", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        }
        return 0;
    }
}

public class ApiMonthlySubscriptionSyncDto
{
    [JsonPropertyName("subscriptionId")]
    public Guid SubscriptionId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("customerName")]
    public string CustomerName { get; set; } = string.Empty;

    [JsonPropertyName("customerDocument")]
    public string CustomerDocument { get; set; } = string.Empty;

    [JsonPropertyName("customerPhone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [JsonPropertyName("customerEmail")]
    public string? CustomerEmail { get; set; }

    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("vehicleType")]
    public object? VehicleType { get; set; }

    [JsonPropertyName("startDateUtc")]
    public DateTime StartDateUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("endDateUtc")]
    public DateTime EndDateUtc { get; set; } = DateTime.UtcNow.AddMonths(1);

    [JsonPropertyName("monthlyFee")]
    public decimal MonthlyFee { get; set; }

    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }

    [JsonPropertyName("paymentMethod")]
    public object? PaymentMethod { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public VehicleType GetVehicleType()
    {
        if (VehicleType is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(VehicleType), elem.GetInt32()))
                return (VehicleType)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String && Enum.TryParse<VehicleType>(elem.GetString(), true, out var vt))
                return vt;
        }
        else if (VehicleType is string s && Enum.TryParse<VehicleType>(s, true, out var vt))
        {
            return vt;
        }
        else if (VehicleType is int i && Enum.IsDefined(typeof(VehicleType), i))
        {
            return (VehicleType)i;
        }
        return Parking.Core.Enums.VehicleType.Car;
    }

    public PaymentMethod GetPaymentMethod()
    {
        if (PaymentMethod is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(PaymentMethod), elem.GetInt32()))
                return (PaymentMethod)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String)
            {
                var str = elem.GetString();
                if (string.Equals(str, "Transfer", StringComparison.OrdinalIgnoreCase)) return Parking.Core.Enums.PaymentMethod.DigitalTransfer;
                if (Enum.TryParse<PaymentMethod>(str, true, out var pm)) return pm;
            }
        }
        else if (PaymentMethod is string s)
        {
            if (string.Equals(s, "Transfer", StringComparison.OrdinalIgnoreCase)) return Parking.Core.Enums.PaymentMethod.DigitalTransfer;
            if (Enum.TryParse<PaymentMethod>(s, true, out var pm)) return pm;
        }
        else if (PaymentMethod is int i && Enum.IsDefined(typeof(PaymentMethod), i))
        {
            return (PaymentMethod)i;
        }
        return Parking.Core.Enums.PaymentMethod.Cash;
    }
}

public class ApiParkingTicketSyncDto
{
    [JsonPropertyName("ticketId")]
    public Guid TicketId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("ticketNumber")]
    public string TicketNumber { get; set; } = string.Empty;

    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("vehicleType")]
    public object? VehicleType { get; set; }

    [JsonPropertyName("customerPhone")]
    public string? CustomerPhone { get; set; }

    [JsonPropertyName("bayNumber")]
    public string? BayNumber { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }

    [JsonPropertyName("entryTimeUtc")]
    public DateTime EntryTimeUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("exitTimeUtc")]
    public DateTime? ExitTimeUtc { get; set; }

    [JsonPropertyName("totalDurationMinutes")]
    public int TotalDurationMinutes { get; set; }

    [JsonPropertyName("hourlyRate")]
    public decimal HourlyRate { get; set; }

    [JsonPropertyName("grossAmount")]
    public decimal GrossAmount { get; set; }

    [JsonPropertyName("discountAmount")]
    public decimal DiscountAmount { get; set; }

    [JsonPropertyName("netAmount")]
    public decimal NetAmount { get; set; }

    [JsonPropertyName("amountPaid")]
    public decimal AmountPaid { get; set; }

    [JsonPropertyName("changeGiven")]
    public decimal ChangeGiven { get; set; }

    [JsonPropertyName("paymentMethod")]
    public object? PaymentMethod { get; set; }

    [JsonPropertyName("paymentMethodId")]
    public int? PaymentMethodId { get; set; }

    [JsonPropertyName("exitNotes")]
    public string? ExitNotes { get; set; }

    [JsonPropertyName("status")]
    public object? Status { get; set; }

    [JsonPropertyName("operatorName")]
    public string OperatorName { get; set; } = "Operador General";

    [JsonPropertyName("isSynchronized")]
    public bool IsSynchronized { get; set; } = true;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public VehicleType GetVehicleType()
    {
        if (VehicleType is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(VehicleType), elem.GetInt32()))
                return (VehicleType)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String && Enum.TryParse<VehicleType>(elem.GetString(), true, out var vt))
                return vt;
        }
        else if (VehicleType is string s && Enum.TryParse<VehicleType>(s, true, out var vt))
        {
            return vt;
        }
        else if (VehicleType is int i && Enum.IsDefined(typeof(VehicleType), i))
        {
            return (VehicleType)i;
        }
        return Parking.Core.Enums.VehicleType.Car;
    }

    public TicketStatus GetTicketStatus()
    {
        if (Status is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(TicketStatus), elem.GetInt32()))
                return (TicketStatus)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String && Enum.TryParse<TicketStatus>(elem.GetString(), true, out var st))
                return st;
        }
        else if (Status is string s && Enum.TryParse<TicketStatus>(s, true, out var st))
        {
            return st;
        }
        else if (Status is int i && Enum.IsDefined(typeof(TicketStatus), i))
        {
            return (TicketStatus)i;
        }
        return TicketStatus.Active;
    }

    public PaymentMethod? GetPaymentMethod()
    {
        if (PaymentMethod == null) return null;
        if (PaymentMethod is JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Number && Enum.IsDefined(typeof(PaymentMethod), elem.GetInt32()))
                return (PaymentMethod)elem.GetInt32();
            if (elem.ValueKind == JsonValueKind.String)
            {
                var str = elem.GetString();
                if (string.Equals(str, "Transfer", StringComparison.OrdinalIgnoreCase)) return Parking.Core.Enums.PaymentMethod.DigitalTransfer;
                if (Enum.TryParse<PaymentMethod>(str, true, out var pm)) return pm;
            }
        }
        else if (PaymentMethod is string s)
        {
            if (string.Equals(s, "Transfer", StringComparison.OrdinalIgnoreCase)) return Parking.Core.Enums.PaymentMethod.DigitalTransfer;
            if (Enum.TryParse<PaymentMethod>(s, true, out var pm)) return pm;
        }
        else if (PaymentMethod is int i && Enum.IsDefined(typeof(PaymentMethod), i))
        {
            return (PaymentMethod)i;
        }
        return null;
    }
}

public class ApiVehicleIncidentSyncDto
{
    [JsonPropertyName("incidentId")]
    public Guid IncidentId { get; set; } = Guid.NewGuid();

    [JsonPropertyName("branchId")]
    public int? BranchId { get; set; }

    [JsonPropertyName("isGlobal")]
    public bool IsGlobal { get; set; }

    [JsonPropertyName("branchIds")]
    public List<int> BranchIds { get; set; } = new();

    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("incidentType")]
    public string IncidentType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "Activa";

    [JsonPropertyName("reportedBy")]
    public string ReportedBy { get; set; } = string.Empty;

    [JsonPropertyName("resolvedBy")]
    public string? ResolvedBy { get; set; }

    [JsonPropertyName("resolvedNotes")]
    public string? ResolvedNotes { get; set; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("resolvedAtUtc")]
    public DateTime? ResolvedAtUtc { get; set; }
}

public class ApiUserRoleSyncDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class ApiRoleActionSyncDto
{
    [JsonPropertyName("roleId")]
    public int RoleId { get; set; }

    [JsonPropertyName("actionSlug")]
    public string ActionSlug { get; set; } = string.Empty;

    [JsonPropertyName("actionName")]
    public string? ActionName { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class BootstrapSyncResponse
{
    [JsonPropertyName("serverTimeUtc")]
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("totalCapacity")]
    public int TotalCapacity { get; set; } = 120;

    [JsonPropertyName("branches")]
    public List<ApiBranchSyncDto> Branches { get; set; } = new();

    [JsonPropertyName("users")]
    public List<ApiUserSyncDto> Users { get; set; } = new();

    [JsonPropertyName("userRoles")]
    public List<ApiUserRoleSyncDto> UserRoles { get; set; } = new();

    [JsonPropertyName("roleActions")]
    public List<ApiRoleActionSyncDto> RoleActions { get; set; } = new();

    [JsonPropertyName("paymentMethods")]
    public List<ApiPaymentMethodSyncDto> PaymentMethods { get; set; } = new();

    [JsonPropertyName("rates")]
    public List<ApiVehicleRateSyncDto> Rates { get; set; } = new();

    [JsonPropertyName("stores")]
    public List<ApiStoreSyncDto> Stores { get; set; } = new();

    [JsonPropertyName("agreements")]
    public List<ApiCommercialAgreementSyncDto> Agreements { get; set; } = new();

    [JsonPropertyName("workShifts")]
    public List<ApiWorkShiftSyncDto> WorkShifts { get; set; } = new();

    [JsonPropertyName("monthlySubscriptions")]
    public List<ApiMonthlySubscriptionSyncDto> MonthlySubscriptions { get; set; } = new();

    [JsonPropertyName("activeTickets")]
    public List<ApiParkingTicketSyncDto> ActiveTickets { get; set; } = new();

    [JsonPropertyName("recentTickets")]
    public List<ApiParkingTicketSyncDto> RecentTickets { get; set; } = new();

    [JsonPropertyName("incidents")]
    public List<ApiVehicleIncidentSyncDto> Incidents { get; set; } = new();

    [JsonPropertyName("resolutions")]
    public List<ApiBillingResolutionSyncDto> Resolutions { get; set; } = new();
}

public class PlateCheckResultDto
{
    [JsonPropertyName("plateNumber")]
    public string PlateNumber { get; set; } = string.Empty;

    [JsonPropertyName("hasIncidents")]
    public bool HasIncidents { get; set; }

    [JsonPropertyName("isBlocked")]
    public bool IsBlocked { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("incidentType")]
    public string IncidentType { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("reportedBy")]
    public string ReportedBy { get; set; } = string.Empty;

    [JsonPropertyName("reportedAtUtc")]
    public DateTime? ReportedAtUtc { get; set; }

    [JsonPropertyName("incidentId")]
    public Guid? IncidentId { get; set; }
}
