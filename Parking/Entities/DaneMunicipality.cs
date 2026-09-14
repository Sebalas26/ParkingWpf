namespace Parking.Entities;

public class DaneMunicipality
{
    public string Code { get; set; } = string.Empty; // Código 5 dígitos DANE
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string MunicipalityName { get; set; } = string.Empty;
}
