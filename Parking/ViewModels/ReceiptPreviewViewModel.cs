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
    private string _invoiceNumberText = string.Empty;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _electronicInvoiceQrImage;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _consultationQrCodeImage;

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

    public void LoadTicket(ParkingTicket ticket)
    {
        Ticket = ticket;
        PrintSuccess = false;

        var currentBranch = _sessionService.CurrentBranch;
        BranchName = !string.IsNullOrWhiteSpace(currentBranch?.Name) ? currentBranch.Name.ToUpperInvariant() : "PARKING FLOW";
        BranchAddress = !string.IsNullOrWhiteSpace(currentBranch?.Address) ? currentBranch.Address.ToUpperInvariant() : "CALLE 26 #57-83";
        BranchNit = !string.IsNullOrWhiteSpace(currentBranch?.Notes) && currentBranch.Notes.StartsWith("NIT", StringComparison.OrdinalIgnoreCase)
            ? currentBranch.Notes
            : "NIT: 900.914.246-2";

        FormattedRateText = $"TARIFA: {ticket.HourlyRate:C0} / HORA";

        IsExitReceipt = ticket.Status == TicketStatus.Completed || ticket.ExitTimeUtc.HasValue;
        IsEntryTicket = !IsExitReceipt;

        if (IsExitReceipt)
        {
            // 1. Detectar si la resolución es FVM (Factura Electrónica)
            bool isFvm = false;

            if (!string.IsNullOrWhiteSpace(ticket.ResolutionName) &&
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
                    var resolution = db.BillingResolutions.FirstOrDefault(r => r.ResolutionId == ticket.ResolutionId.Value);
                    if (resolution != null)
                    {
                        isFvm = (resolution.Prefix?.Equals("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (resolution.DocumentType?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (resolution.DocumentType?.Contains("Factura Electr", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                (resolution.Name?.Contains("FVM", StringComparison.OrdinalIgnoreCase) ?? false);
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
            var totalPaid = ticket.NetAmount > 0 ? ticket.NetAmount : (ticket.AmountPaid > 0 ? ticket.AmountPaid : ticket.GrossAmount);
            FormattedTotalPaid = $"{totalPaid:C0}";
            IvaPercentageText = "19%";

            // 5. Factura Electrónica y QR
            if (IsFvmInvoice)
            {
                CustomerName = "CONSUMIDOR FINAL";
                CustomerDocument = "CC 222222222";
                InvoiceNumberText = !string.IsNullOrWhiteSpace(ticket.InvoiceNumber) ? ticket.InvoiceNumber : "FVM-0001";

                var cufeMock = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                var nitClean = BranchNit.Replace("NIT:", "", StringComparison.OrdinalIgnoreCase).Trim();
                var exitDateFormatted = ticket.ExitTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var qrContent = $"NumFac:{InvoiceNumberText}\nFecFac:{exitDateFormatted}\nNitFac:{nitClean}\nDocAdq:222222222\nValFac:{totalPaid:F2}\nValIva:{(totalPaid - (totalPaid / 1.19m)):F2}\nCUFE:{cufeMock}";
                ElectronicInvoiceQrImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(qrContent, 8);
            }
            else
            {
                ElectronicInvoiceQrImage = null;
            }

            BarcodeImage = null;
            ConsultationQrCodeImage = null;
        }
        else
        {
            // Tiquete de Entrada
            IsFvmInvoice = false;
            IsStandardExitReceipt = false;
            BarcodeImage = Services.Implementations.BarcodeGeneratorService.GenerateCode128(ticket.PlateNumber);
            ConsultationQrCodeImage = Services.Implementations.QrCodeGeneratorService.GenerateQrCode(PublicConsultationUrl, 8);
        }
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
            if (Ticket.Status == Core.Enums.TicketStatus.Completed || Ticket.ExitTimeUtc.HasValue)
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
