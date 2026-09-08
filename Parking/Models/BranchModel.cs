using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Parking.Entities;

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

    [JsonPropertyName("paperWidth")]
    public int PaperWidth { get; set; } = 80;

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

    [JsonPropertyName("lostTicketFee")]
    public decimal LostTicketFee { get; set; } = 0m;

    [JsonPropertyName("fullDayThresholdMinutes")]
    public int? FullDayThresholdMinutes { get; set; }

    [JsonPropertyName("fullDayApplicableDays")]
    public string? FullDayApplicableDays { get; set; }

    [JsonPropertyName("fullDayStartTime")]
    public TimeSpan? FullDayStartTime { get; set; }

    [JsonPropertyName("fullDayEndTime")]
    public TimeSpan? FullDayEndTime { get; set; }

    [JsonPropertyName("fullDayRulesJson")]
    public string? FullDayRulesJson { get; set; }

    [JsonPropertyName("nightApplicableDays")]
    public string? NightApplicableDays { get; set; }

    [JsonPropertyName("nightStartTime")]
    public TimeSpan? NightStartTime { get; set; }

    [JsonPropertyName("nightEndTime")]
    public TimeSpan? NightEndTime { get; set; }

    [JsonPropertyName("nightStayMinMinutes")]
    public int? NightStayMinMinutes { get; set; }

    [JsonPropertyName("operatingHours")]
    public List<BranchOperatingHour> OperatingHours { get; set; } = new();

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}
