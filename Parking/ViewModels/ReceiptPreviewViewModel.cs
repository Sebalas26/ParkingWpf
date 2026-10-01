using Parking.Models.ApiModels;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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
    private readonly IPrinterDiscoveryService? _printerDiscovery;
    private readonly IPermissionService? _permissionService;

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
    private bool _isShiftCloseReceipt;

    [ObservableProperty]
    private WorkShift? _shift;

    [ObservableProperty]
    private ShiftSummaryModel? _shiftSummary;

    [ObservableProperty]
    private string _shiftIdText = string.Empty;

    [ObservableProperty]
    private string _cashierName = string.Empty;

    [ObservableProperty]
    private string _shiftStartDateStr = string.Empty;

    [ObservableProperty]
    private string _shiftStartTimeStr = string.Empty;

    [ObservableProperty]
    private string _shiftEndDateStr = string.Empty;

    [ObservableProperty]
    private string _shiftEndTimeStr = string.Empty;

    [ObservableProperty]
    private string _shiftDurationStr = string.Empty;

    [ObservableProperty]
    private bool _hasSubscriptionsModule = true;

    [ObservableProperty]
    private int _shiftDiscountTicketsCount;

    [ObservableProperty]
    private ObservableCollection<ShiftPaymentMethodItem> _shiftPaymentMethods = new();

    [ObservableProperty]
    private string _shiftParqueosAmountStr = "$ 0";

    [ObservableProperty]
    private string _shiftMensualidadesAmountStr = "$ 0";

    [ObservableProperty]
    private int _shiftMensualidadesCount;

    [ObservableProperty]
    private string _shiftCashInflowsStr = "$ 0";

    [ObservableProperty]
    private string _shiftCashOutflowsStr = "$ 0";

    [ObservableProperty]
    private string _shiftGrossSubtotalStr = "$ 0";

    [ObservableProperty]
    private string _shiftDiscountsStr = "$ 0";

    [ObservableProperty]
    private string _shiftDiscountedSubtotalStr = "$ 0";

    [ObservableProperty]
    private string _shiftTaxBaseStr = "$ 0";

    [ObservableProperty]
    private string _shiftIva19Str = "$ 0";

    [ObservableProperty]
    private string _shiftTotalRevenueStr = "$ 0";

    [ObservableProperty]
    private string _shiftCashCollectedStr = "$ 0";

    [ObservableProperty]
    private string _shiftCardCollectedStr = "$ 0";

    [ObservableProperty]
    private string _shiftTransferCollectedStr = "$ 0";

    [ObservableProperty]
    private int _shiftVehiclesExitedCount;

    [ObservableProperty]
    private int _shiftVehiclesInYardCount;

    [ObservableProperty]
    private string _shiftBaseAmountStr = "$ 0";

    [ObservableProperty]
    private string _shiftExpectedCashStr = "$ 0";

    [ObservableProperty]
    private string _shiftActualCashStr = "$ 0";

    [ObservableProperty]
    private string _shiftDifferenceStr = "$ 0";

    [ObservableProperty]
    private string _shiftArqueoStatusText = "CUADRADA";

    [ObservableProperty]
    private string _shiftNotes = string.Empty;

    [ObservableProperty]
    private bool _hasShiftNotes;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _barcodeImage;

    [ObservableProperty]
    private string? _branchLogoBase64;

    [ObservableProperty]
    private double _logoMaxHeight = 48;

    [ObservableProperty]
    private double _logoMaxWidth = 130;

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
    private string _vehicleTypeName = string.Empty;

    [ObservableProperty]
    private string _amountPaidStr = string.Empty;

    [ObservableProperty]
    private bool _hasAmountPaid;

    [ObservableProperty]
    private string _changeGivenStr = string.Empty;

    [ObservableProperty]
    private bool _hasChange;

    [ObservableProperty]
    private string _paymentMethodDisplayName = "Efectivo";

    [ObservableProperty]
    private bool _hasAgreement;

    [ObservableProperty]
    private string _agreementDisplayName = "NO APLICA";

    [ObservableProperty]
    private bool _hasDiscount;

    [ObservableProperty]
    private string _discountAmountStr = "$ 0";

    [ObservableProperty]
    private string? _ticketPolicy;

    [ObservableProperty]
    private string? _ticketAdditionalInfo;

    [ObservableProperty]
    private bool _hasTicketPolicy;

    [ObservableProperty]
    private bool _hasTicketAdditionalInfo;

    [ObservableProperty]
    private bool _hasTicketPolicyOrAdditionalInfo;

    [ObservableProperty]
    private string? _branchSchedule;

    [ObservableProperty]
    private bool _hasBranchSchedule;

    [ObservableProperty]
    private string _subtotalStr = string.Empty;

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
    private string _customerIdTypeLabel = "NIT:";

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
    private double _paperContainerWidth = 298;

    [ObservableProperty]
    private double _printableContentWidth = 270;

    [ObservableProperty]
    private Thickness _ticketPrintablePadding = new Thickness(15, 0, 10, 0);

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
    private double _ticketHeaderFontSize = 11;

    [ObservableProperty]
    private double _ticketNumberFontSize = 13;

    [ObservableProperty]
    private string _publicConsultationUrl = "https://www.parking-flow.com/consulta";

    [ObservableProperty]
    private string _consultationDomainText = "www.parking-flow.com/consulta";

    public event Action? RequestClose;

    [ObservableProperty]
    private string _detectedPrinterName = "Detectando impresora...";

    [ObservableProperty]
    private bool _hasConnectedPrinter;

    [ObservableProperty]
    private string? _printStatusMessage;

    public Func<Task<(bool Success, string? PrinterName, string? ErrorMessage)>>? DirectPrintHandler { get; set; }

    public ReceiptPreviewViewModel(
        IReceiptPrinterService printerService,
        ISessionService sessionService,
        IDbConnectionManager connectionManager,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        IPricingCalculatorService pricingCalculator,
        IPrinterDiscoveryService? printerDiscovery = null,
        IPermissionService? permissionService = null)
    {
        _printerService = printerService;
        _sessionService = sessionService;
        _connectionManager = connectionManager;
        _configuration = configuration;
        _pricingCalculator = pricingCalculator;
        _printerDiscovery = printerDiscovery;
        _permissionService = permissionService;

        RefreshDetectedPrinter();
    }

    public int ResolvePaperWidth(int? fallbackBranchId = null)
    {
        // 1. Si la impresora conectada o detectada contiene "58", priorizar formato 58 mm
        if (!string.IsNullOrWhiteSpace(DetectedPrinterName) &&
            (DetectedPrinterName.Contains("58", StringComparison.OrdinalIgnoreCase) ||
             DetectedPrinterName.Contains("POS-58", StringComparison.OrdinalIgnoreCase)))
        {
            return 58;
        }

        // 2. Si la sede activa tiene PaperWidth configurado
        var currentBranch = _sessionService.CurrentBranch;
        if (currentBranch?.PaperWidth > 0)
        {
            return currentBranch.PaperWidth;
        }

        // 3. Consultar base de datos si hay fallbackBranchId
        if (fallbackBranchId.HasValue && fallbackBranchId.Value > 0)
        {
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchWidth = db.Branches.Where(b => b.Id == fallbackBranchId.Value).Select(b => b.PaperWidth).FirstOrDefault();
                if (branchWidth > 0)
                {
                    return branchWidth;
                }
            }
            catch { }
        }

        // 4. Default a 80 mm
        return 80;
    }

    public void ApplyPaperMetrics(int width)
    {
        PaperWidth = width;
        Is58Mm = width <= 58;
        PaperWidthBadgeText = Is58Mm ? "Formato: 58 mm ⇄" : "Formato: 80 mm ⇄";

        if (Is58Mm)
        {
            PrintableContentWidth = 168;
            PaperContainerWidth = 200;
            DialogWindowWidth = 430;
            TicketPrintablePadding = new Thickness(15, 0, 10, 0);
            BarcodeWidth = 135;
            QrCodeWidth = 75;
            MonospaceFontSize = 8.0;
            MonospaceTitleFontSize = 10.0;
            PlateFontSize = 12.0;
            TicketHeaderFontSize = 7.5;
            TicketNumberFontSize = 8.0;
            LogoMaxHeight = 30;
            LogoMaxWidth = 90;
        }
        else
        {
            PrintableContentWidth = 265;
            PaperContainerWidth = 300;
            DialogWindowWidth = 500;
            TicketPrintablePadding = new Thickness(10, 0, 10, 0);
            BarcodeWidth = 210;
            QrCodeWidth = 95;
            MonospaceFontSize = 10.0;
            MonospaceTitleFontSize = 13.0;
            PlateFontSize = 15.0;
            TicketHeaderFontSize = 10.0;
            TicketNumberFontSize = 12.0;
            LogoMaxHeight = 44;
            LogoMaxWidth = 130;
        }
    }

    [RelayCommand]
    private void TogglePaperWidth()
    {
        var targetWidth = Is58Mm ? 80 : 58;
        ApplyPaperMetrics(targetWidth);
    }

    public void RefreshDetectedPrinter()
    {
        try
        {
            if (_printerDiscovery != null)
            {
                var q = _printerDiscovery.ResolveConnectedPrinter();
                if (q != null)
                {
                    DetectedPrinterName = q.Name;
                    HasConnectedPrinter = !q.IsOffline;
                    PrintStatusMessage = q.IsOffline ? "Impresora desconectada" : $"Lista en {q.Name}";
                }
                else
                {
                    DetectedPrinterName = "Sin impresora conectada";
                    HasConnectedPrinter = false;
                    PrintStatusMessage = "No se detectó impresora";
                }
            }
            else if (string.IsNullOrWhiteSpace(DetectedPrinterName) || DetectedPrinterName == "Detectando impresora...")
            {
                DetectedPrinterName = "Impresora estándar";
                HasConnectedPrinter = true;
            }
        }
        catch
        {
            DetectedPrinterName = "Sin impresora";
            HasConnectedPrinter = false;
        }
    }

    public void LoadTicket(ParkingTicket ticket, BillingResolution? resolution = null)
    {
        Ticket = ticket;
        Resolution = resolution;
        PrintSuccess = false;
        PrintStatusMessage = null;
        RefreshDetectedPrinter();
        IsShiftCloseReceipt = false;
        Shift = null;
        ShiftSummary = null;
        BranchSchedule = null;
        HasBranchSchedule = false;

        var currentBranch = _sessionService.CurrentBranch;
        var initialWidth = ResolvePaperWidth(ticket.BranchId);
        ApplyPaperMetrics(initialWidth);

        var rawLogo = currentBranch?.LogoBase64;
        if (string.IsNullOrWhiteSpace(rawLogo))
        {
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? ticket.BranchId;
                if (branchId > 0)
                {
                    rawLogo = db.Branches.Where(b => b.Id == branchId && !string.IsNullOrWhiteSpace(b.LogoBase64)).Select(b => b.LogoBase64).FirstOrDefault();
                }
                if (string.IsNullOrWhiteSpace(rawLogo))
                {
                    rawLogo = db.Branches.Where(b => !string.IsNullOrWhiteSpace(b.LogoBase64)).Select(b => b.LogoBase64).FirstOrDefault();
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(rawLogo))
        {
            rawLogo = "pack://application:,,,/Parking;component/Resources/logo.jpeg";
        }

        BranchLogoBase64 = rawLogo;
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
        if (IsExitReceipt)
        {
            AttendedBy = !string.IsNullOrWhiteSpace(ticket.ExitOperatorName)
                ? ticket.ExitOperatorName.ToUpperInvariant()
                : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                    ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                    : (!string.IsNullOrWhiteSpace(ticket.OperatorName)
                        ? ticket.OperatorName.ToUpperInvariant()
                        : "OPERADOR"));
        }
        else
        {
            AttendedBy = !string.IsNullOrWhiteSpace(ticket.OperatorName)
                ? ticket.OperatorName.ToUpperInvariant()
                : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                    ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                    : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.Username)
                        ? _sessionService.CurrentUser.Username.ToUpperInvariant()
                        : "OPERADOR"));
        }

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

        var vType = ticket.VehicleType;
        string resolvedName = rate?.DisplayName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedName) || resolvedName.Equals("Car", StringComparison.OrdinalIgnoreCase))
        {
            resolvedName = vType switch
            {
                VehicleType.Car => "AUTOMÓVIL",
                VehicleType.Motorcycle => "MOTOCICLETA",
                VehicleType.Truck => "VEHÍCULO PESADO",
                VehicleType.Van => "FURGÓN",
                VehicleType.Bicycle => "BICICLETA",
                VehicleType.Suv => "CAMIONETA / SUV",
                _ => "AUTOMÓVIL"
            };
        }
        VehicleTypeName = resolvedName.ToUpperInvariant();

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
            // 1. Detectar si el ticket o la resolución es de Facturación Electrónica (100% data-driven)
            bool isElectronicInvoice = ticket.IsElectronicInvoice;

            if (!isElectronicInvoice && resolution != null)
            {
                isElectronicInvoice = resolution.IsElectronicResolution;
            }
            else if (!isElectronicInvoice && ticket.ResolutionId.HasValue)
            {
                try
                {
                    using var db = _connectionManager.CreateDbContext();
                    var dbRes = db.BillingResolutions.FirstOrDefault(r => r.ResolutionId == ticket.ResolutionId.Value);
                    if (dbRes != null)
                    {
                        isElectronicInvoice = dbRes.IsElectronicResolution;
                    }
                }
                catch { }
            }

            IsFvmInvoice = isElectronicInvoice;
            IsStandardExitReceipt = !isElectronicInvoice;

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

            // 3.1 Recargo por Tiquete Extraviado
            HasLostTicketSurcharge = ticket.IsLostTicket && ticket.LostTicketFee > 0;
            LostTicketFeeText = HasLostTicketSurcharge ? $"{ticket.LostTicketFee:C0}" : string.Empty;

            // 4. Valor que pagó y % IVA
            var totalPaid = ticket.DiscountAmount > 0
                ? ticket.NetAmount
                : (ticket.NetAmount > 0 ? ticket.NetAmount : (ticket.AmountPaid > 0 ? ticket.AmountPaid : (ticket.TotalAmount > 0 ? ticket.TotalAmount : ticket.GrossAmount)));
            FormattedTotalPaid = $"{totalPaid:C0}";
            IvaPercentageText = "19%";

            // 3. Resolver Convenio si aplica
            HasAgreement = false;
            HasDiscount = false;
            AgreementDisplayName = "NO APLICA";
            DiscountAmountStr = "$ 0";
            SubtotalStr = string.Empty;

            if (ticket.DiscountAmount > 0)
            {
                string? agreementName = null;
                try
                {
                    using var db = _connectionManager.CreateDbContext();
                    var discountEntity = db.TicketDiscounts
                        .Include(d => d.Agreement)
                        .Where(d => d.TicketId == ticket.TicketId)
                        .OrderByDescending(d => d.ValidatedAtUtc)
                        .FirstOrDefault();

                    if (discountEntity != null)
                    {
                        if (discountEntity.Agreement != null && !string.IsNullOrWhiteSpace(discountEntity.Agreement.Name))
                        {
                            agreementName = discountEntity.Agreement.Name;
                        }
                        else if (discountEntity.AgreementId != Guid.Empty)
                        {
                            var ag = db.CommercialAgreements.FirstOrDefault(a => a.AgreementId == discountEntity.AgreementId);
                            if (ag != null && !string.IsNullOrWhiteSpace(ag.Name))
                            {
                                agreementName = ag.Name;
                            }
                        }
                    }
                }
                catch { }

                // Fallback defensivo: si no se encontró en BD pero el tiquete tiene comprobante de convenio
                if (string.IsNullOrWhiteSpace(agreementName) && !string.IsNullOrWhiteSpace(ticket.InvoiceNumber) && ticket.InvoiceNumber.StartsWith("CONV-", StringComparison.OrdinalIgnoreCase))
                {
                    agreementName = ticket.InvoiceNumber.Substring(5).Trim();
                }

                HasAgreement = true;
                HasDiscount = true;
                AgreementDisplayName = !string.IsNullOrWhiteSpace(agreementName) ? agreementName.ToUpperInvariant() : "CONVENIO APLICADO";
                DiscountAmountStr = $"- {ticket.DiscountAmount:C0}";
                var gross = ticket.GrossAmount > totalPaid ? ticket.GrossAmount : (totalPaid + ticket.DiscountAmount);
                SubtotalStr = $"{gross:N0}";
            }

            var paid = ticket.AmountPaid > 0 ? ticket.AmountPaid : totalPaid;
            AmountPaidStr = $"{paid:N0}";
            HasAmountPaid = paid > 0 || (ticket.DiscountAmount > 0 && totalPaid == 0m);

            var change = ticket.ChangeGiven > 0 ? ticket.ChangeGiven : (paid > totalPaid ? paid - totalPaid : 0m);
            ChangeGivenStr = $"{change:N0}";
            HasChange = change > 0;

            var exitTime = ticket.ExitTime ?? (ticket.ExitTimeUtc.HasValue ? ticket.ExitTimeUtc.Value.ToLocalTime() : DateTime.Now);
            var entryTime = ticket.EntryTime != default ? ticket.EntryTime : (ticket.CreatedAtUtc != default ? ticket.CreatedAtUtc.ToLocalTime() : DateTime.Now);

            ExitTimeStr = exitTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            ExitDateStr = exitTime.ToString("dd/MM/yy");
            EntryTimeStr = entryTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
            EntryDateStr = entryTime.ToString("dd/MM/yy");

            var duration = exitTime - entryTime;
            var totalMins = Math.Max(1, (long)Math.Round(duration.TotalMinutes));
            DurationMinutesStr = totalMins.ToString();

            var baseGrav = Math.Round(totalPaid / 1.19m, 0);
            var iva = totalPaid - baseGrav;

            BaseGravableStr = $"{baseGrav:N0}";
            Iva19Str = $"{iva:N0}";
            TotalStr = $"{totalPaid:N0}";

            if (IsExitReceipt)
            {
                AttendedBy = !string.IsNullOrWhiteSpace(ticket.ExitOperatorName)
                    ? ticket.ExitOperatorName.ToUpperInvariant()
                    : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                        ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                        : (!string.IsNullOrWhiteSpace(ticket.OperatorName)
                            ? ticket.OperatorName.ToUpperInvariant()
                            : "OPERADOR"));
            }
            else
            {
                AttendedBy = !string.IsNullOrWhiteSpace(ticket.OperatorName)
                    ? ticket.OperatorName.ToUpperInvariant()
                    : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.FullName)
                        ? _sessionService.CurrentUser.FullName.ToUpperInvariant()
                        : (!string.IsNullOrWhiteSpace(_sessionService.CurrentUser?.Username)
                            ? _sessionService.CurrentUser.Username.ToUpperInvariant()
                            : "OPERADOR"));
            }

            // 5. Datos de Factura vs POS Estándar
            CustomerName = "CONSUMIDOR FINAL";
            CustomerDocument = "222222222222";
            CustomerNit = "222222222222";
            CustomerIdTypeLabel = "NIT:";
            CustomerAddress = !string.IsNullOrWhiteSpace(BranchAddress) ? BranchAddress : "NO REGISTRADA";

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
                        CustomerIdTypeLabel = ResolveIdTypeLabel(cust.IdentificationTypeId, cust.PersonType);
                        CustomerAddress = !string.IsNullOrWhiteSpace(cust.Address) ? cust.Address.ToUpperInvariant() : "NO REGISTRADA";
                    }
                }
                catch { }
            }

            if (ticket.IsElectronicInvoice || isElectronicInvoice)
            {
                IsFvmInvoice = true;
                IsStandardExitReceipt = false;

                if (!string.IsNullOrWhiteSpace(ticket.InvoiceNumber))
                {
                    InvoiceNumberText = ticket.InvoiceNumber;
                    if (ticket.InvoiceNumber.Contains('-'))
                    {
                        var parts = ticket.InvoiceNumber.Split('-');
                        InvoicePrefix = parts[0];
                        InvoiceNumberStr = string.Join("-", parts.Skip(1));
                    }
                    else
                    {
                        InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : string.Empty;
                        InvoiceNumberStr = ticket.InvoiceNumber;
                    }
                }
                else
                {
                    InvoicePrefix = !string.IsNullOrWhiteSpace(resolution?.Prefix) ? resolution.Prefix : string.Empty;
                    var currentNum = resolution != null ? resolution.CurrentNumber.ToString() : ticket.TicketNumber;
                    InvoiceNumberStr = currentNum.PadLeft(8, '0');
                    InvoiceNumberText = string.IsNullOrWhiteSpace(InvoicePrefix) ? InvoiceNumberStr : $"{InvoicePrefix}- {InvoiceNumberStr}";
                }

                InvoiceDateStr = exitTime.ToString("dd/MM/yy");
                InvoiceTimeStr = exitTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);

                Cufe = !string.IsNullOrWhiteSpace(ticket.Cufe)
                    ? ticket.Cufe
                    : GenerateCufe($"{InvoicePrefix}{InvoiceNumberStr}", exitTime, totalPaid, BranchNit);

                var resNum = !string.IsNullOrWhiteSpace(resolution?.ResolutionNumber) ? resolution.ResolutionNumber : "18764000000";
                var validFromStr = resolution != null ? resolution.ValidFrom.ToString("yyyy/MM/dd") : "2024/06/18";
                DianResolutionText = $"RES DIAN Nº {resNum} DE {validFromStr} Vig. 24 meses";

                var fromNum = resolution?.FromNumber > 0 ? resolution.FromNumber : 1;
                var toNum = resolution?.ToNumber > 0 ? resolution.ToNumber : 5000000;
                DianRangeText = $"Autorización del {InvoicePrefix}-{fromNum} hasta {InvoicePrefix}-{toNum}";

                var qrContent = !string.IsNullOrWhiteSpace(ticket.QrCodeData)
                    ? ticket.QrCodeData
                    : $"NumFac: {InvoicePrefix}-{InvoiceNumberStr}\nFecFac: {InvoiceDateStr} {InvoiceTimeStr}\nNitFac: {BranchNit}\nDocAdq: {CustomerNit}\nValFac: {totalPaid:F2}\nValIva: {iva:F2}\nCUFE: {Cufe}";
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
                InvoiceTimeStr = exitTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);

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
            AmountPaidStr = string.Empty;
            HasAmountPaid = false;
            ChangeGivenStr = string.Empty;
            HasChange = false;
            InvoiceNumberText = string.IsNullOrWhiteSpace(ticket.TicketNumber) ? string.Empty : (ticket.TicketNumber.StartsWith("#") ? ticket.TicketNumber : $"#{ticket.TicketNumber}");
            InvoiceDateStr = (ticket.EntryTime != default ? ticket.EntryTime : DateTime.Now).ToString("dd/MM/yy");
            InvoiceTimeStr = (ticket.EntryTime != default ? ticket.EntryTime : DateTime.Now).ToString("hh:mm tt", CultureInfo.InvariantCulture);
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

        // Evaluar políticas de impresión para Información Adicional y Póliza (asociadas a la Sede o Resolución de Facturación)
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var branchId = currentBranch?.Id ?? ticket.BranchId ?? _sessionService.CurrentBranch?.Id;
            string? branchPolicy = currentBranch?.TicketPolicy ?? _sessionService.CurrentBranch?.TicketPolicy;
            bool branchPrintPolicyOnEntry = currentBranch?.PrintPolicyOnEntry ?? _sessionService.CurrentBranch?.PrintPolicyOnEntry ?? false;
            string? branchAdditionalInfo = currentBranch?.TicketAdditionalInfo ?? _sessionService.CurrentBranch?.TicketAdditionalInfo;
            bool branchPrintAdditionalInfoOnEntry = currentBranch?.PrintAdditionalInfoOnEntry ?? _sessionService.CurrentBranch?.PrintAdditionalInfoOnEntry ?? false;
            string? branchSchedule = currentBranch?.TicketSchedule ?? _sessionService.CurrentBranch?.TicketSchedule;
            bool branchPrintScheduleOnEntry = currentBranch?.PrintScheduleOnEntry ?? _sessionService.CurrentBranch?.PrintScheduleOnEntry ?? false;

            if (string.IsNullOrWhiteSpace(branchPolicy) && string.IsNullOrWhiteSpace(branchAdditionalInfo) && string.IsNullOrWhiteSpace(branchSchedule) && branchId.HasValue)
            {
                var dbBranch = db.Branches.FirstOrDefault(b => b.Id == branchId.Value);
                if (dbBranch != null)
                {
                    branchPolicy = dbBranch.TicketPolicy;
                    branchPrintPolicyOnEntry = dbBranch.PrintPolicyOnEntry;
                    branchAdditionalInfo = dbBranch.TicketAdditionalInfo;
                    branchPrintAdditionalInfoOnEntry = dbBranch.PrintAdditionalInfoOnEntry;
                    branchSchedule = dbBranch.TicketSchedule;
                    branchPrintScheduleOnEntry = dbBranch.PrintScheduleOnEntry;
                }
            }

            if (!IsExitReceipt)
            {
                var entryRes = resolution;
                if (entryRes == null || (!entryRes.PrintPolicyOnEntry && !entryRes.PrintAdditionalInfoOnEntry))
                {
                    entryRes = db.BillingResolutions
                        .Where(r => r.IsActive && (r.BranchId == branchId || r.BranchId == null))
                        .OrderByDescending(r => !r.IsElectronicResolution) // Priorizar resolución POS
                        .FirstOrDefault(r => r.PrintPolicyOnEntry || r.PrintAdditionalInfoOnEntry);

                    if (entryRes == null)
                    {
                        entryRes = db.BillingResolutions
                            .Where(r => r.IsActive && (r.BranchId == branchId || r.BranchId == null))
                            .OrderByDescending(r => !r.IsElectronicResolution)
                            .FirstOrDefault(r => !string.IsNullOrEmpty(r.TicketPolicy) || !string.IsNullOrEmpty(r.TicketAdditionalInfo))
                            ?? resolution;
                    }
                }

                // 1. Póliza en Tiquete de Entrada: Prioridad a la Sede, luego Resolución
                string? resolvedPolicy = null;
                if (branchPrintPolicyOnEntry && !string.IsNullOrWhiteSpace(branchPolicy))
                {
                    resolvedPolicy = branchPolicy.Trim();
                }
                else if (entryRes?.PrintPolicyOnEntry == true && !string.IsNullOrWhiteSpace(entryRes.TicketPolicy))
                {
                    resolvedPolicy = entryRes.TicketPolicy.Trim();
                }

                // 2. Información Adicional en Tiquete de Entrada: Prioridad a la Sede, luego Resolución
                string? resolvedAdditionalInfo = null;
                if (branchPrintAdditionalInfoOnEntry && !string.IsNullOrWhiteSpace(branchAdditionalInfo))
                {
                    resolvedAdditionalInfo = branchAdditionalInfo.Trim();
                }
                else if (entryRes?.PrintAdditionalInfoOnEntry == true && !string.IsNullOrWhiteSpace(entryRes.TicketAdditionalInfo))
                {
                    resolvedAdditionalInfo = entryRes.TicketAdditionalInfo.Trim();
                }

                TicketPolicy = resolvedPolicy;
                TicketAdditionalInfo = resolvedAdditionalInfo;

                string? rawEntrySchedule = null;
                if (branchPrintScheduleOnEntry && !string.IsNullOrWhiteSpace(branchSchedule))
                {
                    rawEntrySchedule = branchSchedule.Trim();
                }
                else if (entryRes?.PrintScheduleOnEntry == true && !string.IsNullOrWhiteSpace(entryRes.TicketSchedule))
                {
                    rawEntrySchedule = entryRes.TicketSchedule.Trim();
                }

                BranchSchedule = !string.IsNullOrWhiteSpace(rawEntrySchedule)
                    ? (rawEntrySchedule.StartsWith("Horario", StringComparison.OrdinalIgnoreCase) ? rawEntrySchedule : $"Horario: {rawEntrySchedule}")
                    : null;
            }
            else
            {
                var exitRes = resolution;
                if (exitRes == null && ticket.ResolutionId.HasValue)
                {
                    exitRes = db.BillingResolutions.FirstOrDefault(r => r.ResolutionId == ticket.ResolutionId.Value);
                }

                if (exitRes == null)
                {
                    exitRes = db.BillingResolutions
                        .Where(r => r.IsActive && (r.BranchId == branchId || r.BranchId == null))
                        .OrderByDescending(r => r.IsElectronicResolution == IsFvmInvoice)
                        .FirstOrDefault(r => r.PrintPolicyOnExit || r.PrintAdditionalInfoOnExit)
                        ?? db.BillingResolutions
                            .Where(r => r.IsActive && (r.BranchId == branchId || r.BranchId == null))
                            .OrderByDescending(r => r.IsElectronicResolution == IsFvmInvoice)
                            .FirstOrDefault();
                }

                // Póliza en Tiquete de Salida: Resolución o Sede
                TicketPolicy = (exitRes?.PrintPolicyOnExit == true || !string.IsNullOrWhiteSpace(exitRes?.TicketPolicy))
                    ? exitRes?.TicketPolicy?.Trim()
                    : (!string.IsNullOrWhiteSpace(branchPolicy) ? branchPolicy.Trim() : null);

                TicketAdditionalInfo = (exitRes?.PrintAdditionalInfoOnExit == true || !string.IsNullOrWhiteSpace(exitRes?.TicketAdditionalInfo))
                    ? exitRes?.TicketAdditionalInfo?.Trim()
                    : (!string.IsNullOrWhiteSpace(branchAdditionalInfo) ? branchAdditionalInfo.Trim() : null);

                string? rawExitSchedule = null;
                if (exitRes?.PrintScheduleOnExit == true && !string.IsNullOrWhiteSpace(exitRes.TicketSchedule))
                {
                    rawExitSchedule = exitRes.TicketSchedule.Trim();
                }
                else if (branchPrintScheduleOnEntry && !string.IsNullOrWhiteSpace(branchSchedule))
                {
                    rawExitSchedule = branchSchedule.Trim();
                }

                BranchSchedule = !string.IsNullOrWhiteSpace(rawExitSchedule)
                    ? (rawExitSchedule.StartsWith("Horario", StringComparison.OrdinalIgnoreCase) ? rawExitSchedule : $"Horario: {rawExitSchedule}")
                    : null;
            }
        }
        catch
        {
            TicketPolicy = null;
            TicketAdditionalInfo = null;
            BranchSchedule = null;
        }

        HasBranchSchedule = !string.IsNullOrWhiteSpace(BranchSchedule);

        HasTicketPolicy = !string.IsNullOrWhiteSpace(TicketPolicy);
        HasTicketAdditionalInfo = !string.IsNullOrWhiteSpace(TicketAdditionalInfo);
        HasTicketPolicyOrAdditionalInfo = HasTicketPolicy || HasTicketAdditionalInfo;
    }

    private static string GenerateCufe(string numFac, DateTime fechaFac, decimal valFac, string nitEmisor)
    {
        var cleanNit = nitEmisor.Replace("NIT:", "", StringComparison.OrdinalIgnoreCase).Replace("NIT.", "", StringComparison.OrdinalIgnoreCase).Trim();
        var rawData = $"{numFac}{fechaFac:yyyyMMddHHmmss}{valFac:F2}010.00040.0003{valFac:F2}{cleanNit}22222222";
        using var sha = System.Security.Cryptography.SHA384.Create();
        var hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public void LoadShiftClosure(WorkShift shift, ShiftSummaryModel? summary = null)
    {
        Shift = shift;
        ShiftSummary = summary;
        PrintSuccess = false;
        PrintStatusMessage = null;
        RefreshDetectedPrinter();

        IsShiftCloseReceipt = true;
        IsEntryTicket = false;
        IsExitReceipt = false;
        IsFvmInvoice = false;
        IsStandardExitReceipt = false;

        var currentBranch = _sessionService.CurrentBranch;
        var initialWidth = ResolvePaperWidth(shift.BranchId);
        ApplyPaperMetrics(initialWidth);

        var rawLogo = currentBranch?.LogoBase64;
        if (string.IsNullOrWhiteSpace(rawLogo))
        {
            try
            {
                using var db = _connectionManager.CreateDbContext();
                var branchId = currentBranch?.Id ?? shift.BranchId;
                if (branchId > 0)
                {
                    rawLogo = db.Branches.Where(b => b.Id == branchId && !string.IsNullOrWhiteSpace(b.LogoBase64)).Select(b => b.LogoBase64).FirstOrDefault();
                }
                if (string.IsNullOrWhiteSpace(rawLogo))
                {
                    rawLogo = db.Branches.Where(b => !string.IsNullOrWhiteSpace(b.LogoBase64)).Select(b => b.LogoBase64).FirstOrDefault();
                }
            }
            catch { }
        }
        BranchLogoBase64 = rawLogo;

        var bName = currentBranch?.Name ?? "PARKING FLOW";
        BranchName = bName.ToUpperInvariant();

        var bNit = currentBranch?.CompanyNit ?? _configuration?["BranchSettings:Nit"] ?? "900.000.000-1";
        BranchNit = bNit.StartsWith("NIT", StringComparison.OrdinalIgnoreCase) ? bNit : $"NIT. {bNit}";

        var bAddress = currentBranch?.Address ?? _configuration?["BranchSettings:Address"] ?? "CALLE PRINCIPAL";
        BranchAddress = bAddress.ToUpperInvariant();

        var bPhone = currentBranch?.Phone ?? _configuration?["BranchSettings:Phone"] ?? "000-000-0000";
        BranchPhone = bPhone.StartsWith("TEL", StringComparison.OrdinalIgnoreCase) ? bPhone : $"Tel. {bPhone}";

        var operatorName = !string.IsNullOrWhiteSpace(shift.OperatorName)
            ? shift.OperatorName
            : (_sessionService.CurrentUser?.FullName ?? "OPERADOR");
        CashierName = operatorName.ToUpperInvariant();

        var startTime = shift.StartTime;
        var endTime = shift.EndTime ?? DateTime.Now;

        ShiftStartDateStr = startTime.ToString("dd/MM/yyyy");
        ShiftStartTimeStr = startTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        ShiftEndDateStr = endTime.ToString("dd/MM/yyyy");
        ShiftEndTimeStr = endTime.ToString("hh:mm tt", CultureInfo.InvariantCulture);

        var duration = endTime - startTime;
        ShiftDurationStr = $"{(int)duration.TotalHours}h {duration.Minutes}m";

        var shortId = shift.ShiftId.ToString().Length >= 8 ? shift.ShiftId.ToString()[..8].ToUpperInvariant() : shift.ShiftId.ToString().ToUpperInvariant();
        ShiftIdText = $"TURNO #{shortId}";

        var ci = new CultureInfo("es-CO");

        var cash = PickBestValue(summary?.TotalCashCollected, shift.TotalCashCollected);
        var card = PickBestValue(summary?.TotalCardCollected, shift.TotalCardCollected);
        var transfer = PickBestValue(summary?.TotalTransferCollected, shift.TotalTransferCollected);
        var totalRevenue = cash + card + transfer;

        var discounts = PickBestValue(summary?.TotalDiscounts, shift.TotalDiscounts);
        var withdrawals = PickBestValue(summary?.TotalCashWithdrawals, shift.TotalCashWithdrawals);

        decimal mensualidades = 0m;
        int mensualidadesCount = 0;
        int yardCount = 0;

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var bId = currentBranch?.Id ?? shift.BranchId;
            yardCount = db.ParkingTickets.Count(t => t.Status == TicketStatus.Active && (!bId.HasValue || bId.Value == 0 || t.BranchId == bId.Value));

            var subs = db.MonthlySubscriptions
                .Where(s => s.CreatedAtUtc >= shift.StartTimeUtc && s.CreatedAtUtc <= (shift.EndTimeUtc ?? DateTime.UtcNow) && (!bId.HasValue || bId.Value == 0 || s.BranchId == bId.Value))
                .ToList();
            mensualidades = subs.Sum(s => s.AmountPaid);
            mensualidadesCount = subs.Count;
        }
        catch { }

        decimal parqueos = Math.Max(0m, totalRevenue - mensualidades);
        var grossSubtotal = totalRevenue + discounts;
        var subtotalWithDiscount = totalRevenue;

        var taxBase = Math.Round(totalRevenue / 1.19m, 0);
        var iva19 = totalRevenue - taxBase;

        ShiftParqueosAmountStr = parqueos.ToString("C0", ci);
        ShiftMensualidadesAmountStr = mensualidades.ToString("C0", ci);
        ShiftMensualidadesCount = mensualidadesCount;

        ShiftCashInflowsStr = 0m.ToString("C0", ci);
        ShiftCashOutflowsStr = withdrawals.ToString("C0", ci);

        ShiftGrossSubtotalStr = grossSubtotal.ToString("C0", ci);
        ShiftDiscountsStr = discounts.ToString("C0", ci);
        ShiftDiscountedSubtotalStr = subtotalWithDiscount.ToString("C0", ci);
        ShiftTaxBaseStr = taxBase.ToString("C0", ci);
        ShiftIva19Str = iva19.ToString("C0", ci);
        ShiftTotalRevenueStr = totalRevenue.ToString("C0", ci);

        ShiftCashCollectedStr = cash.ToString("C0", ci);
        ShiftCardCollectedStr = card.ToString("C0", ci);
        ShiftTransferCollectedStr = transfer.ToString("C0", ci);

        HasSubscriptionsModule = _permissionService == null || (
            _permissionService.HasPermission("wpf.subscriptions.view") &&
            _permissionService.HasPermission("wpf.subscriptions.create") &&
            _permissionService.HasPermission("wpf.subscriptions.renew") &&
            _permissionService.HasPermission("wpf.subscriptions.cancel")
        );
        ShiftDiscountTicketsCount = summary?.TotalDiscountTickets ?? 0;

        ShiftPaymentMethods.Clear();
        if (summary?.PaymentMethodsBreakdown != null && summary.PaymentMethodsBreakdown.Any(pm => pm.TotalCollected > 0 || pm.TransactionCount > 0))
        {
            foreach (var pm in summary.PaymentMethodsBreakdown)
            {
                ShiftPaymentMethods.Add(pm);
            }
        }
        else
        {
            ShiftPaymentMethods.Add(new ShiftPaymentMethodItem { Name = "Efectivo", TotalCollected = cash });
            ShiftPaymentMethods.Add(new ShiftPaymentMethodItem { Name = "Tarjetas", TotalCollected = card });
            ShiftPaymentMethods.Add(new ShiftPaymentMethodItem { Name = "Transferencias / QR", TotalCollected = transfer });
        }

        ShiftVehiclesExitedCount = summary != null && summary.TotalTicketsProcessed > 0 ? summary.TotalTicketsProcessed : shift.TotalTicketsProcessed;
        ShiftVehiclesInYardCount = yardCount;

        var baseAmount = PickBestValue(summary?.BaseAmount, shift.BaseAmount);
        var expectedCash = (summary != null && summary.ExpectedCash > 0m) 
            ? summary.ExpectedCash 
            : (shift.ExpectedCash > 0m ? shift.ExpectedCash : (baseAmount + cash - withdrawals));
        var actualCash = PickBestValue(summary?.ActualCashCounted, shift.ActualCashCounted);
        var diff = actualCash - expectedCash;

        ShiftBaseAmountStr = baseAmount.ToString("C0", ci);
        ShiftExpectedCashStr = expectedCash.ToString("C0", ci);
        ShiftActualCashStr = actualCash.ToString("C0", ci);
        ShiftDifferenceStr = diff.ToString("C0", ci);

        if (Math.Abs(diff) < 0.01m)
        {
            ShiftArqueoStatusText = "CUADRADA";
        }
        else if (diff > 0.01m)
        {
            ShiftArqueoStatusText = "SOBRANTE";
        }
        else
        {
            ShiftArqueoStatusText = "FALTANTE";
        }

        ShiftNotes = shift.Notes ?? string.Empty;
        HasShiftNotes = !string.IsNullOrWhiteSpace(ShiftNotes);
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
            if (DirectPrintHandler != null)
            {
                var (success, printerName, error) = await DirectPrintHandler.Invoke();
                PrintSuccess = success;
                if (success)
                {
                    PrintStatusMessage = $"Enviado a: {printerName}";
                }
                else
                {
                    PrintStatusMessage = error ?? "Fallo al enviar a la impresora";
                }
            }
            else
            {
                if (IsShiftCloseReceipt && Shift != null)
                {
                    PrintSuccess = await _printerService.PrintShiftCloseReceiptAsync(Shift, ShiftSummary);
                }
                else if (Ticket.Status == Core.Enums.TicketStatus.Completed || Ticket.ExitTimeUtc.HasValue || Ticket.ExitTime.HasValue)
                {
                    PrintSuccess = await _printerService.PrintExitReceiptAsync(Ticket);
                }
                else
                {
                    PrintSuccess = await _printerService.PrintEntryTicketAsync(Ticket);
                }
            }
        }
        finally
        {
            IsPrinting = false;
        }
    }

    public static string ResolveIdTypeLabel(int idType, string? personType = null)
    {
        return idType switch
        {
            13 or 1 => "CC:",
            31 or 3 => "NIT:",
            22 or 2 => "CE:",
            12 => "TI:",
            41 or 4 => "PAS:",
            42 or 5 => "DIE:",
            _ => string.Equals(personType, "Company", StringComparison.OrdinalIgnoreCase) ? "NIT:" : "CC:"
        };
    }

    private static decimal PickBestValue(decimal? summaryVal, decimal shiftVal)
    {
        if (!summaryVal.HasValue) return shiftVal;
        if (summaryVal.Value > 0m) return summaryVal.Value;
        return shiftVal > 0m ? shiftVal : summaryVal.Value;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }
}
