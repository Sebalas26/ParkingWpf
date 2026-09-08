using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parking.Entities;

public class WorkShift
{
    public Guid ShiftId { get; set; } = Guid.NewGuid();
    public int? BranchId { get; set; }
    public int? CompanyId { get; set; }
    public int UserId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string CashRegisterName { get; set; } = "Caja Principal";
    public DateTime StartTimeUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndTimeUtc { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal TotalCashCollected { get; set; }
    public decimal TotalCardCollected { get; set; }
    public decimal TotalTransferCollected { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalCashWithdrawals { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal ActualCashCounted { get; set; }
    public decimal CashDifference { get; set; }
    public int TotalTicketsProcessed { get; set; }
    public int TotalVehiclesEntered { get; set; }

    [JsonConverter(typeof(ShiftStatusJsonConverter))]
    public int Status { get; set; } // 0 = Open, 1 = Closed

    public string? Notes { get; set; }
    public Guid? HandoverToUserId { get; set; }
    public string? HandoverToUserName { get; set; }
    public bool IsSynchronized { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAtUtc { get; set; }

    public DateTime StartTime => StartTimeUtc.ToLocalTime();
    public DateTime? EndTime => EndTimeUtc?.ToLocalTime();
    public TimeSpan Duration => (EndTimeUtc ?? DateTime.UtcNow) - StartTimeUtc;
    public string FormattedDuration => $"{(int)Duration.TotalHours}h {Duration.Minutes}m";
    public decimal TotalRevenue => TotalCashCollected + TotalCardCollected + TotalTransferCollected;
}

public class ShiftStatusJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            if (string.Equals(str, "Closed", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(str, "Open", StringComparison.OrdinalIgnoreCase)) return 0;
            if (int.TryParse(str, out var val)) return val;
        }

        if (reader.TokenType == JsonTokenType.True) return 1;
        if (reader.TokenType == JsonTokenType.False) return 0;

        return 0;
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
