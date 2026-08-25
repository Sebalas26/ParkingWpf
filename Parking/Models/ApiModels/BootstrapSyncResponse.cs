using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Parking.Entities;

namespace Parking.Models.ApiModels;

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

public class BootstrapSyncResponse
{
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public int TotalCapacity { get; set; } = 120;
    public List<ApiUserSyncDto> Users { get; set; } = new();
    public List<PaymentMethodEntity> PaymentMethods { get; set; } = new();
    public List<VehicleRate> Rates { get; set; } = new();
    public List<Store> Stores { get; set; } = new();
    public List<CommercialAgreement> Agreements { get; set; } = new();
    public List<WorkShift> WorkShifts { get; set; } = new();
    public List<MonthlySubscription> MonthlySubscriptions { get; set; } = new();
    public List<ParkingTicket> ActiveTickets { get; set; } = new();
    public List<ParkingTicket> RecentTickets { get; set; } = new();
}
