using System;
using System.Text.Json.Serialization;

namespace Parking.Models;

public class BranchModel
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("companyId")]
    public int? CompanyId { get; set; }

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

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("defaultInitialCash")]
    public decimal? DefaultInitialCash { get; set; }

    [JsonPropertyName("allowChargeByMinute")]
    public bool AllowChargeByMinute { get; set; } = true;

    [JsonPropertyName("allowChargeByHour")]
    public bool AllowChargeByHour { get; set; } = true;

    [JsonPropertyName("allowChargeByDay")]
    public bool AllowChargeByDay { get; set; } = true;

    [JsonPropertyName("allowChargeByNight")]
    public bool AllowChargeByNight { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}
