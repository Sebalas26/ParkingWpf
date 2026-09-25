using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;
using Parking.Services.Implementations;

namespace Parking.ViewModels;

public class IdentificationTypeOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[RequirePermission("checkout.view", "Salida y Cobro / Caja")]
public partial class CheckOutViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IMonthlySubscriptionService _monthlySubscriptionService;
    private readonly IStoreService _storeService;
    private readonly IAgreementService _agreementService;
    private readonly IBillingResolutionService _billingResolutionService;
    private readonly ISessionService _sessionService;
    private readonly IDialogService _dialogService;
    private readonly IDbConnectionManager _connectionManager;
    private readonly ISyncEngineService? _syncEngine;
    private readonly IApiClientService? _apiClient;
    private readonly DispatcherTimer _liveCalculationTimer;
    private DateTime _ticketSelectionTimeUtc;
    private DateTime _frozenExitTimeUtc;
    private int _currentGracePeriodSeconds = 0;
    private int _graceRenewalCount = 0;
    private bool _isPaymentTimeoutDialogShowing;

    public event Action? RequestCloseDialog;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchTicketCommand))]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isVirtualKeyboardVisible;

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private bool _isMoreDataPopupOpen;

    public string FormattedCustomerPhone =>
        !string.IsNullOrWhiteSpace(SelectedTicket?.CustomerPhone) ? SelectedTicket.CustomerPhone : "N/A";

    public string FormattedNotes =>
        !string.IsNullOrWhiteSpace(SelectedTicket?.Notes) ? SelectedTicket.Notes : "N/A";

    [RelayCommand]
    private void ToggleMoreDataPopup()
    {
        IsMoreDataPopupOpen = !IsMoreDataPopupOpen;
    }

    [RelayCommand]
    private void CloseMoreDataPopup()
    {
        IsMoreDataPopupOpen = false;
    }

    [ObservableProperty]
    private bool _isMonthlyTicket;

    [ObservableProperty]
    private decimal _minuteRate;

    [ObservableProperty]
    private decimal _grossFee;

    [ObservableProperty]
    private decimal _hourRate;

    [ObservableProperty]
    private bool _isLostTicket;

    [ObservableProperty]
    private decimal _lostTicketFee;

    [ObservableProperty]
    private bool _hasLostTicketFee;

    [ObservableProperty]
    private bool _allowMinute = true;

    [ObservableProperty]
    private bool _allowHour = true;

    [ObservableProperty]
    private bool _allowDay = true;

    [ObservableProperty]
    private bool _allowNight = false;

    [ObservableProperty]
    private string _rateSummaryText = string.Empty;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _calculatedFee;

    [ObservableProperty]
    private string _elapsedTimeString = "0min 0seg";

    [ObservableProperty]
    private decimal _amountTendered;

    [ObservableProperty]
    private decimal _changeDue;

    [ObservableProperty]
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private PaymentMethodEntity? _selectedPaymentMethodEntity;

    [ObservableProperty]
    private bool _hasPaymentMethods = true;

    [ObservableProperty]
    private string? _exitNotes;

    partial void OnExitNotesChanged(string? value)
    {
        if (value != null && value.Length > 50)
        {
            ExitNotes = value.Substring(0, 50);
        }
    }

    [ObservableProperty]
    private bool _hasAgreementDiscount;

    [ObservableProperty]
    private Store? _selectedStore;

    [ObservableProperty]
    private CommercialAgreement? _selectedAgreement;

    [ObservableProperty]
    private decimal _customerPurchaseAmount;

    [ObservableProperty]
    private string _invoiceNumber = string.Empty;

    [ObservableProperty]
    private string? _discountRuleDescription;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    [ObservableProperty]
    private bool _isAgreementPopupOpen;

    [ObservableProperty]
    private CommercialAgreement? _popupAgreement;

    [ObservableProperty]
    private string _popupDiscountSummary = string.Empty;

    [ObservableProperty]
    private bool _isAgreementTooltipOpen;

    [ObservableProperty]
    private bool _isAgreementEligible;

    [ObservableProperty]
    private bool _agreementRequiresPurchase;

    [ObservableProperty]
    private bool _agreementMinPurchaseMet = true;

    [ObservableProperty]
    private bool _agreementMaxTimeMet = true;

    [ObservableProperty]
    private int _agreementTotalStayMinutes;

    [ObservableProperty]
    private int _agreementMaxAllowedMinutes;

    [ObservableProperty]
    private string _agreementBenefitText = string.Empty;

    [ObservableProperty]
    private string _agreementEligibilityBannerText = string.Empty;

    [ObservableProperty]
    private string _agreementEligibilityBannerStatus = string.Empty;

    [ObservableProperty]
    private string _customerPurchaseAmountText = string.Empty;

    [ObservableProperty]
    private BillingResolution? _selectedResolution;

    [ObservableProperty]
    private bool _hasResolutions;

    [ObservableProperty]
    private bool _showPaymentMethodWarning;

    [ObservableProperty]
    private bool _showResolutionWarning;

    [ObservableProperty]
    private bool _isResolutionLocked;

    private bool _isApplyingResolutionFilter;

    [ObservableProperty]
    private string _resolutionLockReason = string.Empty;

    public bool CanChangeResolution => !IsResolutionLocked || FilteredResolutions.Count > 1;

    public static bool IsElectronicResolutionDefensive(BillingResolution? r)
    {
        if (r == null) return false;
        if (r.IsElectronicResolution) return true;

        var doc = r.DocumentType ?? string.Empty;
        var pfx = r.Prefix ?? string.Empty;
        var name = r.Name ?? string.Empty;

        return doc.IndexOf("electr", StringComparison.OrdinalIgnoreCase) >= 0 ||
               pfx.Equals("FE", StringComparison.OrdinalIgnoreCase) ||
               pfx.Equals("SETP", StringComparison.OrdinalIgnoreCase) ||
               pfx.StartsWith("FE", StringComparison.OrdinalIgnoreCase) ||
               name.IndexOf("electr", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public bool IsElectronicInvoicingSectionVisible =>
        HasElectronicInvoicingEnabled ||
        EmitElectronicInvoice ||
        (SelectedPaymentMethodEntity?.RequiresResolution == true) ||
        (SelectedResolution != null && IsElectronicResolutionDefensive(SelectedResolution));

    private readonly DispatcherTimer _agreementPopupTimer;
    private readonly DispatcherTimer _agreementTooltipTimer;

    public ObservableCollection<ParkingTicket> ActiveVehicles { get; } = new();
    public ObservableCollection<Store> AvailableStores { get; } = new();
    public ObservableCollection<CommercialAgreement> AvailableAgreements { get; } = new();
    public ObservableCollection<CommercialAgreement> BranchAgreements { get; } = new();
    public ObservableCollection<PaymentMethodEntity> AvailablePaymentMethods { get; } = new();
    public ObservableCollection<BillingResolution> AvailableResolutions { get; } = new();
    public ObservableCollection<BillingResolution> FilteredResolutions { get; } = new();
    public ObservableCollection<Customer> AvailableCustomers { get; } = new();
    public ObservableCollection<Customer> FilteredAvailableCustomers { get; } = new();
    public ObservableCollection<DaneMunicipality> AvailableMunicipalities { get; } = new();
    public List<IdentificationTypeOption> IdentificationTypeOptions { get; } = new()
    {
        new() { Id = 13, Name = "Cédula de Ciudadanía (CC)" },
        new() { Id = 31, Name = "NIT (Empresas / Jurídicas)" },
        new() { Id = 22, Name = "Cédula de Extranjería (CE)" },
        new() { Id = 41, Name = "Pasaporte (PP)" }
    };

    [ObservableProperty]
    private bool _hasElectronicInvoicingEnabled;

    [ObservableProperty]
    private bool _forceElectronicInvoiceOnCheckout;

    [ObservableProperty]
    private bool _emitElectronicInvoice;

    [ObservableProperty]
    private bool _canToggleElectronicInvoice = true;

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    [ObservableProperty]
    private bool _isCustomerDropDownOpen;

    private bool _isUpdatingCustomerSelection;

    [ObservableProperty]
    private bool _showCustomerWarning;

    [ObservableProperty]
    private bool _isQuickRegisterCustomerOpen;

    [ObservableProperty]
    private bool _isNitDocumentType;

    [ObservableProperty]
    private IdentificationTypeOption? _selectedIdentificationTypeOption;

    [ObservableProperty]
    private string _newCustomerDocumentNumber = string.Empty;

    [ObservableProperty]
    private string? _newCustomerCheckDigit;

    [ObservableProperty]
    private string _newCustomerFullName = string.Empty;

    [ObservableProperty]
    private string _newCustomerEmail = string.Empty;

    [ObservableProperty]
    private string? _newCustomerPhone;

    [ObservableProperty]
    private string _newCustomerPersonType = "Person";

    [ObservableProperty]
    private string _newCustomerAddress = string.Empty;

    [ObservableProperty]
    private string? _newCustomerAddressError;

    [ObservableProperty]
    private string _newCustomerCityCode = string.Empty;

    [ObservableProperty]
    private DaneMunicipality? _selectedDaneMunicipality;

    [ObservableProperty]
    private string? _newCustomerCityError;

    [ObservableProperty]
    private string? _quickCustomerFeedback;

    [ObservableProperty]
    private string? _newCustomerDocumentError;

    [ObservableProperty]
    private string? _newCustomerCheckDigitError;

    [ObservableProperty]
    private string? _newCustomerFullNameError;

    [ObservableProperty]
    private string? _newCustomerEmailError;

    [ObservableProperty]
    private string? _newCustomerPhoneError;

    public CheckOutViewModel(
        IParkingTicketService ticketService,
        IPricingCalculatorService pricingCalculator,
        IMonthlySubscriptionService monthlySubscriptionService,
        IStoreService storeService,
        IAgreementService agreementService,
        IBillingResolutionService billingResolutionService,
        ISessionService sessionService,
        IDialogService dialogService,
        IDbConnectionManager connectionManager,
        ISyncEngineService syncEngine,
        IApiClientService? apiClient = null)
    {
        _ticketService = ticketService;
        _pricingCalculator = pricingCalculator;
        _monthlySubscriptionService = monthlySubscriptionService;
        _storeService = storeService;
        _agreementService = agreementService;
        _billingResolutionService = billingResolutionService;
        _sessionService = sessionService;
        _dialogService = dialogService;
        _connectionManager = connectionManager;
        _syncEngine = syncEngine;
        _apiClient = apiClient;
        SelectedIdentificationTypeOption = IdentificationTypeOptions.FirstOrDefault();

        syncEngine.DataSynchronized += () =>
        {
            if (SelectedTicket != null)
            {
                // Un vehículo está siendo liquidado activamente; no reiniciar el estado del checkout,
                // medio de pago, resolución o formulario de registro de cliente adquirente.
                return;
            }

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(async () =>
                {
                    try { await InitializeAsync(); } catch { }
                });
            }
            else
            {
                _ = InitializeAsync();
            }
        };

        _sessionService.ActiveBranchChanged += async _ =>
        {
            if (SelectedTicket != null) return;
            try { await InitializeAsync(); } catch { }
        };

        _liveCalculationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveCalculationTimer.Tick += (s, e) => RecalculateLiveFee();

        _agreementPopupTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _agreementPopupTimer.Tick += (s, e) =>
        {
            _agreementPopupTimer.Stop();
            IsAgreementPopupOpen = false;
        };

        _agreementTooltipTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _agreementTooltipTimer.Tick += (s, e) =>
        {
            _agreementTooltipTimer.Stop();
            IsAgreementTooltipOpen = false;
        };

        _ticketService.TicketRegistered += (s, t) => _ = LoadActiveVehiclesAsync();
        _ticketService.TicketCompleted += (s, t) =>
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.InvokeAsync(async () =>
                {
                    HandleRemoteCheckOutEvent(t);
                    await LoadActiveVehiclesAsync();
                });
            }
            else
            {
                HandleRemoteCheckOutEvent(t);
                _ = LoadActiveVehiclesAsync();
            }
        };
    }

    private void HandleRemoteCheckOutEvent(ParkingTicket? ticket)
    {
        if (ticket == null || SelectedTicket == null) return;

        bool matchesId = ticket.TicketId != Guid.Empty && SelectedTicket.TicketId == ticket.TicketId;
        bool matchesPlate = !string.IsNullOrWhiteSpace(ticket.PlateNumber) &&
                            string.Equals(SelectedTicket.PlateNumber?.Trim(), ticket.PlateNumber.Trim(), StringComparison.OrdinalIgnoreCase);

        if (matchesId || matchesPlate)
        {
            var plate = SelectedTicket.PlateNumber;
            SelectedTicket = null;
            IsAgreementPopupOpen = false;
            SearchQuery = string.Empty;
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"El vehículo con placa '{plate}' ya fue liquidado centralmente (desde PWA).";
        }
    }

    public override async Task InitializeAsync()
    {
        HasElectronicInvoicingEnabled = _sessionService.CurrentUser?.HasElectronicInvoicingEnabled ?? false;
        ForceElectronicInvoiceOnCheckout = _sessionService.CurrentUser?.ForceElectronicInvoiceOnCheckout ?? false;
        CanToggleElectronicInvoice = !ForceElectronicInvoiceOnCheckout;
        EmitElectronicInvoice = ForceElectronicInvoiceOnCheckout;

        await LoadPaymentMethodsAsync();
        await LoadActiveVehiclesAsync();
        await LoadStoresAsync();
        await LoadResolutionsAsync();
        await LoadMunicipalitiesAsync();
        if (HasElectronicInvoicingEnabled || (SelectedPaymentMethodEntity?.RequiresResolution == true) || (SelectedResolution?.IsElectronicResolution == true))
        {
            await LoadCustomersAsync();
        }
        await _pricingCalculator.ReloadRatesAsync();
        if (SelectedTicket != null)
        {
            var rateInfo = _pricingCalculator.GetRate(SelectedTicket.VehicleType);
            HourRate = rateInfo?.HourRate ?? 0m;
            MinuteRate = rateInfo != null && rateInfo.MinuteRate > 0
                ? rateInfo.MinuteRate
                : (rateInfo != null && rateInfo.HourRate > 0 ? Math.Round(rateInfo.HourRate / 60m, 2) : 0m);
            RecalculateLiveFee();
        }
    }

    private int _isLoadingCustomers;

    private async Task LoadCustomersAsync()
    {
        if (Interlocked.CompareExchange(ref _isLoadingCustomers, 1, 0) != 0)
        {
            return;
        }

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var customers = await db.Customers
                .Include(c => c.Vehicles)
                .Where(c => c.IsActive)
                .OrderBy(c => c.FullName)
                .ToListAsync();

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailableCustomers.Clear();
                    foreach (var c in customers)
                    {
                        AvailableCustomers.Add(c);
                    }
                    ApplyCustomerFilter();
                });
            }
            else
            {
                AvailableCustomers.Clear();
                foreach (var c in customers)
                {
                    AvailableCustomers.Add(c);
                }
                ApplyCustomerFilter();
            }
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _isLoadingCustomers, 0);
        }
    }

    private async Task LoadMunicipalitiesAsync()
    {
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var munis = await db.DaneMunicipalities
                .OrderBy(m => m.MunicipalityName)
                .ToListAsync();

            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AvailableMunicipalities.Clear();
                    foreach (var m in munis)
                    {
                        AvailableMunicipalities.Add(m);
                    }
                });
            }
            else
            {
                AvailableMunicipalities.Clear();
                foreach (var m in munis)
                {
                    AvailableMunicipalities.Add(m);
                }
            }
        }
        catch { }
    }

    private async Task LoadResolutionsAsync()
    {
        try
        {
            var currentBranchId = _sessionService.CurrentBranch?.Id;
            var resolutions = await _billingResolutionService.GetActiveResolutionsByBranchAsync(currentBranchId);
            AvailableResolutions.Clear();
            foreach (var r in resolutions)
            {
                AvailableResolutions.Add(r);
            }
            HasResolutions = AvailableResolutions.Count > 0;
            ApplyPaymentMethodResolutionFilter(SelectedPaymentMethodEntity);
        }
        catch { }
    }

    private async Task LoadPaymentMethodsAsync()
    {
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var currentBranchId = _sessionService.CurrentBranch?.Id;
            List<PaymentMethodEntity> methods;

            if (currentBranchId.HasValue)
            {
                var branchMethods = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    db.BranchPaymentMethods
                        .Where(bpm => bpm.BranchId == currentBranchId.Value && bpm.IsActive)
                );
                var branchPmIds = branchMethods.Select(bpm => bpm.PaymentMethodId).ToHashSet();

                methods = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    db.PaymentMethods.Where(p => p.State && branchPmIds.Contains(p.Id))
                );

                // Aplicar personalización de RequiresCashTender por sede
                foreach (var m in methods)
                {
                    var bpm = branchMethods.FirstOrDefault(b => b.PaymentMethodId == m.Id);
                    if (bpm != null)
                    {
                        m.RequiresCashTender = bpm.RequiresCashTender;
                    }
                }
            }
            else
            {
                methods = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                    db.PaymentMethods.Where(p => p.State)
                );
            }

            AvailablePaymentMethods.Clear();
            foreach (var m in methods)
            {
                AvailablePaymentMethods.Add(m);
            }

            HasPaymentMethods = AvailablePaymentMethods.Count > 0;
            SelectedPaymentMethodEntity = AvailablePaymentMethods.FirstOrDefault();
        }
        catch { }
    }

    partial void OnHasElectronicInvoicingEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsElectronicInvoicingSectionVisible));
    }

    partial void OnSelectedResolutionChanged(BillingResolution? value)
    {
        if (_isApplyingResolutionFilter) return;

        if (value != null)
        {
            ShowResolutionWarning = false;

            // Si el medio de pago actual exige FE y se intenta seleccionar una resolución no electrónica
            if (SelectedPaymentMethodEntity?.RequiresResolution == true && !IsElectronicResolutionDefensive(value))
            {
                _isApplyingResolutionFilter = true;
                try
                {
                    var validFe = FilteredResolutions.FirstOrDefault(r => IsElectronicResolutionDefensive(r));
                    SelectedResolution = validFe;
                    ShowResolutionWarning = validFe == null;
                }
                finally
                {
                    _isApplyingResolutionFilter = false;
                }
                return;
            }

            bool isElectronic = IsElectronicResolutionDefensive(value);

            if (isElectronic)
            {
                EmitElectronicInvoice = true;
                _ = LoadCustomersAsync();
            }
            else if (!ForceElectronicInvoiceOnCheckout && (SelectedPaymentMethodEntity == null || !SelectedPaymentMethodEntity.RequiresResolution))
            {
                EmitElectronicInvoice = false;
            }
        }
        OnPropertyChanged(nameof(IsElectronicInvoicingSectionVisible));
    }

    partial void OnSelectedPaymentMethodEntityChanged(PaymentMethodEntity? value)
    {
        if (value != null)
        {
            ShowPaymentMethodWarning = false;
            SelectedPaymentMethod = value.ToEnum();
            if (!value.RequiresCashTender)
            {
                AmountTendered = CalculatedFee;
                ChangeDue = 0m;
            }
            else
            {
                CalculateChange();
            }

            ApplyPaymentMethodResolutionFilter(value);
        }
        else
        {
            ApplyPaymentMethodResolutionFilter(null);
        }
    }

    private void ApplyPaymentMethodResolutionFilter(PaymentMethodEntity? method)
    {
        if (_isApplyingResolutionFilter) return;
        _isApplyingResolutionFilter = true;

        try
        {
            if (AvailableResolutions.Count == 0)
            {
                FilteredResolutions.Clear();
                SelectedResolution = null;
                IsResolutionLocked = false;
                ResolutionLockReason = string.Empty;
                return;
            }

            if (method != null && (method.RequiresResolution || !string.IsNullOrWhiteSpace(method.DefaultResolutionId)))
            {
                var feResolutions = AvailableResolutions.Where(r => IsElectronicResolutionDefensive(r)).ToList();

                if (method.RequiresResolution)
                {
                    // Si el medio de pago exige Facturación Electrónica, SOLO se permiten resoluciones electrónicas
                    FilteredResolutions.Clear();
                    foreach (var r in feResolutions)
                    {
                        FilteredResolutions.Add(r);
                    }

                    IsResolutionLocked = true;
                    ResolutionLockReason = $"Bloqueada por {method.Name}: exige Facturación Electrónica";
                    EmitElectronicInvoice = true;
                    CanToggleElectronicInvoice = false;
                    _ = LoadCustomersAsync();

                    if (feResolutions.Count == 0)
                    {
                        SelectedResolution = null;
                        ShowResolutionWarning = true;
                    }
                    else
                    {
                        BillingResolution? targetResolution = null;
                        if (!string.IsNullOrWhiteSpace(method.DefaultResolutionId))
                        {
                            targetResolution = feResolutions.FirstOrDefault(r => r.ResolutionId.ToString().Equals(method.DefaultResolutionId, StringComparison.OrdinalIgnoreCase));
                        }

                        // Fallback dinámico a la primera resolución electrónica disponible
                        targetResolution ??= feResolutions.FirstOrDefault();

                        SelectedResolution = targetResolution;
                        ShowResolutionWarning = targetResolution == null;
                    }
                }
                else
                {
                    // No exige FE de forma obligatoria, pero puede tener resolución por defecto configurada
                    FilteredResolutions.Clear();
                    foreach (var r in AvailableResolutions)
                    {
                        FilteredResolutions.Add(r);
                    }

                    BillingResolution? targetResolution = null;
                    if (!string.IsNullOrWhiteSpace(method.DefaultResolutionId))
                    {
                        targetResolution = AvailableResolutions.FirstOrDefault(r => r.ResolutionId.ToString().Equals(method.DefaultResolutionId, StringComparison.OrdinalIgnoreCase));
                    }

                    SelectedResolution = targetResolution ?? AvailableResolutions.FirstOrDefault();
                }
            }
            else
            {
                if (IsResolutionLocked)
                {
                    EmitElectronicInvoice = ForceElectronicInvoiceOnCheckout;
                }
                IsResolutionLocked = false;
                ResolutionLockReason = string.Empty;
                CanToggleElectronicInvoice = !ForceElectronicInvoiceOnCheckout;
                ShowResolutionWarning = false;
                ShowCustomerWarning = false;

                FilteredResolutions.Clear();
                foreach (var r in AvailableResolutions)
                {
                    FilteredResolutions.Add(r);
                }

                if (method != null)
                {
                    if (EmitElectronicInvoice)
                    {
                        AutoSelectElectronicResolution();
                    }
                    else
                    {
                        AutoSelectStandardResolution();
                    }
                }
            }
        }
        finally
        {
            _isApplyingResolutionFilter = false;
            OnPropertyChanged(nameof(CanChangeResolution));
            OnPropertyChanged(nameof(IsElectronicInvoicingSectionVisible));
        }
    }

    private void AutoSelectElectronicResolution()
    {
        var targetList = FilteredResolutions.Count > 0 ? FilteredResolutions : AvailableResolutions;
        if (targetList.Count == 0) return;

        if (SelectedResolution != null && IsElectronicResolutionDefensive(SelectedResolution) && targetList.Contains(SelectedResolution))
        {
            return;
        }

        var feRes = targetList.FirstOrDefault(r => IsElectronicResolutionDefensive(r)) ?? targetList.FirstOrDefault();
        if (feRes != null)
        {
            SelectedResolution = feRes;
        }
    }

    private void AutoSelectStandardResolution()
    {
        var targetList = FilteredResolutions.Count > 0 ? FilteredResolutions : AvailableResolutions;
        if (targetList.Count == 0) return;

        if (SelectedResolution != null && !IsElectronicResolutionDefensive(SelectedResolution) && targetList.Contains(SelectedResolution))
        {
            return;
        }

        var stdRes = targetList.FirstOrDefault(r => !IsElectronicResolutionDefensive(r)) ?? targetList.FirstOrDefault();
        if (stdRes != null)
        {
            SelectedResolution = stdRes;
        }
    }

    [RelayCommand]
    private void SelectResolution(BillingResolution resolution)
    {
        if (resolution == null) return;
        if (IsResolutionLocked && !IsElectronicResolutionDefensive(resolution))
        {
            return;
        }
        SelectedResolution = resolution;
    }

    [RelayCommand]
    private void SelectPaymentMethod(PaymentMethodEntity method)
    {
        if (method == null) return;
        SelectedPaymentMethodEntity = method;
    }

    private async Task LoadActiveVehiclesAsync()
    {
        var active = await _ticketService.GetActiveTicketsAsync();
        ActiveVehicles.Clear();
        foreach (var ticket in active)
        {
            ActiveVehicles.Add(ticket);
        }
    }

    private async Task LoadStoresAsync()
    {
        var stores = await _storeService.GetActiveStoresAsync();
        AvailableStores.Clear();
        BranchAgreements.Clear();
        foreach (var s in stores)
        {
            AvailableStores.Add(s);
            var agreements = await _agreementService.GetAgreementsByStoreAsync(s.StoreId);
            foreach (var a in agreements)
            {
                BranchAgreements.Add(a);
            }
        }

        try
        {
            var allAgreements = await _agreementService.GetAllAgreementsAsync();
            foreach (var a in allAgreements.Where(a => a.IsActive))
            {
                if (!BranchAgreements.Any(ba => ba.AgreementId == a.AgreementId))
                {
                    BranchAgreements.Add(a);
                }
            }
        }
        catch { }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var sanitized = SanitizePlateQuery(value);
        if (sanitized != value)
        {
            SearchQuery = sanitized;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        HasFeedback = false;
        FeedbackMessage = null;
        SelectedTicket = null;
    }

    [RelayCommand]
    private void ToggleVirtualKeyboard()
    {
        IsVirtualKeyboardVisible = !IsVirtualKeyboardVisible;
    }

    [RelayCommand]
    private void AppendVirtualKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (key.Equals("BACKSPACE", StringComparison.OrdinalIgnoreCase) || key.Equals("DEL", StringComparison.OrdinalIgnoreCase))
        {
            if (SearchQuery.Length > 0)
            {
                SearchQuery = SearchQuery[..^1];
            }
        }
        else if (key.Equals("CLEAR", StringComparison.OrdinalIgnoreCase))
        {
            SearchQuery = string.Empty;
        }
        else if (key.Equals("SPACE", StringComparison.OrdinalIgnoreCase))
        {
            SearchQuery += " ";
        }
        else
        {
            SearchQuery = (SearchQuery + key).ToUpperInvariant();
        }
    }

    private string SanitizePlateQuery(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var text = raw.Trim();

        // Normalizar caracteres producidos por pistolas lectoras con layout en español
        text = text.Replace("Ñ--", "://")
                   .Replace("¿", "=")
                   .Replace("'", "?")
                   .Replace("¡", "");

        // Si tiene parámetro plate=
        if (text.Contains("plate=", StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf("plate=", StringComparison.OrdinalIgnoreCase);
            var queryPart = text[(idx + 6)..];
            var endIdx = queryPart.IndexOfAny(new[] { '&', ' ', '#', '/', '?' });
            if (endIdx > 0) queryPart = queryPart[..endIdx];
            return queryPart.Trim().ToUpperInvariant();
        }

        // Si tiene parámetro ticket=
        if (text.Contains("ticket=", StringComparison.OrdinalIgnoreCase))
        {
            var idx = text.IndexOf("ticket=", StringComparison.OrdinalIgnoreCase);
            var queryPart = text[(idx + 7)..];
            var endIdx = queryPart.IndexOfAny(new[] { '&', ' ', '#', '/', '?' });
            if (endIdx > 0) queryPart = queryPart[..endIdx];
            return queryPart.Trim().ToUpperInvariant();
        }

        // Si es una URL completa, extraer el último segmento
        if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase) || text.Contains("://"))
        {
            var parts = text.Split(new[] { '/', '?', '=', '&' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var last = parts[^1];
                if (last.Length is >= 4 and <= 25)
                {
                    return last.Trim().ToUpperInvariant();
                }
            }
        }

        // Detectar y remover repeticiones consecutivas de placas producidas por disparos múltiples de escáner
        // Ejemplo: XDI21HXDI21H o XDI21HXDI21HXDI21H -> XDI21H
        for (int len = 3; len <= 10; len++)
        {
            if (text.Length >= len * 2)
            {
                var prefix = text[..len];
                var isAllRepeats = true;
                for (int i = 0; i + len <= text.Length; i += len)
                {
                    if (text.Substring(i, len) != prefix)
                    {
                        isAllRepeats = false;
                        break;
                    }
                }
                var remainder = text.Length % len;
                if (isAllRepeats && remainder == 0)
                {
                    return prefix.ToUpperInvariant();
                }
                if (isAllRepeats && remainder > 0 && text[^remainder..] == prefix[..remainder])
                {
                    return prefix.ToUpperInvariant();
                }
            }
        }

        return text.Trim().ToUpperInvariant();
    }

    async partial void OnSelectedStoreChanged(Store? value)
    {
        AvailableAgreements.Clear();
        SelectedAgreement = null;
        DiscountRuleDescription = null;

        if (value != null)
        {
            var agreements = await _agreementService.GetAgreementsByStoreAsync(value.StoreId);
            foreach (var a in agreements)
            {
                AvailableAgreements.Add(a);
            }

            if (AvailableAgreements.Count > 0)
            {
                SelectedAgreement = AvailableAgreements[0];
            }
        }

        RecalculateLiveFee();
    }

    partial void OnSelectedAgreementChanged(CommercialAgreement? value)
    {
        if (value != null)
        {
            var rule = value.DiscountPercentage.HasValue
                ? $"{value.DiscountPercentage.Value:F0}% dcto. en compras > ${value.MinPurchaseAmount:N0}"
                : $"${value.DiscountFixedAmount:N0} dcto. fijo en compras > ${value.MinPurchaseAmount:N0}";

            DiscountRuleDescription = rule;
        }
        else
        {
            DiscountRuleDescription = null;
        }

        RecalculateLiveFee();
    }

    [RelayCommand]
    private async Task ToggleSelectAgreementAsync(CommercialAgreement? agreement)
    {
        if (agreement == null) return;

        if (SelectedAgreement?.AgreementId == agreement.AgreementId && HasAgreementDiscount)
        {
            SelectedAgreement = null;
            HasAgreementDiscount = false;
            CustomerPurchaseAmount = 0m;
            CustomerPurchaseAmountText = string.Empty;
            DiscountAmount = 0m;
            CloseAgreementTooltip();
            RecalculateLiveFee();
            return;
        }

        // Validar si la estadía del vehículo supera el tiempo máximo permitido para este convenio
        if (SelectedTicket != null)
        {
            var now = _currentGracePeriodSeconds > 0 ? _frozenExitTimeUtc : DateTime.UtcNow;
            var totalStay = Math.Max(0, (int)(now - SelectedTicket.EntryTimeUtc).TotalMinutes);
            var maxAllowed = (agreement.MaxHoursApplicable.GetValueOrDefault(0) * 60) + agreement.MaxMinutesApplicable.GetValueOrDefault(0);

            if (maxAllowed > 0 && totalStay > maxAllowed)
            {
                SelectedAgreement = null;
                HasAgreementDiscount = false;
                CustomerPurchaseAmount = 0m;
                CustomerPurchaseAmountText = string.Empty;
                DiscountAmount = 0m;
                CloseAgreementTooltip();
                RecalculateLiveFee();

                await _dialogService.ShowAlertAsync(
                    "Convenio No Aplicable",
                    $"El convenio '{agreement.Name}' no aplica para este vehículo porque la estadía ({FormatMinutesToHours(totalStay)}) supera el tiempo máximo permitido ({FormatMinutesToHours(maxAllowed)}).",
                    DialogNotificationType.Warning);
                return;
            }
        }

        SelectedAgreement = agreement;
        HasAgreementDiscount = true;
        CustomerPurchaseAmount = 0m; // Formulario limpio: NO pre-llenar con agreement.MinPurchaseAmount
        CustomerPurchaseAmountText = string.Empty;

        RecalculateLiveFee();
    }

    [RelayCommand]
    private void ShowAgreementDetails(CommercialAgreement? agreement)
    {
        if (agreement == null) return;

        PopupAgreement = agreement;

        var parts = new List<string>();
        if (agreement.DiscountPercentage.HasValue && agreement.DiscountPercentage.Value > 0)
        {
            parts.Add($"Descuento: {agreement.DiscountPercentage.Value:N0}%");
        }
        if (agreement.DiscountFixedAmount.HasValue && agreement.DiscountFixedAmount.Value > 0)
        {
            parts.Add($"Monto Fijo: ${agreement.DiscountFixedAmount.Value:N0}");
        }
        if (agreement.MinPurchaseAmount > 0)
        {
            parts.Add($"Compra Mínima: ${agreement.MinPurchaseAmount:N0}");
        }
        if (agreement.MaxHoursApplicable.HasValue && agreement.MaxHoursApplicable.Value > 0)
        {
            parts.Add($"Máx. {agreement.MaxHoursApplicable.Value} horas");
        }
        if (agreement.MaxMinutesApplicable.HasValue && agreement.MaxMinutesApplicable.Value > 0)
        {
            parts.Add($"Máx. {agreement.MaxMinutesApplicable.Value} min");
        }

        PopupDiscountSummary = parts.Count > 0 ? string.Join(" • ", parts) : "Tarifa de convenio preferencial";

        IsAgreementPopupOpen = true;
        _agreementPopupTimer.Stop();
        _agreementPopupTimer.Start();
    }

    [RelayCommand]
    private void CloseAgreementPopup()
    {
        _agreementPopupTimer.Stop();
        IsAgreementPopupOpen = false;
    }

    [RelayCommand]
    private void ShowAgreementTooltip()
    {
        _agreementTooltipTimer.Stop();
        IsAgreementTooltipOpen = true;
        _agreementTooltipTimer.Start();
    }

    [RelayCommand]
    private void CloseAgreementTooltip()
    {
        _agreementTooltipTimer.Stop();
        IsAgreementTooltipOpen = false;
    }

    partial void OnCustomerPurchaseAmountChanged(decimal value) => RecalculateLiveFee();

    partial void OnCustomerPurchaseAmountTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            CustomerPurchaseAmount = 0m;
        }
        else
        {
            var digits = new string(value.Where(char.IsDigit).ToArray());
            if (decimal.TryParse(digits, out var parsed))
            {
                CustomerPurchaseAmount = parsed;
            }
            else
            {
                CustomerPurchaseAmount = 0m;
            }
        }
        RecalculateLiveFee();
    }

    partial void OnHasAgreementDiscountChanged(bool value)
    {
        if (!value)
        {
            SelectedStore = null;
            SelectedAgreement = null;
            CustomerPurchaseAmount = 0m;
            CustomerPurchaseAmountText = string.Empty;
            InvoiceNumber = string.Empty;
            DiscountAmount = 0m;
            CloseAgreementTooltip();
        }

        RecalculateLiveFee();
    }

    async partial void OnSelectedTicketChanged(ParkingTicket? value)
    {
        IsMoreDataPopupOpen = false;
        OnPropertyChanged(nameof(FormattedCustomerPhone));
        OnPropertyChanged(nameof(FormattedNotes));

        if (value != null)
        {
            await LoadPaymentMethodsAsync();
            if (!HasPaymentMethods)
            {
                await _dialogService.ShowAlertAsync(
                    "Sin Medios de Pago en Sede",
                    "No es posible procesar el cobro ni dar salida al vehículo porque la sede activa no cuenta con ningún medio de pago registrado o habilitado en la base de datos.",
                    DialogNotificationType.Warning);
                SelectedTicket = null;
                return;
            }

            await LoadStoresAsync();
            await LoadResolutionsAsync();

            _ticketSelectionTimeUtc = DateTime.UtcNow;
            _frozenExitTimeUtc = _ticketSelectionTimeUtc;
            _isPaymentTimeoutDialogShowing = false;
            _graceRenewalCount = 0;

            var currentBranch = _sessionService.CurrentBranch;
            AllowMinute = currentBranch?.AllowChargeByMinute ?? true;
            AllowHour = currentBranch?.AllowChargeByHour ?? true;
            AllowDay = currentBranch?.AllowChargeByDay ?? true;
            AllowNight = currentBranch?.AllowChargeByNight ?? false;
            LostTicketFee = currentBranch?.LostTicketFee ?? 0m;
            HasLostTicketFee = LostTicketFee > 0;
            IsLostTicket = false;

            var rateInfo = _pricingCalculator.GetRate(value.VehicleType);
            var exitGrace = currentBranch?.ExitGracePeriodMinutes ?? rateInfo?.GracePeriodMinutes ?? 0;
            _currentGracePeriodSeconds = Math.Max(300, exitGrace * 60);
            HourRate = rateInfo?.HourRate ?? 0m;
            MinuteRate = rateInfo != null && rateInfo.MinuteRate > 0
                ? rateInfo.MinuteRate
                : (rateInfo != null && rateInfo.HourRate > 0 ? Math.Round(rateInfo.HourRate / 60m, 2) : 0m);

            if (AllowMinute && MinuteRate > 0)
            {
                RateSummaryText = $"${MinuteRate:N0} / min";
            }
            else if (AllowHour && HourRate > 0)
            {
                RateSummaryText = $"${HourRate:N0} / hora";
            }
            else if (rateInfo != null && rateInfo.FullDayRate > 0)
            {
                RateSummaryText = $"${rateInfo.FullDayRate:N0} plena";
            }
            else
            {
                RateSummaryText = "$0";
            }

            IsMonthlyTicket = (value.Notes?.Contains("Mensualidad", StringComparison.OrdinalIgnoreCase) ?? false);
            if (!IsMonthlyTicket)
            {
                try
                {
                    var sub = await _monthlySubscriptionService.GetActiveSubscriptionByPlateAsync(value.PlateNumber);
                    if (sub != null)
                    {
                        IsMonthlyTicket = true;
                    }
                }
                catch { }
            }

            _liveCalculationTimer.Start();
            HasAgreementDiscount = false;
            RecalculateLiveFee();
            AmountTendered = CalculatedFee;

            SelectedPaymentMethodEntity = AvailablePaymentMethods.FirstOrDefault();
            SelectedResolution = null;
            ShowPaymentMethodWarning = false;
            ShowResolutionWarning = false;

            HasElectronicInvoicingEnabled = _sessionService.CurrentUser?.HasElectronicInvoicingEnabled ?? false;
            ForceElectronicInvoiceOnCheckout = _sessionService.CurrentUser?.ForceElectronicInvoiceOnCheckout ?? false;
            CanToggleElectronicInvoice = !ForceElectronicInvoiceOnCheckout;
            EmitElectronicInvoice = ForceElectronicInvoiceOnCheckout;
            ShowCustomerWarning = false;
            IsQuickRegisterCustomerOpen = false;
            QuickCustomerFeedback = null;

            ExitNotes = string.Empty;

            if (HasElectronicInvoicingEnabled)
            {
                await LoadCustomersAsync();
                if (!string.IsNullOrWhiteSpace(value.PlateNumber))
                {
                    var cleanPlate = value.PlateNumber.Trim().ToUpperInvariant();
                    var match = AvailableCustomers.FirstOrDefault(c => c.Vehicles.Any(v => v.PlateNumber.Trim().ToUpperInvariant() == cleanPlate));
                    SelectedCustomer = match;
                    CustomerSearchText = match?.DisplayText ?? string.Empty;
                    ApplyCustomerFilter();
                }
                else
                {
                    SelectedCustomer = null;
                    CustomerSearchText = string.Empty;
                    ApplyCustomerFilter();
                }
            }
            else
            {
                SelectedCustomer = null;
                CustomerSearchText = string.Empty;
                ApplyCustomerFilter();
            }

            var dialogResult = await _dialogService.ShowCheckOutDialogAsync(this);
            if (SelectedTicket != null && !dialogResult)
            {
                SelectedTicket = null;
                RequestCloseDialog?.Invoke();
            }
        }
        else
        {
            _liveCalculationTimer.Stop();
            _isPaymentTimeoutDialogShowing = false;
            _ticketSelectionTimeUtc = default;
            _frozenExitTimeUtc = default;
            _currentGracePeriodSeconds = 0;
            _graceRenewalCount = 0;
            HourRate = 0m;
            MinuteRate = 0m;
            IsLostTicket = false;
            LostTicketFee = 0m;
            HasLostTicketFee = false;
            IsMonthlyTicket = false;
            GrossFee = 0m;
            DiscountAmount = 0m;
            CalculatedFee = 0m;
            ElapsedTimeString = "0min 0seg";
            AmountTendered = 0m;
            ChangeDue = 0m;
            HasAgreementDiscount = false;
            SelectedCustomer = null;
            EmitElectronicInvoice = false;
            ShowCustomerWarning = false;
            IsQuickRegisterCustomerOpen = false;
            QuickCustomerFeedback = null;
        }
    }

    partial void OnAmountTenderedChanged(decimal value) => CalculateChange();
    partial void OnCalculatedFeeChanged(decimal value) => CalculateChange();
    partial void OnIsLostTicketChanged(bool value) => RecalculateLiveFee();

    partial void OnEmitElectronicInvoiceChanged(bool value)
    {
        if (value)
        {
            _ = LoadCustomersAsync();
            if (SelectedResolution == null || !IsElectronicResolutionDefensive(SelectedResolution))
            {
                AutoSelectElectronicResolution();
            }
        }
        else
        {
            ShowCustomerWarning = false;
            IsQuickRegisterCustomerOpen = false;
            if (SelectedResolution != null && IsElectronicResolutionDefensive(SelectedResolution))
            {
                AutoSelectStandardResolution();
            }
        }
        OnPropertyChanged(nameof(IsElectronicInvoicingSectionVisible));
    }

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        if (value != null)
        {
            _isUpdatingCustomerSelection = true;
            try
            {
                CustomerSearchText = value.DisplayText;
                ShowCustomerWarning = false;
                IsCustomerDropDownOpen = false;

                if (!FilteredAvailableCustomers.Contains(value))
                {
                    FilteredAvailableCustomers.Insert(0, value);
                }
            }
            finally
            {
                _isUpdatingCustomerSelection = false;
            }
        }
    }

    partial void OnForceElectronicInvoiceOnCheckoutChanged(bool value)
    {
        CanToggleElectronicInvoice = !value;
        if (value)
        {
            EmitElectronicInvoice = true;
        }
    }

    [RelayCommand]
    private void ToggleQuickRegisterCustomer()
    {
        IsQuickRegisterCustomerOpen = !IsQuickRegisterCustomerOpen;
        QuickCustomerFeedback = null;
        NewCustomerDocumentError = null;
        NewCustomerCheckDigitError = null;
        NewCustomerFullNameError = null;
        NewCustomerEmailError = null;
        NewCustomerPhoneError = null;
        NewCustomerAddressError = null;
        NewCustomerCityError = null;

        if (IsQuickRegisterCustomerOpen)
        {
            SelectedIdentificationTypeOption = IdentificationTypeOptions.FirstOrDefault();
            IsNitDocumentType = SelectedIdentificationTypeOption?.Id == 31;
            NewCustomerPersonType = "Person";
            NewCustomerDocumentNumber = string.Empty;
            NewCustomerCheckDigit = null;
            NewCustomerFullName = string.Empty;
            NewCustomerEmail = string.Empty;
            NewCustomerPhone = string.Empty;
            NewCustomerAddress = string.Empty;
            NewCustomerCityCode = string.Empty;
            SelectedDaneMunicipality = AvailableMunicipalities.FirstOrDefault(m => m.Code == "11001") ?? AvailableMunicipalities.FirstOrDefault();
            if (SelectedDaneMunicipality != null)
            {
                NewCustomerCityCode = SelectedDaneMunicipality.Code;
            }
        }
    }

    public static string CalculateNitCheckDigit(string nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return "0";
        var cleanNit = new string(nit.Where(char.IsDigit).ToArray());
        if (cleanNit.Length == 0) return "0";

        int[] vpri = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
        int z = cleanNit.Length;
        int x = 0;
        for (int i = 0; i < z; i++)
        {
            var y = cleanNit[i] - '0';
            x += (y * vpri[z - 1 - i]);
        }
        int yRemainder = x % 11;
        return (yRemainder > 1 ? 11 - yRemainder : yRemainder).ToString();
    }

    private bool ValidateQuickCustomerForm()
    {
        bool isValid = true;
        NewCustomerDocumentError = null;
        NewCustomerCheckDigitError = null;
        NewCustomerFullNameError = null;
        NewCustomerEmailError = null;
        NewCustomerPhoneError = null;
        NewCustomerAddressError = null;
        NewCustomerCityError = null;

        var doc = NewCustomerDocumentNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(doc))
        {
            NewCustomerDocumentError = "El número de documento es obligatorio.";
            isValid = false;
        }
        else if (doc.Length < 4)
        {
            NewCustomerDocumentError = "Debe tener al menos 4 caracteres.";
            isValid = false;
        }

        var isNit = SelectedIdentificationTypeOption?.Id == 31;
        var dv = NewCustomerCheckDigit?.Trim();
        if (isNit)
        {
            if (string.IsNullOrWhiteSpace(dv))
            {
                NewCustomerCheckDigit = CalculateNitCheckDigit(doc);
            }
            else if (!Regex.IsMatch(dv, @"^[0-9]{1}$"))
            {
                NewCustomerCheckDigitError = "El DV debe ser un dígito (0-9).";
                isValid = false;
            }
        }
        else
        {
            NewCustomerCheckDigit = null;
            NewCustomerCheckDigitError = null;
        }

        var name = NewCustomerFullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            NewCustomerFullNameError = "El nombre o razón social es obligatorio.";
            isValid = false;
        }
        else if (name.Length < 3)
        {
            NewCustomerFullNameError = "Debe tener al menos 3 caracteres.";
            isValid = false;
        }

        var email = NewCustomerEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email))
        {
            NewCustomerEmailError = "El correo electrónico es obligatorio para la DIAN.";
            isValid = false;
        }
        else if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            NewCustomerEmailError = "Ingrese un correo válido (ej: cliente@correo.com).";
            isValid = false;
        }

        var phone = NewCustomerPhone?.Trim();
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!Regex.IsMatch(phone, @"^[0-9+\s\-]{7,15}$") || phone.Count(char.IsDigit) < 7)
            {
                NewCustomerPhoneError = "El teléfono debe contener solo números (mínimo 7).";
                isValid = false;
            }
        }

        var address = NewCustomerAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address))
        {
            NewCustomerAddressError = "La dirección fiscal es obligatoria para la DIAN.";
            isValid = false;
        }
        else if (address.Length < 4)
        {
            NewCustomerAddressError = "Debe tener al menos 4 caracteres.";
            isValid = false;
        }

        var city = NewCustomerCityCode?.Trim() ?? SelectedDaneMunicipality?.Code?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(city))
        {
            NewCustomerCityError = "Debe seleccionar el municipio DANE.";
            isValid = false;
        }

        if (!isValid)
        {
            QuickCustomerFeedback = "Por favor corrija los campos marcados en rojo.";
        }
        else
        {
            QuickCustomerFeedback = null;
        }

        return isValid;
    }

    partial void OnSelectedIdentificationTypeOptionChanged(IdentificationTypeOption? value)
    {
        IsNitDocumentType = value?.Id == 31;
        if (IsNitDocumentType)
        {
            NewCustomerPersonType = "Company";
            if (!string.IsNullOrWhiteSpace(NewCustomerDocumentNumber))
            {
                NewCustomerCheckDigit = CalculateNitCheckDigit(NewCustomerDocumentNumber);
                NewCustomerCheckDigitError = null;
            }
        }
        else
        {
            NewCustomerPersonType = "Person";
            NewCustomerCheckDigit = null;
            NewCustomerCheckDigitError = null;
        }
    }

    partial void OnSelectedDaneMunicipalityChanged(DaneMunicipality? value)
    {
        if (value != null)
        {
            NewCustomerCityCode = value.Code;
            NewCustomerCityError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
    }

    partial void OnNewCustomerAddressChanged(string value)
    {
        if (NewCustomerAddressError != null && !string.IsNullOrWhiteSpace(value) && value.Trim().Length >= 4)
        {
            NewCustomerAddressError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
    }

    partial void OnNewCustomerDocumentNumberChanged(string value)
    {
        if (NewCustomerDocumentError != null && !string.IsNullOrWhiteSpace(value))
        {
            NewCustomerDocumentError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
        if (IsNitDocumentType && !string.IsNullOrWhiteSpace(value))
        {
            NewCustomerCheckDigit = CalculateNitCheckDigit(value);
            NewCustomerCheckDigitError = null;
        }
        else if (!IsNitDocumentType)
        {
            NewCustomerCheckDigit = null;
            NewCustomerCheckDigitError = null;
        }
    }

    partial void OnCustomerSearchTextChanged(string value)
    {
        if (_isUpdatingCustomerSelection)
        {
            return;
        }

        ApplyCustomerFilter();

        if (SelectedCustomer != null && SelectedCustomer.DisplayText != value && SelectedCustomer.DocumentNumber != value)
        {
            SelectedCustomer = null;
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            IsCustomerDropDownOpen = FilteredAvailableCustomers.Count > 0;
        }
        else
        {
            IsCustomerDropDownOpen = false;
        }
    }

    public void ApplyCustomerFilter()
    {
        var query = CustomerSearchText?.Trim() ?? string.Empty;
        FilteredAvailableCustomers.Clear();
        if (string.IsNullOrWhiteSpace(query))
        {
            foreach (var c in AvailableCustomers)
            {
                FilteredAvailableCustomers.Add(c);
            }
        }
        else
        {
            var matches = AvailableCustomers
                .Where(c => (!string.IsNullOrEmpty(c.DocumentNumber) && c.DocumentNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
                         || (!string.IsNullOrEmpty(c.FullName) && c.FullName.Contains(query, StringComparison.OrdinalIgnoreCase))
                         || (!string.IsNullOrEmpty(c.DisplayText) && c.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (SelectedCustomer != null && !matches.Contains(SelectedCustomer) &&
                (SelectedCustomer.DisplayText.Equals(query, StringComparison.OrdinalIgnoreCase) || 
                 SelectedCustomer.DocumentNumber.Equals(query, StringComparison.OrdinalIgnoreCase)))
            {
                matches.Insert(0, SelectedCustomer);
            }

            foreach (var c in matches)
            {
                FilteredAvailableCustomers.Add(c);
            }
        }
    }

    partial void OnNewCustomerFullNameChanged(string value)
    {
        if (NewCustomerFullNameError != null && !string.IsNullOrWhiteSpace(value))
        {
            NewCustomerFullNameError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
    }

    partial void OnNewCustomerEmailChanged(string value)
    {
        if (NewCustomerEmailError != null && Regex.IsMatch(value?.Trim() ?? string.Empty, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            NewCustomerEmailError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
    }

    partial void OnNewCustomerPhoneChanged(string? value)
    {
        if (NewCustomerPhoneError != null)
        {
            if (string.IsNullOrWhiteSpace(value) || (Regex.IsMatch(value.Trim(), @"^[0-9+\s\-]{7,15}$") && value.Count(char.IsDigit) >= 7))
            {
                NewCustomerPhoneError = null;
                if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
            }
        }
    }

    partial void OnNewCustomerCheckDigitChanged(string? value)
    {
        if (NewCustomerCheckDigitError != null && (string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), @"^[0-9]{1}$")))
        {
            NewCustomerCheckDigitError = null;
            if (QuickCustomerFeedback != null) QuickCustomerFeedback = null;
        }
    }

    [RelayCommand]
    private async Task SaveQuickCustomerAsync()
    {
        if (!ValidateQuickCustomerForm())
        {
            return;
        }

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var docClean = NewCustomerDocumentNumber.Trim();
            var existing = await db.Customers.Include(c => c.Vehicles).FirstOrDefaultAsync(c => c.DocumentNumber == docClean);
            if (existing != null)
            {
                SelectedCustomer = existing;
                IsQuickRegisterCustomerOpen = false;
                QuickCustomerFeedback = null;
                ShowCustomerWarning = false;
                return;
            }

            var idType = SelectedIdentificationTypeOption?.Id ?? 13;
            var selectedMuni = SelectedDaneMunicipality ?? AvailableMunicipalities.FirstOrDefault(m => m.Code == NewCustomerCityCode.Trim());
            var effectiveCityCode = !string.IsNullOrWhiteSpace(NewCustomerCityCode) 
                ? NewCustomerCityCode.Trim() 
                : (selectedMuni?.Code ?? "11001");
            var effectiveStateCode = selectedMuni?.DepartmentCode ?? (effectiveCityCode.Length >= 2 ? effectiveCityCode.Substring(0, 2) : "11");

            var newCustomer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                CompanyId = _sessionService.CurrentUser?.CompanyId ?? 1,
                IdentificationTypeId = idType,
                DocumentNumber = docClean,
                CheckDigit = NewCustomerCheckDigit?.Trim(),
                PersonType = idType == 31 ? "Company" : NewCustomerPersonType,
                FullName = NewCustomerFullName.Trim(),
                Email = NewCustomerEmail.Trim(),
                Phone = string.IsNullOrWhiteSpace(NewCustomerPhone) ? null : NewCustomerPhone.Trim(),
                Address = NewCustomerAddress.Trim(),
                CityCode = effectiveCityCode,
                StateCode = effectiveStateCode,
                FiscalResponsibilities = "R-99-PN",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (SelectedTicket != null && !string.IsNullOrWhiteSpace(SelectedTicket.PlateNumber))
            {
                newCustomer.Vehicles.Add(new CustomerVehicle
                {
                    CustomerId = newCustomer.CustomerId,
                    PlateNumber = SelectedTicket.PlateNumber.Trim().ToUpperInvariant(),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            db.Customers.Add(newCustomer);

            // Encolar creación del cliente para sincronización con MySQL y que no falle el checkout offline
            var pendingItem = new PendingSyncItem
            {
                PendingSyncItemId = Guid.NewGuid(),
                OperationType = "CreateCustomer",
                PayloadJson = JsonSerializer.Serialize(new CreateCustomerApiRequest
                {
                    CustomerId = newCustomer.CustomerId,
                    CompanyId = newCustomer.CompanyId,
                    IdentificationTypeId = newCustomer.IdentificationTypeId,
                    DocumentNumber = newCustomer.DocumentNumber,
                    CheckDigit = newCustomer.CheckDigit,
                    PersonType = newCustomer.PersonType,
                    FullName = newCustomer.FullName,
                    Email = newCustomer.Email,
                    Phone = newCustomer.Phone,
                    Address = newCustomer.Address,
                    CityCode = newCustomer.CityCode,
                    StateCode = newCustomer.StateCode,
                    FiscalResponsibilities = newCustomer.FiscalResponsibilities,
                    InitialPlateNumber = SelectedTicket?.PlateNumber?.Trim().ToUpperInvariant()
                }, ParkingApiClient.JsonOptions),
                CreatedAtUtc = DateTime.UtcNow,
                IsProcessed = false
            };
            db.PendingSyncItems.Add(pendingItem);

            await db.SaveChangesAsync();

            // Si está en línea, intentar sincronizar de inmediato
            if (_syncEngine != null && _syncEngine.IsOnline)
            {
                _ = _syncEngine.ProcessPendingQueueAsync();
            }

            AvailableCustomers.Add(newCustomer);
            ApplyCustomerFilter();
            SelectedCustomer = newCustomer;
            IsQuickRegisterCustomerOpen = false;
            QuickCustomerFeedback = null;
            ShowCustomerWarning = false;
        }
        catch (Exception ex)
        {
            QuickCustomerFeedback = $"Error al guardar cliente: {ex.Message}";
        }
    }

    private void CalculateChange()
    {
        ChangeDue = Math.Max(0m, AmountTendered - CalculatedFee);
    }

    private void RecalculateLiveFee()
    {
        if (SelectedTicket == null || _ticketSelectionTimeUtc == default) return;

        var nowUtc = DateTime.UtcNow;

        // Si hay periodo de gracia de salida configurado (> 0), validar si expiró la tolerancia
        if (_currentGracePeriodSeconds > 0)
        {
            if ((nowUtc - _ticketSelectionTimeUtc).TotalSeconds >= _currentGracePeriodSeconds && !_isPaymentTimeoutDialogShowing)
            {
                _isPaymentTimeoutDialogShowing = true;
                _ = HandlePaymentTimeoutAsync();
                return;
            }
        }

        // Si no hay periodo de gracia (0 segundos), se calcula contra el tiempo actual en vivo
        var feeCalculationTime = _currentGracePeriodSeconds > 0 ? _frozenExitTimeUtc : nowUtc;
        var duration = feeCalculationTime - SelectedTicket.EntryTimeUtc;
        if (duration.TotalSeconds < 0) duration = TimeSpan.Zero;

        if (duration.TotalDays >= 1)
        {
            ElapsedTimeString = $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        }
        else if (duration.TotalHours >= 1)
        {
            ElapsedTimeString = $"{(int)duration.TotalHours}h {duration.Minutes}min {duration.Seconds}seg";
        }
        else
        {
            ElapsedTimeString = $"{duration.Minutes}min {duration.Seconds}seg";
        }

        if (IsMonthlyTicket)
        {
            GrossFee = 0m;
            DiscountAmount = 0m;
            CalculatedFee = 0m;
            AmountTendered = 0m;
            ChangeDue = 0m;
            return;
        }

        GrossFee = _pricingCalculator.CalculateFee(SelectedTicket.VehicleType, SelectedTicket.EntryTimeUtc, feeCalculationTime, 0, IsLostTicket);

        EvaluateAgreementEligibility();

        if (HasAgreementDiscount && SelectedAgreement != null && IsAgreementEligible)
        {
            if (SelectedAgreement.DiscountType == 2 || (SelectedAgreement.FreeHours.GetValueOrDefault(0) > 0 || SelectedAgreement.FreeMinutes.GetValueOrDefault(0) > 0))
            {
                int freeMins = (SelectedAgreement.FreeHours.GetValueOrDefault(0) * 60) + SelectedAgreement.FreeMinutes.GetValueOrDefault(0);
                if (freeMins <= 0 && AgreementMaxAllowedMinutes > 0 && SelectedAgreement.DiscountType == 2)
                {
                    freeMins = AgreementMaxAllowedMinutes;
                }
                var feeWithoutFreeTime = _pricingCalculator.CalculateFee(SelectedTicket.VehicleType, SelectedTicket.EntryTimeUtc, feeCalculationTime, 0, false);
                var feeWithFreeTime = _pricingCalculator.CalculateFee(SelectedTicket.VehicleType, SelectedTicket.EntryTimeUtc, feeCalculationTime, freeMins, false);
                DiscountAmount = Math.Max(0m, feeWithoutFreeTime - feeWithFreeTime);
            }
            else if (SelectedAgreement.DiscountPercentage.GetValueOrDefault(0) > 0)
            {
                DiscountAmount = Math.Round(GrossFee * (SelectedAgreement.DiscountPercentage!.Value / 100m), 2);
            }
            else if (SelectedAgreement.DiscountFixedAmount.GetValueOrDefault(0) > 0)
            {
                DiscountAmount = Math.Min(GrossFee, SelectedAgreement.DiscountFixedAmount!.Value);
            }
            else
            {
                DiscountAmount = _agreementService.CalculateDiscount(SelectedAgreement, CustomerPurchaseAmount, GrossFee);
            }
        }
        else
        {
            DiscountAmount = 0m;
        }

        CalculatedFee = Math.Max(0m, GrossFee - DiscountAmount);

        var requiresCash = SelectedPaymentMethodEntity?.RequiresCashTender ?? (SelectedPaymentMethod == PaymentMethod.Cash);
        if (AmountTendered < CalculatedFee && !requiresCash)
        {
            AmountTendered = CalculatedFee;
        }
    }

    private void EvaluateAgreementEligibility()
    {
        if (SelectedAgreement == null || SelectedTicket == null)
        {
            IsAgreementEligible = false;
            AgreementRequiresPurchase = false;
            AgreementMinPurchaseMet = true;
            AgreementMaxTimeMet = true;
            AgreementTotalStayMinutes = 0;
            AgreementMaxAllowedMinutes = 0;
            AgreementBenefitText = string.Empty;
            AgreementEligibilityBannerText = string.Empty;
            AgreementEligibilityBannerStatus = string.Empty;
            NotifyAgreementUiProperties();
            return;
        }

        var now = _currentGracePeriodSeconds > 0 ? _frozenExitTimeUtc : DateTime.UtcNow;
        var totalStay = Math.Max(0, (int)(now - SelectedTicket.EntryTimeUtc).TotalMinutes);
        AgreementTotalStayMinutes = totalStay;

        var maxAllowed = (SelectedAgreement.MaxHoursApplicable.GetValueOrDefault(0) * 60) + SelectedAgreement.MaxMinutesApplicable.GetValueOrDefault(0);
        AgreementMaxAllowedMinutes = maxAllowed;

        AgreementRequiresPurchase = SelectedAgreement.MinPurchaseAmount > 0;
        AgreementMinPurchaseMet = !AgreementRequiresPurchase || CustomerPurchaseAmount >= SelectedAgreement.MinPurchaseAmount;
        AgreementMaxTimeMet = maxAllowed <= 0 || totalStay <= maxAllowed;

        IsAgreementEligible = AgreementMinPurchaseMet && AgreementMaxTimeMet;
        AgreementBenefitText = FormatAgreementBenefit(SelectedAgreement);

        if (IsAgreementEligible)
        {
            AgreementEligibilityBannerText = "✓ Reglas cumplidas — Descuento aplicado";
            AgreementEligibilityBannerStatus = "success";
        }
        else if (!AgreementMinPurchaseMet && !AgreementMaxTimeMet)
        {
            AgreementEligibilityBannerText = "⚠️ Requiere compra y superó tiempo límite";
            AgreementEligibilityBannerStatus = "danger";
        }
        else if (!AgreementMinPurchaseMet)
        {
            AgreementEligibilityBannerText = "⚠️ Ingrese el valor de compra para aplicar";
            AgreementEligibilityBannerStatus = "warning";
        }
        else
        {
            AgreementEligibilityBannerText = "⚠️ Tiempo de estadía supera el máximo permitido";
            AgreementEligibilityBannerStatus = "danger";
        }

        NotifyAgreementUiProperties();
    }

    private void NotifyAgreementUiProperties()
    {
        OnPropertyChanged(nameof(AgreementMinPurchaseBadgeSuccessVisibility));
        OnPropertyChanged(nameof(AgreementMinPurchaseBadgeWarningVisibility));
        OnPropertyChanged(nameof(HasMaxAllowedTime));
        OnPropertyChanged(nameof(HasMaxAllowedTimeVisibility));
        OnPropertyChanged(nameof(NoMaxAllowedTimeVisibility));
        OnPropertyChanged(nameof(AgreementMaxTimeBadgeSuccessVisibility));
        OnPropertyChanged(nameof(AgreementMaxTimeBadgeDangerVisibility));
        OnPropertyChanged(nameof(AgreementMinPurchasePromptVisibility));
        OnPropertyChanged(nameof(AgreementMinPurchaseSuccessVisibility));
        OnPropertyChanged(nameof(AgreementMaxAllowedTimeString));
        OnPropertyChanged(nameof(AgreementStaySuccessString));
        OnPropertyChanged(nameof(AgreementStayDangerString));
        OnPropertyChanged(nameof(AgreementEligibilityBannerBrush));
        OnPropertyChanged(nameof(AgreementMinPurchaseBadgeBg));
        OnPropertyChanged(nameof(AgreementMinPurchaseBadgeFg));
        OnPropertyChanged(nameof(AgreementRuleStatusTitle));
        OnPropertyChanged(nameof(AgreementRuleStatusBrush));
        OnPropertyChanged(nameof(AgreementRuleStatusSubtitle));
        OnPropertyChanged(nameof(AgreementRuleStatusSubtitleVisibility));
        OnPropertyChanged(nameof(AgreementMinPurchaseStatusText));
        OnPropertyChanged(nameof(AgreementMinPurchaseTooltipStatusText));
        OnPropertyChanged(nameof(AgreementMinPurchaseRequiredText));
    }

    public static string FormatAgreementBenefit(CommercialAgreement ag)
    {
        if (ag.DiscountType == 2 || ag.FreeHours.GetValueOrDefault(0) > 0 || ag.FreeMinutes.GetValueOrDefault(0) > 0)
        {
            var parts = new List<string>();
            if (ag.FreeHours.GetValueOrDefault(0) > 0) parts.Add($"{ag.FreeHours!.Value}h");
            if (ag.FreeMinutes.GetValueOrDefault(0) > 0) parts.Add($"{ag.FreeMinutes!.Value}m");
            if (parts.Count == 0 && ag.MaxHoursApplicable.GetValueOrDefault(0) > 0)
            {
                parts.Add($"{ag.MaxHoursApplicable!.Value}h");
                if (ag.MaxMinutesApplicable.GetValueOrDefault(0) > 0) parts.Add($"{ag.MaxMinutesApplicable!.Value}m");
            }
            return parts.Count > 0 ? $"{string.Join(" ", parts)} GRATIS" : "TIEMPO LIBRE";
        }
        if (ag.DiscountPercentage.GetValueOrDefault(0) > 0)
        {
            return $"{ag.DiscountPercentage!.Value:N0}% DCTO";
        }
        if (ag.DiscountFixedAmount.GetValueOrDefault(0) > 0)
        {
            return $"${ag.DiscountFixedAmount!.Value:N0} DCTO";
        }
        if (ag.MaxHoursApplicable.GetValueOrDefault(0) > 0 || ag.MaxMinutesApplicable.GetValueOrDefault(0) > 0)
        {
            var parts = new List<string>();
            if (ag.MaxHoursApplicable.GetValueOrDefault(0) > 0) parts.Add($"{ag.MaxHoursApplicable!.Value}h");
            if (ag.MaxMinutesApplicable.GetValueOrDefault(0) > 0) parts.Add($"{ag.MaxMinutesApplicable!.Value}m");
            return $"{string.Join(" ", parts)} GRATIS";
        }
        return "CONVENIO";
    }

    public static string FormatMinutesToHours(int totalMinutes)
    {
        int hours = totalMinutes / 60;
        int mins = totalMinutes % 60;
        if (hours == 0) return $"{mins} min";
        if (mins == 0) return $"{hours} h";
        return $"{hours}h {mins}m";
    }

    public bool HasMaxAllowedTime => AgreementMaxAllowedMinutes > 0;

    public System.Windows.Visibility AgreementMinPurchaseBadgeSuccessVisibility =>
        AgreementRequiresPurchase && AgreementMinPurchaseMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility AgreementMinPurchaseBadgeWarningVisibility =>
        AgreementRequiresPurchase && !AgreementMinPurchaseMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility HasMaxAllowedTimeVisibility =>
        AgreementMaxAllowedMinutes > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility NoMaxAllowedTimeVisibility =>
        AgreementMaxAllowedMinutes <= 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility AgreementMaxTimeBadgeSuccessVisibility =>
        AgreementMaxAllowedMinutes > 0 && AgreementMaxTimeMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility AgreementMaxTimeBadgeDangerVisibility =>
        AgreementMaxAllowedMinutes > 0 && !AgreementMaxTimeMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility AgreementMinPurchasePromptVisibility =>
        AgreementRequiresPurchase && !AgreementMinPurchaseMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public System.Windows.Visibility AgreementMinPurchaseSuccessVisibility =>
        AgreementRequiresPurchase && AgreementMinPurchaseMet ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string AgreementMaxAllowedTimeString =>
        AgreementMaxAllowedMinutes > 0 ? FormatMinutesToHours(AgreementMaxAllowedMinutes) : "Sin límite";

    public string AgreementStaySuccessString =>
        $"✓ En tiempo ({FormatMinutesToHours(AgreementTotalStayMinutes)})";

    public string AgreementStayDangerString =>
        $"❌ Excedido ({FormatMinutesToHours(AgreementTotalStayMinutes)})";

    public System.Windows.Media.Brush AgreementEligibilityBannerBrush =>
        AgreementEligibilityBannerStatus switch
        {
            "success" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(52, 211, 153)), // #34D399
            "warning" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)), // #FBBF24
            _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113)) // #F87171
        };

    public System.Windows.Media.Brush AgreementMinPurchaseBadgeBg =>
        AgreementMinPurchaseMet
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 252, 231)) // #DCFCE7
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(254, 243, 199)); // #FEF3C7

    public System.Windows.Media.Brush AgreementMinPurchaseBadgeFg =>
        AgreementMinPurchaseMet
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 128, 61)) // #15803D
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 83, 9)); // #B45309

    public string AgreementRuleStatusTitle =>
        IsAgreementEligible
            ? $"✓ Convenio aplicado: {SelectedAgreement?.Name}"
            : $"⚠️ Convenio no cumple requisitos: {SelectedAgreement?.Name}";

    public System.Windows.Media.Brush AgreementRuleStatusBrush =>
        IsAgreementEligible
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(5, 150, 105)) // #059669
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 38, 38)); // #DC2626

    public string AgreementRuleStatusSubtitle =>
        !AgreementMaxTimeMet
            ? $"Estadía ({FormatMinutesToHours(AgreementTotalStayMinutes)}) supera el máximo permitido de {FormatMinutesToHours(AgreementMaxAllowedMinutes)}."
            : (!AgreementMinPurchaseMet
                ? $"Requiere compra mínima de ${SelectedAgreement?.MinPurchaseAmount:N0}."
                : string.Empty);

    public System.Windows.Visibility AgreementRuleStatusSubtitleVisibility =>
        !IsAgreementEligible && !string.IsNullOrEmpty(AgreementRuleStatusSubtitle) ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string AgreementMinPurchaseStatusText =>
        AgreementMinPurchaseMet ? "✓ Cumple compra mínima" : "⚠️ Monto insuficiente";

    public string AgreementMinPurchaseTooltipStatusText =>
        AgreementMinPurchaseMet ? "✓ Cumplida" : "⚠️ Pendiente";

    public string AgreementMinPurchaseRequiredText =>
        SelectedAgreement?.MinPurchaseAmount > 0 ? $"${SelectedAgreement.MinPurchaseAmount:N0}" : "No aplica";

    private async Task HandlePaymentTimeoutAsync()
    {
        _liveCalculationTimer.Stop();

        if (SelectedTicket == null || _ticketSelectionTimeUtc == default)
        {
            _isPaymentTimeoutDialogShowing = false;
            return;
        }

        _graceRenewalCount++;
        var graceMinutes = _currentGracePeriodSeconds / 60;

        if (_graceRenewalCount <= 3)
        {
            await _dialogService.ShowAlertAsync(
                "Tiempo de Pago Superado",
                $"Superó el tiempo de tolerancia de pago ({graceMinutes} minutos). Se actualizará el tiempo y cobro con los minutos transcurridos.",
                DialogNotificationType.Warning);

            if (SelectedTicket == null || _ticketSelectionTimeUtc == default)
            {
                _isPaymentTimeoutDialogShowing = false;
                return;
            }

            // Al hacer clic en Aceptar, refrescar tiempo y cobro sumando los minutos transcurridos
            var nowUtc = DateTime.UtcNow;
            _ticketSelectionTimeUtc = nowUtc;
            _frozenExitTimeUtc = nowUtc;
            _isPaymentTimeoutDialogShowing = false;

            RecalculateLiveFee();
            AmountTendered = CalculatedFee;

            _liveCalculationTimer.Start();
        }
        else
        {
            await _dialogService.ShowAlertAsync(
                "Límite de Tolerancia Excedido",
                "Se alcanzó el límite máximo de tolerancia de pago para esta liquidación. Por favor finalice el cobro o vuelva a seleccionar el vehículo.",
                DialogNotificationType.Warning);

            _isPaymentTimeoutDialogShowing = false;
        }
    }

    private bool CanSearchTicket => !string.IsNullOrWhiteSpace(SearchQuery);

    [RelayCommand(CanExecute = nameof(CanSearchTicket))]
    private async Task SearchTicketAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        var ticket = await _ticketService.FindActiveTicketAsync(SearchQuery);
        if (ticket != null)
        {
            SelectedTicket = ticket;
            SearchQuery = string.Empty;
            HasFeedback = false;
            FeedbackMessage = null;
        }
        else
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"No se encontró ningún vehículo activo con placa o tiquete '{SearchQuery}'.";
        }
    }

    [RelayCommand]
    private void SelectActiveVehicle(ParkingTicket ticket)
    {
        SelectedTicket = ticket;
        SearchQuery = string.Empty;
        HasFeedback = false;
        FeedbackMessage = null;
    }

    [RelayCommand]
    private void QuickCash(object? parameter)
    {
        if (parameter != null && decimal.TryParse(parameter.ToString(), out var amount))
        {
            AmountTendered = amount;
        }
    }

    [RelayCommand]
    private void ExactCash()
    {
        AmountTendered = CalculatedFee;
    }

    [RelayCommand]
    private void SelectPaymentMethodEnum(PaymentMethod method)
    {
        SelectedPaymentMethod = method;
        var entity = AvailablePaymentMethods.FirstOrDefault(pm => pm.ToEnum() == method);
        if (entity != null)
        {
            SelectedPaymentMethodEntity = entity;
            return;
        }

        var requiresCash = method == PaymentMethod.Cash;
        if (!requiresCash)
        {
            AmountTendered = CalculatedFee;
            ChangeDue = 0m;
        }
        else
        {
            CalculateChange();
        }
    }

    [RelayCommand]
    private async Task ProcessPaymentAsync()
    {
        if (SelectedTicket == null)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Debe seleccionar un vehículo activo para liquidar.";
            return;
        }

        // Validación preventiva en línea: si el vehículo ya fue liquidado centralmente desde PWA
        if (_syncEngine != null && _syncEngine.IsOnline && _apiClient != null)
        {
            try
            {
                var remoteCheck = await _apiClient.GetTicketByIdAsync(SelectedTicket.TicketId);
                if (remoteCheck != null && remoteCheck.Status == TicketStatus.Completed)
                {
                    var plate = SelectedTicket.PlateNumber;
                    SelectedTicket = null;
                    IsAgreementPopupOpen = false;
                    await _ticketService.HandleRemoteTicketCheckOutAsync(remoteCheck.TicketId, remoteCheck.PlateNumber, remoteCheck.BranchId);
                    await LoadActiveVehiclesAsync();

                    await _dialogService.ShowAlertAsync(
                        "Vehículo Ya Liquidado",
                        $"El vehículo con placa '{plate}' ya fue liquidado centralmente desde el panel central (PWA).\n\nSe ha actualizado el terminal con la información canónica para evitar un doble cobro.",
                        DialogNotificationType.Warning);
                    return;
                }
            }
            catch { }
        }

        if (HasAgreementDiscount && !IsMonthlyTicket)
        {
            if (SelectedAgreement == null)
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = "Debe seleccionar un convenio válido.";
                return;
            }

            EvaluateAgreementEligibility();
            if (!IsAgreementEligible)
            {
                if (!AgreementMaxTimeMet)
                {
                    var name = SelectedAgreement.Name;
                    SelectedAgreement = null;
                    HasAgreementDiscount = false;
                    DiscountAmount = 0m;
                    RecalculateLiveFee();

                    await _dialogService.ShowAlertAsync(
                        "Convenio Desmarcado",
                        $"El convenio '{name}' no es aplicable porque la estadía supera el tiempo máximo permitido ({FormatMinutesToHours(AgreementMaxAllowedMinutes)}). Se ha desmarcado el convenio para continuar.",
                        DialogNotificationType.Warning);
                    return;
                }

                if (!AgreementMinPurchaseMet)
                {
                    await _dialogService.ShowAlertAsync(
                        "Compra Mínima Requerida",
                        $"El convenio '{SelectedAgreement.Name}' requiere una compra mínima de ${SelectedAgreement.MinPurchaseAmount:N0} en el comercio aliado. Ingrese el monto de compra válido o desmarque el convenio para continuar con la salida.",
                        DialogNotificationType.Warning);
                    return;
                }
            }

            if (SelectedStore == null && SelectedAgreement.StoreId != Guid.Empty)
            {
                SelectedStore = AvailableStores.FirstOrDefault(s => s.StoreId == SelectedAgreement.StoreId);
            }

            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                InvoiceNumber = $"CONV-{SelectedAgreement.Name?.Trim().ToUpperInvariant() ?? "SEDE"}";
            }
        }

        if (!IsMonthlyTicket)
        {
            if (!HasPaymentMethods)
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = "No se puede liquidar el cobro ni dar salida porque no existen medios de pago habilitados para esta sede.";
                await _dialogService.ShowAlertAsync(
                    "Sin Medios de Pago en Sede",
                    "No es posible procesar el cobro ni dar salida al vehículo porque la sede no cuenta con ningún medio de pago registrado o habilitado en la base de datos.",
                    DialogNotificationType.Warning);
                return;
            }

            bool hasValidationError = false;
            if (SelectedPaymentMethodEntity == null)
            {
                ShowPaymentMethodWarning = true;
                hasValidationError = true;
            }
            else
            {
                ShowPaymentMethodWarning = false;
            }

            if (SelectedResolution == null)
            {
                ShowResolutionWarning = true;
                hasValidationError = true;
            }
            else
            {
                ShowResolutionWarning = false;
            }

            bool customerRequired = EmitElectronicInvoice && SelectedCustomer == null && !(_sessionService.CurrentUser?.AllowAnonymousInvoice ?? false);

            if (customerRequired)
            {
                ShowCustomerWarning = true;
                hasValidationError = true;
            }
            else
            {
                ShowCustomerWarning = false;
            }

            if (hasValidationError)
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = customerRequired
                    ? "Debe seleccionar un cliente / adquirente para emitir la Factura Electrónica."
                    : "Por favor seleccione el método de pago y la resolución requeridos.";

                if (customerRequired)
                {
                    await _dialogService.ShowAlertAsync(
                        "Adquirente Requerido",
                        "Ha seleccionado emitir Factura Electrónica (DIAN), pero no ha seleccionado ningún cliente. Por favor seleccione o registre uno.",
                        DialogNotificationType.Warning);
                }
                return;
            }
        }

        var methodEnum = SelectedPaymentMethodEntity?.ToEnum() ?? PaymentMethod.Cash;
        var requiresCash = !IsMonthlyTicket && (SelectedPaymentMethodEntity?.RequiresCashTender ?? (SelectedPaymentMethod == PaymentMethod.Cash));

        if (requiresCash && AmountTendered < CalculatedFee)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"El monto en efectivo recibido (${AmountTendered:F2}) es menor al total neto (${CalculatedFee:F2}).";
            return;
        }

        IsBusy = true;
        BusyMessage = IsMonthlyTicket
            ? "Registrando salida de vehículo abonado y liberando cupo..."
            : "Procesando cobro, liquidación y liberando cupo...";

        try
        {
            var paidAmount = IsMonthlyTicket ? 0m : (requiresCash ? AmountTendered : CalculatedFee);
            var discount = IsMonthlyTicket ? 0m : DiscountAmount;

            string? generatedInvoiceNumber = null;
            if (SelectedResolution != null && !IsMonthlyTicket)
            {
                generatedInvoiceNumber = await _billingResolutionService.ConsumeNextInvoiceNumberAsync(SelectedResolution.ResolutionId);
            }

            if (SelectedTicket == null) return;

            var completedTicket = await _ticketService.ProcessExitAsync(
                SelectedTicket.TicketId,
                methodEnum,
                paidAmount,
                HasAgreementDiscount ? SelectedStore?.StoreId : null,
                HasAgreementDiscount ? SelectedAgreement?.AgreementId : null,
                HasAgreementDiscount ? InvoiceNumber : null,
                HasAgreementDiscount ? CustomerPurchaseAmount : null,
                discount,
                SelectedPaymentMethodEntity?.Id,
                IsMonthlyTicket ? "Salida Abonado / Mensualidad" : ExitNotes,
                _frozenExitTimeUtc,
                SelectedResolution?.ResolutionId,
                SelectedResolution?.Name ?? SelectedResolution?.DocumentType,
                generatedInvoiceNumber,
                IsLostTicket,
                IsLostTicket ? LostTicketFee : 0m,
                EmitElectronicInvoice,
                SelectedCustomer?.CustomerId);

            if (completedTicket != null)
            {
                var clearedPlate = completedTicket.PlateNumber;
                var totalPaid = completedTicket.NetAmount;
                var change = completedTicket.ChangeGiven;

                SelectedTicket = null;
                RequestCloseDialog?.Invoke();
                SearchQuery = string.Empty;
                AmountTendered = 0m;
                ChangeDue = 0m;
                ExitNotes = string.Empty;
                HasAgreementDiscount = false;
                IsLostTicket = false;
                SelectedCustomer = null;
                EmitElectronicInvoice = ForceElectronicInvoiceOnCheckout;
                ShowCustomerWarning = false;
                IsQuickRegisterCustomerOpen = false;
                QuickCustomerFeedback = null;

                HasFeedback = false;
                FeedbackMessage = null;

                var shouldPrint = await _dialogService.ShowConfirmationAsync(
                    "Impresión de Factura",
                    "¿Desea imprimir la factura / tiquete de salida?",
                    DialogNotificationType.Question,
                    "Sí, Imprimir",
                    "No Imprimir");

                if (shouldPrint)
                {
                    await _dialogService.ShowReceiptPreviewAsync(completedTicket, SelectedResolution);
                }
            }
        }
        catch (Exception ex)
        {
            var rawError = ex.InnerException?.Message ?? ex.Message;
            var friendlyError = rawError;

            if (rawError.Contains("no such table", StringComparison.OrdinalIgnoreCase))
            {
                friendlyError = "La estructura de datos local requería actualización y ya fue reparada. Por favor presione de nuevo en Cobrar.";
            }
            else if (rawError.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase))
            {
                friendlyError = "El número de factura de comercio ya fue registrado anteriormente con este convenio.";
            }

            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"No se pudo completar la salida: {friendlyError}";

            await _dialogService.ShowAlertAsync(
                "Error en Liquidación",
                $"No se pudo procesar la salida del vehículo: {friendlyError}",
                DialogNotificationType.Error);
        }

        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void CancelSelection()
    {
        SelectedTicket = null;
        RequestCloseDialog?.Invoke();
        SearchQuery = string.Empty;
        AmountTendered = 0m;
        ChangeDue = 0m;
        ExitNotes = string.Empty;
        HasAgreementDiscount = false;
        HasFeedback = false;
        FeedbackMessage = null;
        SelectedCustomer = null;
        CustomerSearchText = string.Empty;
        ApplyCustomerFilter();
        EmitElectronicInvoice = ForceElectronicInvoiceOnCheckout;
        ShowCustomerWarning = false;
        IsQuickRegisterCustomerOpen = false;
        QuickCustomerFeedback = null;
    }
}
