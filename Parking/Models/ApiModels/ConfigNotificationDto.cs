using System;

namespace Parking.Models.ApiModels;

public class ConfigNotificationDto
{
    public string EventType { get; set; } = "ConfigUpdated";
    public int? BranchId { get; set; }
    public int? UserId { get; set; }
    public string? SessionToken { get; set; }
    public string Title { get; set; } = "Actualización de Configuración";
    public string Message { get; set; } = "Se han modificado parámetros en el servidor central.";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
