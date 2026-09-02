using System;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class ReceiptPreviewViewModel : ViewModelBase
{
    private readonly IReceiptPrinterService _printerService;
    private readonly ISessionService _sessionService;

    [ObservableProperty]
    private ParkingTicket _ticket = new();

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private bool _printSuccess;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _barcodeImage;

    [ObservableProperty]
    private string _branchName = "PARKING FLOW";

    [ObservableProperty]
    private string _branchNit = "NIT: 900.914.246-2";

    [ObservableProperty]
    private string _branchAddress = "CALLE 26 #57-83";

    [ObservableProperty]
    private string _formattedRateText = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _consultationQrCodeImage;

    [ObservableProperty]
    private BillingResolution? _resolution;

    [ObservableProperty]
    private bool _isFvmInvoice;

    [ObservableProperty]
    private string _branchPhone = "Tel. 318 181818 - 301 301301301";

    [ObservableProperty]
    private string _invoicePrefix = "FVM";

    [ObservableProperty]
    private string _invoiceNumberStr = "991000031";

    [ObservableProperty]
    private string _invoiceDateStr = string.Empty;

    [ObservableProperty]
    private string _invoiceTimeStr = string.Empty;

    [ObservableProperty]
    private string _customerName = "CONSUMIDOR FINAL";

    [ObservableProperty]
    private string _customerNit = "22222222";

    [ObservableProperty]
    private string _customerAddress = "CR 38 19 55 BRR CAMOA";

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

    public string PublicConsultationUrl => "https://www.parking-flow.com/mockup-consulta";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(IReceiptPrinterService printerService, ISessionService sessionService)
    {
        _printerService = printerService;
        _sessionService = sessionService;
    }

    public void LoadTicket(ParkingTicket ticket, BillingResolution? resolution = null)
    {
        Ticket = ticket;
        Resolution = resolution;
        PrintSuccess = false;
        BarcodeImage = Services.Implementations.BarcodeGeneratorService.GenerateCode128(ticket.PlateNumber);

        var currentBranch = _sessionService.CurrentBranch;
        BranchName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name.ToUpperInvariant() : "PARQUEADERO MERLIN";
        BranchAddress = !string.IsNullOrWhiteSpace(currentBranch?.Address) ? currentBranch.Address.ToUpperInvariant() : "CALLE 18 18-18";
        BranchNit = !string.IsNullOrWhiteSpace(currentBranch?.Notes) && currentBranch.Notes.StartsWith("NIT", StringComparison.OrdinalIgnoreCase)
            ? currentBranch.Notes
            : "NIT. 900900900-9";
        BranchPhone = "Tel. 318 181818 - 301 301301301";

        FormattedRateText = $"TARIFA: {ticket.HourlyRate:C0} / HORA";

        // Determinar si aplica diseño Factura de Venta Electrónica (FVM)
        // Aplica si se especificó resolución FVM o si el prefijo/documentType contiene "FVM"
        IsFvmInvoice = ticket.ExitTime.HasValue && (
            resolution != null && (
                resolution.Prefix.Contains("FVM", StringComparison.OrdinalIgnoreCase) ||
                resolution.DocumentType.Contains("FVM", StringComparison.OrdinalIgnoreCase) ||
                resolution.Name.Contains("FVM", StringComparison.OrdinalIgnoreCase)
            ) ||
            string.Equals(resolution?.Prefix, "A1PQ", StringComparison.OrdinalIgnoreCase)
        );

        if (IsFvmInvoice)
        {
            var exitTime = ticket.ExitTime ?? DateTime.Now;
            var entryTime = ticket.EntryTime;

            InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : "FVM";
            var currentNum = resolution != null ? resolution.CurrentNumber.ToString() : ticket.TicketNumber;
            InvoiceNumberStr = currentNum.PadLeft(8, '0');

            InvoiceDateStr = exitTime.ToString("dd/MM/yy");
            InvoiceTimeStr = exitTime.ToString("HH:mm:ss");

            EntryTimeStr = entryTime.ToString("HH:mm:ss");
            EntryDateStr = entryTime.ToString("dd/MM/yy");

            ExitTimeStr = exitTime.ToString("HH:mm:ss");
            ExitDateStr = exitTime.ToString("dd/MM/yy");

            var duration = exitTime - entryTime;
            var totalMins = Math.Max(1, (long)Math.Round(duration.TotalMinutes));
            DurationMinutesStr = totalMins.ToString();

            // Liquidación Fiscal (IVA 19% incluido)
            var total = ticket.NetAmount > 0 ? ticket.NetAmount : (ticket.TotalAmount > 0 ? ticket.TotalAmount : 5280m);

            var baseGrav = Math.Round(total / 1.19m, 0);
            var iva = total - baseGrav;

            BaseGravableStr = $"{baseGrav:N0}";
            Iva19Str = $"{iva:N0}";
            TotalStr = $"{total:N0}";

            AttendedBy = !string.IsNullOrWhiteSpace(ticket.OperatorName) 
                ? ticket.OperatorName.ToUpperInvariant() 
                : (_sessionService.CurrentUser?.FullName?.ToUpperInvariant() ?? "MERLIN");

            PaymentMethodName = ticket.PaymentMethod switch
            {
                Core.Enums.PaymentMethod.DigitalTransfer => "TRANSFERENCIA",
                Core.Enums.PaymentMethod.CreditCard => "TARJETA CREDITO",
                Core.Enums.PaymentMethod.DebitCard => "TARJETA DEBITO",
                _ => "CONTADO"
            };

            Cufe = GenerateCufe($"{InvoicePrefix}{InvoiceNumberStr}", exitTime, total, BranchNit);

            var resNum = !string.IsNullOrWhiteSpace(resolution?.ResolutionNumber) ? resolution.ResolutionNumber : "18764000000";
            var validFromStr = resolution != null ? resolution.ValidFrom.ToString("yyyy/MM/dd") : "2024/06/18";
            DianResolutionText = $"RES DIAN Nº {resNum} DE {validFromStr} Vig. 24 meses";

            var fromNum = resolution?.FromNumber > 0 ? resolution.FromNumber : 1;
            var toNum = resolution?.ToNumber > 0 ? resolution.ToNumber : 5000000;
            DianRangeText = $"Autorización del {InvoicePrefix}-{fromNum} hasta {InvoicePrefix}-{toNum}";

            // Generar QR para la factura electrónica con datos del CUFE
            var qrContent = $"NumFac: {InvoicePrefix}-{InvoiceNumberStr}\nFecFac: {InvoiceDateStr} {InvoiceTimeStr}\nNitFac: {BranchNit}\nDocAdq: {CustomerNit}\nValFac: {total:F2}\nValIva: {iva:F2}\nCUFE: {Cufe}";
            ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(qrContent, 6);
        }
        else
        {
            ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode("https://www.parking-flow.com/mockup-consulta", 8);
        }
    }

    private static string GenerateCufe(string numFac, DateTime fechaFac, decimal valFac, string nitEmisor)
    {
        var rawData = $"{numFac}{fechaFac:yyyyMMddHHmmss}{valFac:F2}010.00040.0003{valFac:F2}{nitEmisor}22222222";
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
            if (Ticket.Status == Core.Enums.TicketStatus.Completed)
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
