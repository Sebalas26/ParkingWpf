using System;
using Parking.Core.Enums;

namespace Parking.Entities;

public class PaymentMethodEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "IconCash";
    public bool State { get; set; } = true;
    public bool RequiresCashTender { get; set; } = true;

    public PaymentMethod ToEnum()
    {
        var normalized = (Name ?? string.Empty).ToLowerInvariant();
        if (normalized.Contains("debito") || normalized.Contains("débito"))
        {
            return PaymentMethod.DebitCard;
        }
        if (normalized.Contains("credito") || normalized.Contains("crédito") || normalized.Contains("tarjeta"))
        {
            return PaymentMethod.CreditCard;
        }
        if (normalized.Contains("transfer") || normalized.Contains("qr") || normalized.Contains("nequi") || normalized.Contains("davi"))
        {
            return PaymentMethod.DigitalTransfer;
        }
        return PaymentMethod.Cash;
    }
}
