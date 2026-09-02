using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class ReceiptPreviewViewModel : ViewModelBase
{
    private readonly IReceiptPrinterService _printerService;
    private readonly ISessionService _sessionService;
    private readonly IDbConnectionManager _connectionManager;

    [ObservableProperty]
    private ParkingTicket _ticket = new();

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _printSuccess;

    [ObservableProperty]
    private bool _isExitReceipt;

    [ObservableProperty]
    private bool _isFvmInvoice;

    [ObservableProperty]
    private bool _isStandardExitReceipt;

    [ObservableProperty]
    private bool _isEntryTicket = true;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _barcodeImage;

    [ObservableProperty]
    private string _branchName = "PARKING FLOW";

    [ObservableProperty]
    private string _branchNit = "NIT: 900.914.246-2";

    [ObservableProperty]
    private string _branchAddress = "CALLE 26 #57-83";

    [ObservableProperty]
    private string _branchPhone = "Tel. 318 181818 - 301 301301301";

    [ObservableProperty]
    private string _formattedRateText = string.Empty;

    [ObservableProperty]
    private string _paymentMethodDisplayName = "Efectivo";

    [ObservableProperty]
    private bool _hasAgreement;

    [ObservableProperty]
    private string _agreementDisplayName = string.Empty;

    [ObservableProperty]
    private string _formattedTotalPaid = "$ 0";

    [ObservableProperty]
    private string _ivaPercentageText = "19%";

    [ObservableProperty]
    private string _customerName = "CONSUMIDOR FINAL";

    [ObservableProperty]
    private string _customerDocument = "CC 222222222";

    [ObservableProperty]
    private string _customerNit = "22222222";

    [ObservableProperty]
    private string _customerAddress = "CR 38 19 55 BRR CAMOA";

    [ObservableProperty]
    private string _invoiceNumberText = string.Empty;

    [ObservableProperty]
    private string _invoicePrefix = "FVM";

    [ObservableProperty]
    private string _invoiceNumberStr = "991000031";

    [ObservableProperty]
    private string _invoiceDateStr = string.Empty;

    [ObservableProperty]
    private string _invoiceTimeStr = string.Empty;

    [ObservableProperty]
    private string _entryTimeStr = string.Empty;

    [ObservableProperty]
    private string _entryDateStr = string.Empty;

    [ObservableProperty]
    private string _exitTimeStr = string.Empty;

    [ObservableProperty]
    private string _exitDateStr = string.Empty;

    [ObservableProperty]
    private string _durationMinutesStr = "0";

    [ObservableProperty]
    private string _baseGravableStr = "0";

    [ObservableProperty]
    private string _iva19Str = "0";

    [ObservableProperty]
    private string _totalStr = "0";

    [ObservableProperty]
    private string _attendedBy = "MERLIN";

    [ObservableProperty]
    private string _paymentMethodName = "CONTADO";

    [ObservableProperty]
    private string _cufe = string.Empty;

    [ObservableProperty]
    private string _dianResolutionText = string.Empty;

    [ObservableProperty]
    private string _dianRangeText = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _electronicInvoiceQrImage;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _consultationQrCodeImage;

    [ObservableProperty]
    private BillingResolution? _resolution;

    public string PublicConsultationUrl => "https://www.parking-flow.com/mockup-consulta";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(
        IReceiptPrinterService printerService,
        ISessionService sessionService,
        IDbConnectionManager connectionManager)
    {
        _printerService = printerService;
        _sessionService = sessionService;
        _connectionManager = connectionManager;
    }

    public void LoadTicket(ParkingTicket ticket, BillingResolution? resolution = null)
    {
        Ticket = ticket;
        Resolution = resolution;
        PrintSuccess = false;

        var currentBranch = _sessionService.CurrentBranch;
        BranchName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name.ToUpperInvariant() : "PARQUEADERO MERLIN";
        BranchAddress = !string.IsNullOrWhiteSpace(currentBranch?.Address) ? currentBranch.Address.ToUpperInvariant() : "CALLE 18 18-18";
        BranchNit = !string.IsNullOrWhiteSpace(currentBranch?.Notes) && currentBranch.Notes.StartsWith("NIT", StringComparison.OrdinalIgnoreCase)
            ? currentBranch.Notes
            : "NIT. 900900900-9";
        BranchPhone = "Tel. 318 181818 - 301 301301301";

        FormattedRateText = $"TARIFA: {ticket.HourlyRate:C0} / HORA";

        IsExitReceipt = ticket.Status == TicketStatus.Completed || ticket.ExitTimeUtc.HasValue || ticket.ExitTime.HasValue;
        IsEntryTicket = !IsExitReceipt;

        if (IsExitReceipt)
        {
            // 1. Detectar si la resolución es FVM (Factura Electrónica)
            bool isFvm = false;

            if (resolution != null && (
                (resolution.Prefix?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (resolution.DocumentType?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (resolution.DocumentType?.Contains("Factura Electr", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (resolution.Name?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                string.Equals(resolution.Prefix, "A1PQ", StringComparison.OrdinalIgnoreCase)))
            {
                isFvm = true;
            }
            else if (!string.IsNullOrWhiteSpace(ticket.ResolutionName) &&
                (ticket.ResolutionName.Contains("FVM", StringComparison.OrdinalIgnoreCase) ||
                 ticket.ResolutionName.Contains("Factura Electr", StringComparison.OrdinalIgnoreCase)))
            {
                isFvm = true;
            }
            else if (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber) &&
                     ticket.InvoiceNumber.StartsWith("FVM", StringComparison.OrdinalIgnoreCase))
            {
                isFvm = true;
            }
            else if (ticket.ResolutionId.HasValue)
            {
                try
                {
                    using var db = _connectionManager.CreateDbContext();
                    var dbRes = db.BillingResolutions.FirstOrDefault(r => r.ResolutionId == ticket.ResolutionId.Value);
                    if (dbRes != null)
                    {
                        isFvm = (dbRes.Prefix?.Equals("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (dbRes.DocumentType?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (dbRes.DocumentType?.Contains("Factura Electr", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (dbRes.Name?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                string.Equals(dbRes.Prefix, "A1PQ", StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { }
            }

            IsFvmInvoice = isFvm;
            IsStandardExitReceipt = !isFvm;

            // 2. Resolver Nombre del Medio de Pago
            string paymentName = string.Empty;
            try
            {
                using var db = _connectionManager.CreateDbContext();
                if (ticket.PaymentMethodId.HasValue)
                {
                    var pmEntity = db.PaymentMethods.FirstOrDefault(p => p.Id == ticket.PaymentMethodId.Value);
                    if (pmEntity != null && !string.IsNullOrWhiteSpace(pmEntity.Name))
                    {
                        paymentName = pmEntity.Name;
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(paymentName))
            {
                paymentName = ticket.PaymentMethod switch
                {
                    PaymentMethod.DebitCard => "Tarjeta Débito",
                    PaymentMethod.CreditCard => "Tarjeta Crédito",
                    PaymentMethod.DigitalTransfer => "Transferencia Digital",
                    _ => "Efectivo"
                };
            }
            PaymentMethodDisplayName = paymentName;
            PaymentMethodName = paymentName.ToUpperInvariant();

            // 3. Resolver Convenio si aplica
            HasAgreement = false;
            AgreementDisplayName = string.Empty;
            if (ticket.DiscountAmount > 0)
            {
                string? agreementName = null;
                try
                {
                    using var db = _connectionManager.CreateDbContext();
                    var discountEntity = db.TicketDiscounts
                        .Include(d => d.Agreement)
                        .FirstOrDefault(d => d.TicketId == ticket.TicketId);
                    if (discountEntity?.Agreement != null && !string.IsNullOrWhiteSpace(discountEntity.Agreement.Name))
                    {
                        agreementName = discountEntity.Agreement.Name;
                    }
                }
                catch { }

                HasAgreement = true;
                if (!string.IsNullOrWhiteSpace(agreementName))
                {
                    AgreementDisplayName = $"{agreementName} (-{ticket.DiscountAmount:C0})";
                }
                else
                {
                    AgreementDisplayName = $"Descuento (-{ticket.DiscountAmount:C0})";
                }
            }

            // 4. Valor que pagó y % IVA
            var totalPaid = ticket.NetAmount > 0 ? ticket.NetAmount : (ticket.AmountPaid > 0 ? ticket.AmountPaid : (ticket.TotalAmount > 0 ? ticket.TotalAmount : ticket.GrossAmount));
            FormattedTotalPaid = $"{totalPaid:C0}";
            IvaPercentageText = "19%";

            var exitTime = ticket.ExitTime ?? (ticket.ExitTimeUtc.HasValue ? ticket.ExitTimeUtc.Value.ToLocalTime() : DateTime.Now);
            var entryTime = ticket.EntryTime != default ? ticket.EntryTime : (ticket.CreatedAtUtc != default ? ticket.CreatedAtUtc.ToLocalTime() : DateTime.Now);

            ExitTimeStr = exitTime.ToString("HH:mm:ss");
            ExitDateStr = exitTime.ToString("dd/MM/yy");
            EntryTimeStr = entryTime.ToString("HH:mm:ss");
            EntryDateStr = entryTime.ToString("dd/MM/yy");

            var duration = exitTime - entryTime;
            var totalMins = Math.Max(1, (long)Math.Round(duration.TotalMinutes));
            DurationMinutesStr = totalMins.ToString();

            var baseGrav = Math.Round(totalPaid / 1.19m, 0);
            var iva = totalPaid - baseGrav;

            BaseGravableStr = $"{baseGrav:N0}";
            Iva19Str = $"{iva:N0}";
            TotalStr = $"{totalPaid:N0}";

            AttendedBy = !string.IsNullOrWhiteSpace(ticket.OperatorName)
                ? ticket.OperatorName.ToUpperInvariant()
                : (_sessionService.CurrentUser?.FullName?.ToUpperInvariant() ?? "MERLIN");

            // 5. Factura Electrónica y QR
            if (IsFvmInvoice)
            {
                CustomerName = "CONSUMIDOR FINAL";
                CustomerDocument = "CC 222222222";
                CustomerNit = "22222222";
                CustomerAddress = "CR 38 19 55 BRR CAMOA";

                InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : "FVM";
                var currentNum = resolution != null ? resolution.CurrentNumber.ToString() : (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber) ? ticket.InvoiceNumber : ticket.TicketNumber);
                InvoiceNumberStr = currentNum.PadLeft(8, '0');
                InvoiceNumberText = $"{InvoicePrefix}-{InvoiceNumberStr}";

                InvoiceDateStr = exitTime.ToString("dd/MM/yy");
                InvoiceTimeStr = exitTime.ToString("HH:mm:ss");

                Cufe = GenerateCufe($"{InvoicePrefix}{InvoiceNumberStr}", exitTime, totalPaid, BranchNit);

                var resNum = !string.IsNullOrWhiteSpace(resolution?.ResolutionNumber) ? resolution.ResolutionNumber : "18764000000";
                var validFromStr = resolution != null ? resolution.ValidFrom.ToString("yyyy/MM/dd") : "2024/06/18";
                DianResolutionText = $"RES DIAN Nº {resNum} DE {validFromStr} Vig. 24 meses";

                var fromNum = resolution?.FromNumber > 0 ? resolution.FromNumber : 1;
                var toNum = resolution?.ToNumber > 0 ? resolution.ToNumber : 5000000;
                DianRangeText = $"Autorización del {InvoicePrefix}-{fromNum} hasta {InvoicePrefix}-{toNum}";

                var qrContent = $"NumFac: {InvoicePrefix}-{InvoiceNumberStr}\nFecFac: {InvoiceDateStr} {InvoiceTimeStr}\nNitFac: {BranchNit}\nDocAdq: {CustomerNit}\nValFac: {totalPaid:F2}\nValIva: {iva:F2}\nCUFE: {Cufe}";
                ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(qrContent, 6);
                ElectronicInvoiceQrImage = ConsultationQrCodeImage;
            }
            else
            {
                ElectronicInvoiceQrImage = null;
                ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(PublicConsultationUrl, 8);
            }

            BarcodeImage = null;
        }
        else
        {
            // Tiquete de Entrada
            IsFvmInvoice = false;
            IsStandardExitReceipt = false;
            BarcodeImage = Services.Implementations.BarcodeGeneratorService.GenerateCode128(ticket.PlateNumber);
            ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(PublicConsultationUrl, 8);
            ElectronicInvoiceQrImage = null;
        }
    }

    private static string GenerateCufe(string numFac, DateTime fechaFac, decimal valFac, string nitEmisor)
    {
        var cleanNit = nitEmisor.Replace("NIT:", "", StringComparison.OrdinalIgnoreCase).Replace("NIT.", "", StringComparison.OrdinalIgnoreCase).Trim();
        var rawData = $"{numFac}{fechaFac:yyyyMMddHHmmss}{valFac:F2}010.00040.0003{valFac:F2}{cleanNit}22222222";
        using var sha = System.Security.Cryptography.SHA384.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    [RelayCommand]
    private async Task PrintTicketAsync()
    {
        if (IsPrinting)
        {
            return;
        }

        IsPrinting = true;
        try
        {
            if (Ticket.Status == Core.Enums.TicketStatus.Completed || Ticket.ExitTimeUtc.HasValue || Ticket.ExitTime.HasValue)
            {
                PrintSuccess = await _printerService.PrintExitReceiptAsync(Ticket);
            }
            else
            {
                PrintSuccess = await _printerService.PrintEntryTicketAsync(Ticket);
            }
        }
        finally
        {
            IsPrinting = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
