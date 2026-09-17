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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
    private readonly IPricingCalculatorService _pricingCalculator;

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
    private string _branchName = string.Empty;

    [ObservableProperty]
    private string _branchNit = string.Empty;

    [ObservableProperty]
    private string _branchAddress = string.Empty;

    [ObservableProperty]
    private string _branchPhone = string.Empty;

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
    private bool _hasLostTicketSurcharge;

    [ObservableProperty]
    private string _lostTicketFeeText = string.Empty;

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
    private string _attendedBy = "OPERADOR";

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

    [ObservableProperty]
    private int _paperWidth = 80;

    [ObservableProperty]
    private string _paperWidthBadgeText = "Formato: 80 mm";

    [ObservableProperty]
    private bool _is58Mm;

    [ObservableProperty]
    private double _dialogWindowWidth = 490;

    [ObservableProperty]
    private double _paperContainerWidth = 380;

    [ObservableProperty]
    private double _barcodeWidth = 260;

    [ObservableProperty]
    private double _qrCodeWidth = 110;

    [ObservableProperty]
    private double _monospaceFontSize = 11;

    [ObservableProperty]
    private double _monospaceTitleFontSize = 15;

    [ObservableProperty]
    private double _plateFontSize = 16;

    [ObservableProperty]
    private string _publicConsultationUrl = "https://www.parking-flow.com/consulta";

    [ObservableProperty]
    private string _consultationDomainText = "www.parking-flow.com/consulta";

    public event Action? RequestClose;

    public ReceiptPreviewViewModel(
        IReceiptPrinterService printerService,
        ISessionService sessionService,
        IDbConnectionManager connectionManager,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IPricingCalculatorService pricingCalculator)
    {
        _printerService = printerService;
        _sessionService = sessionService;
        _connectionManager = connectionManager;
        _configuration = configuration;
        _pricingCalculator = pricingCalculator;
    }

    public void LoadTicket(ParkingTicket ticket, BillingResolution? resolution = null)
    {
        Ticket = ticket;
        Resolution = resolution;
        PrintSuccess = false;

        var currentBranch = _sessionService.CurrentBranch;
        var width = currentBranch?.PaperWidth > 0 ? currentBranch.PaperWidth : 80;
        PaperWidth = width;
        Is58Mm = width <= 58;
        PaperWidthBadgeText = $"Formato: {width} mm";

        if (Is58Mm)
        {
            DialogWindowWidth = 390;
            PaperContainerWidth = 280;
            BarcodeWidth = 200;
            QrCodeWidth = 85;
            MonospaceFontSize = 9.5;
            MonospaceTitleFontSize = 13;
            PlateFontSize = 14;
        }
        else
        {
            DialogWindowWidth = 490;
            PaperContainerWidth = 380;
            BarcodeWidth = 260;
            QrCodeWidth = 110;
            MonospaceFontSize = 11;
            MonospaceTitleFontSize = 15;
            PlateFontSize = 16;
        }

        BranchName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name.ToUpperInvariant() : "PARQUEADERO";
        BranchAddress = !string.IsNullOrWhiteSpace(currentBranch?.Address) ? currentBranch.Address.ToUpperInvariant() : string.Empty;

        // 1. NIT de la Empresa configurada desde el PWA
        var rawNit = _sessionService.CurrentBranch?.CompanyNit
            ?? _sessionService.CurrentUser?.CompanyNit
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(rawNit))
        {
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? ticket.BranchId;
                if (branchId > 0)
                {
                    rawNit = db.Branches.Where(b => b.Id == branchId).Select(b => b.CompanyNit).FirstOrDefault() ?? string.Empty;
                }
                if (string.IsNullOrWhiteSpace(rawNit))
                {
                    rawNit = db.Branches.Where(b => !string.IsNullOrWhiteSpace(b.CompanyNit)).Select(b => b.CompanyNit).FirstOrDefault() ?? string.Empty;
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(rawNit) && !string.IsNullOrWhiteSpace(currentBranch?.Notes) && currentBranch.Notes.StartsWith("NIT", StringComparison.OrdinalIgnoreCase))
        {
            rawNit = currentBranch.Notes;
        }

        BranchNit = !string.IsNullOrWhiteSpace(rawNit)
            ? (rawNit.StartsWith("NIT", StringComparison.OrdinalIgnoreCase) ? rawNit.ToUpperInvariant() : $"NIT. {rawNit}".ToUpperInvariant())
            : string.Empty;

        // 2. Teléfono de la Sede configurado desde el PWA
        var rawPhone = currentBranch?.Phone ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? ticket.BranchId;
                if (branchId > 0)
                {
                    rawPhone = db.Branches.Where(b => b.Id == branchId).Select(b => b.Phone).FirstOrDefault() ?? string.Empty;
                }
            }
            catch { }
        }

        BranchPhone = !string.IsNullOrWhiteSpace(rawPhone)
            ? (rawPhone.StartsWith("Tel", StringComparison.OrdinalIgnoreCase) ? rawPhone : $"Tel. {rawPhone}")
            : string.Empty;

        // 3. Atendido por (Usuario logueado en WPF que realizó el ingreso)
        AttendedBy = !string.IsNullOrWhiteSpace(ticket.OperatorName)
            ? ticket.OperatorName.ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.Username)
                    ? _sessionService.CurrentUser.Username.ToUpperInvariant()
                    : "OPERADOR"));

        // 4. Tarifas de la Sede configuradas desde el PWA
        VehicleRate? rate = null;
        try
        {
            rate = _pricingCalculator.GetRate(ticket.VehicleType);
            if (rate == null)
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? ticket.BranchId;
                rate = db.VehicleRates.FirstOrDefault(r => (r.BranchId == branchId || r.BranchId == null) && r.VehicleType == ticket.VehicleType && r.IsActive);
            }
        }
        catch { }

        var hourRate = (rate != null && rate.HourRate > 0) ? rate.HourRate : ticket.HourlyRate;
        var minuteRate = rate?.MinuteRate ?? 0m;
        var fullDayRate = rate?.FullDayRate ?? 0m;
        var nightRate = rate?.NightRate ?? 0m;

        if (hourRate > 0 && minuteRate > 0)
        {
            FormattedRateText = $"TARIFA: {hourRate:C0} / HORA  |  {minuteRate:C0} / MIN";
        }
        else if (hourRate > 0)
        {
            FormattedRateText = $"TARIFA: {hourRate:C0} / HORA";
        }
        else if (minuteRate > 0)
        {
            FormattedRateText = $"TARIFA: {minuteRate:C0} / MINUTO";
        }
        else if (fullDayRate > 0)
        {
            FormattedRateText = $"TARIFA DÍA: {fullDayRate:C0}";
        }
        else if (nightRate > 0)
        {
            FormattedRateText = $"TARIFA NOCHE: {nightRate:C0}";
        }
        else
        {
            // Fallback a cualquier tarifa activa de la sede
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? ticket.BranchId;
                var anyRate = db.VehicleRates.FirstOrDefault(r => (r.BranchId == branchId || r.BranchId == null) && r.IsActive && (r.HourRate > 0 || r.MinuteRate > 0));
                if (anyRate != null)
                {
                    if (anyRate.HourRate > 0 && anyRate.MinuteRate > 0)
                        FormattedRateText = $"TARIFA: {anyRate.HourRate:C0} / HORA  |  {anyRate.MinuteRate:C0} / MIN";
                    else if (anyRate.HourRate > 0)
                        FormattedRateText = $"TARIFA: {anyRate.HourRate:C0} / HORA";
                    else
                        FormattedRateText = $"TARIFA: {anyRate.MinuteRate:C0} / MINUTO";
                }
                else
                {
                    FormattedRateText = ticket.HourlyRate > 0 ? $"TARIFA: {ticket.HourlyRate:C0} / HORA" : string.Empty;
                }
            }
            catch
            {
                FormattedRateText = ticket.HourlyRate > 0 ? $"TARIFA: {ticket.HourlyRate:C0} / HORA" : string.Empty;
            }
        }

        if (fullDayRate > 0 && nightRate > 0 && hourRate > 0)
        {
            FormattedRateText += $"\nPLENA: {fullDayRate:C0} | NOCHE: {nightRate:C0}";
        }
        else if (fullDayRate > 0 && hourRate > 0)
        {
            FormattedRateText += $"\nTARIFA PLENA: {fullDayRate:C0}";
        }
        else if (nightRate > 0 && hourRate > 0)
        {
            FormattedRateText += $"\nTARIFA NOCTURNA: {nightRate:C0}";
        }

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

            // 3.1 Recargo por Tiquete Extraviado
            HasLostTicketSurcharge = ticket.IsLostTicket && ticket.LostTicketFee > 0;
            LostTicketFeeText = HasLostTicketSurcharge ? $"{ticket.LostTicketFee:C0}" : string.Empty;

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
                : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                    ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                    : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.Username)
                        ? _sessionService.CurrentUser.Username.ToUpperInvariant()
                        : "OPERADOR"));

            // 5. Datos de Factura vs POS Estándar
            CustomerName = "CONSUMIDOR FINAL";
            CustomerDocument = "CC 222222222";
            CustomerNit = "22222222";
            CustomerAddress = "CR 38 19 55 BRR CAMOA";

            if (ticket.CustomerId.HasValue)
            {
                try
                {
                    using var db = _connectionManager.CreateDbContext();
                    var cust = db.Customers.FirstOrDefault(c => c.CustomerId == ticket.CustomerId.Value);
                    if (cust != null)
                    {
                        CustomerName = cust.FullName.ToUpperInvariant();
                        CustomerDocument = $"{cust.DocumentNumber}{(string.IsNullOrWhiteSpace(cust.CheckDigit) ? "" : "-" + cust.CheckDigit)}";
                        CustomerNit = cust.DocumentNumber;
                        CustomerAddress = !string.IsNullOrWhiteSpace(cust.Address) ? cust.Address.ToUpperInvariant() : "NO REGISTRADA";
                    }
                }
                catch { }
            }

            if (ticket.IsElectronicInvoice || isFvm)
            {
                IsFvmInvoice = true;
                IsStandardExitReceipt = false;

                InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber) && ticket.InvoiceNumber.Contains('-') ? ticket.InvoiceNumber.Split('-')[0] : "FE");
                var currentNum = resolution != null ? resolution.CurrentNumber.ToString() : (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber) ? ticket.InvoiceNumber : ticket.TicketNumber);
                InvoiceNumberStr = currentNum.PadLeft(8, '0');
                InvoiceNumberText = $"{InvoicePrefix}- {InvoiceNumberStr}";

                InvoiceDateStr = exitTime.ToString("dd/MM/yy");
                InvoiceTimeStr = exitTime.ToString("HH:mm:ss");

                Cufe = !string.IsNullOrWhiteSpace(ticket.Cufe)
                    ? ticket.Cufe
                    : GenerateCufe($"{InvoicePrefix}{InvoiceNumberStr}", exitTime, totalPaid, BranchNit);

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
                InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : "POS";
                var currentNum = resolution != null ? resolution.CurrentNumber.ToString() : (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber) ? ticket.InvoiceNumber : ticket.TicketNumber);
                InvoiceNumberStr = currentNum.PadLeft(8, '0');
                InvoiceNumberText = $"{InvoicePrefix}- {InvoiceNumberStr}";

                InvoiceDateStr = exitTime.ToString("dd/MM/yy");
                InvoiceTimeStr = exitTime.ToString("HH:mm:ss");

                Cufe = string.Empty;
                DianResolutionText = string.Empty;
                DianRangeText = string.Empty;
                ElectronicInvoiceQrImage = null;

                var qrContent = $"Recibo: {InvoiceNumberText}\nPlaca: {ticket.PlateNumber}\nEntrada: {EntryDateStr} {EntryTimeStr}\nSalida: {ExitDateStr} {ExitTimeStr}\nTotal: {totalPaid:C0}\nAtendido: {AttendedBy}";
                ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(qrContent, 6);
            }

            BarcodeImage = null;
        }
        else
        {
            // Tiquete de Entrada
            IsFvmInvoice = false;
            IsStandardExitReceipt = false;
            InvoiceNumberText = ticket.TicketNumber;
            InvoiceDateStr = (ticket.EntryTime != default ? ticket.EntryTime : DateTime.Now).ToString("dd/MM/yy");
            InvoiceTimeStr = (ticket.EntryTime != default ? ticket.EntryTime : DateTime.Now).ToString("HH:mm:ss");
            BarcodeImage = Services.Implementations.BarcodeGeneratorService.GenerateCode128(ticket.PlateNumber);

            var pwaBase = _configuration?["PwaSettings:BaseUrl"]?.TrimEnd('/') ?? "https://www.parking-flow.com";
            PublicConsultationUrl = $"{pwaBase}/consulta?plate={Uri.EscapeDataString(ticket.PlateNumber)}&ticket={Uri.EscapeDataString(ticket.TicketNumber)}";

            try
            {
                var uri = new Uri(pwaBase);
                ConsultationDomainText = $"{uri.Host}/consulta";
            }
            catch
            {
                ConsultationDomainText = "www.parking-flow.com/consulta";
            }

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
