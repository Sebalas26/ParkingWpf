using System;
using System.Collections.Generic;
using Parking.Entities;

namespace Parking.Models.ApiModels;

public class BootstrapSyncResponse
{
    public DateTime ServerTimeUtc { get; set; } = DateTime.UtcNow;
    public int TotalCapacity { get; set; } = 120;
    public List<User> Users { get; set; } = new();
    public List<VehicleRate> Rates { get; set; } = new();
    public List<Store> Stores { get; set; } = new();
    public List<CommercialAgreement> Agreements { get; set; } = new();
    public List<ParkingTicket> ActiveTickets { get; set; } = new();
}
