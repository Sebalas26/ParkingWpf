using System;
using System.Text.Json.Serialization;

namespace Parking.Models.ApiModels;

public class CreateCustomerApiRequest
{
    [JsonPropertyName("customerId")]
    public Guid? CustomerId { get; set; }

    [JsonPropertyName("companyId")]
    public int? CompanyId { get; set; }

    [JsonPropertyName("identificationTypeId")]
    public int IdentificationTypeId { get; set; } = 1;

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; } = string.Empty;

    [JsonPropertyName("checkDigit")]
    public string? CheckDigit { get; set; }

    [JsonPropertyName("personType")]
    public string PersonType { get; set; } = "Person";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("tradeName")]
    public string? TradeName { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("cityCode")]
    public string? CityCode { get; set; }

    [JsonPropertyName("stateCode")]
    public string? StateCode { get; set; }

    [JsonPropertyName("fiscalResponsibilities")]
    public string FiscalResponsibilities { get; set; } = "R-99-PN";

    [JsonPropertyName("initialPlateNumber")]
    public string? InitialPlateNumber { get; set; }
}

public class CustomerApiResponse
{
    [JsonPropertyName("customerId")]
    public Guid CustomerId { get; set; }

    [JsonPropertyName("companyId")]
    public int? CompanyId { get; set; }

    [JsonPropertyName("identificationTypeId")]
    public int IdentificationTypeId { get; set; }

    [JsonPropertyName("documentNumber")]
    public string DocumentNumber { get; set; } = string.Empty;

    [JsonPropertyName("checkDigit")]
    public string? CheckDigit { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("personType")]
    public string PersonType { get; set; } = "Person";

    [JsonPropertyName("tradeName")]
    public string? TradeName { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("cityCode")]
    public string? CityCode { get; set; }

    [JsonPropertyName("stateCode")]
    public string? StateCode { get; set; }

    [JsonPropertyName("cityName")]
    public string? CityName { get; set; }

    [JsonPropertyName("departmentName")]
    public string? DepartmentName { get; set; }

    [JsonPropertyName("fiscalResponsibilities")]
    public string FiscalResponsibilities { get; set; } = "R-99-PN";

    [JsonPropertyName("siigoCustomerId")]
    public Guid? SiigoCustomerId { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }

    [JsonPropertyName("plateNumbers")]
    public List<string> PlateNumbers { get; set; } = new();
}

public class CustomerApiUpdateResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public CustomerApiResponse? Customer { get; set; }

    public static implicit operator bool(CustomerApiUpdateResult? result) => result?.Success ?? false;
}
