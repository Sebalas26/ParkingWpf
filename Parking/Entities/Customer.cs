using System;
using System.Collections.Generic;

namespace Parking.Entities;

public class Customer
{
    public Guid CustomerId { get; set; } = Guid.NewGuid();
    public int? CompanyId { get; set; }
    public int IdentificationTypeId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string? CheckDigit { get; set; }
    public string PersonType { get; set; } = "Person"; // Person, Company
    public string FullName { get; set; } = string.Empty;
    public string? TradeName { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? CityCode { get; set; } // Código municipio DANE
    public string? StateCode { get; set; } // Código departamento DANE
    public string FiscalResponsibilities { get; set; } = "R-99-PN";
    public Guid? SiigoCustomerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<CustomerVehicle> Vehicles { get; set; } = new List<CustomerVehicle>();
    public virtual ICollection<ParkingTicket> ParkingTickets { get; set; } = new List<ParkingTicket>();
}
