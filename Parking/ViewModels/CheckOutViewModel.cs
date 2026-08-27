using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

[RequirePermission("checkout.view", "Salida y Cobro / Caja")]
public partial class CheckOutViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IPricingCalculatorService _pricingCalculator;
    private readonly IMonthlySubscriptionService _monthlySubscriptionService;
    private readonly IStoreService _storeService;
    private readonly IAgreementService _agreementService;
    private readonly IDialogService _dialogService;
    private readonly IDbConnectionManager _connectionManager;
    private readonly DispatcherTimer _liveCalculationTimer;
    private DateTime _ticketSelectionTimeUtc;
    private DateTime _frozenExitTimeUtc;
    private int _currentGracePeriodSeconds = 900;
    private bool _isPaymentTimeoutDialogShowing;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private bool _isMonthlyTicket;

    [ObservableProperty]
    private decimal _grossFee;

    [ObservableProperty]
    private decimal _minuteRate;

    [ObservableProperty]
    private decimal _discountAmount;

    [ObservableProperty]
    private decimal _calculatedFee;

    [ObservableProperty]
    private string _elapsedTimeString = "0min 0seg";

    [ObservableProperty]
    private PaymentMethod _selectedPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private PaymentMethodEntity? _selectedPaymentMethodEntity;

    [ObservableProperty]
    private bool _hasPaymentMethods;

    [ObservableProperty]
    private string _exitNotes = string.Empty;

    [ObservableProperty]
    private decimal _amountTendered;

    [ObservableProperty]
    private decimal _changeDue;

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

    public ObservableCollection<ParkingTicket> ActiveVehicles { get; } = new();
    public ObservableCollection<Store> AvailableStores { get; } = new();
    public ObservableCollection<CommercialAgreement> AvailableAgreements { get; } = new();
    public ObservableCollection<PaymentMethodEntity> AvailablePaymentMethods { get; } = new();

    public CheckOutViewModel(
        IParkingTicketService ticketService,
        IPricingCalculatorService pricingCalculator,
        IMonthlySubscriptionService monthlySubscriptionService,
        IStoreService storeService,
        IAgreementService agreementService,
        IDialogService dialogService,
        IDbConnectionManager connectionManager,
        ISyncEngineService syncEngine)
    {
        _ticketService = ticketService;
        _pricingCalculator = pricingCalculator;
        _monthlySubscriptionService = monthlySubscriptionService;
        _storeService = storeService;
        _agreementService = agreementService;
        _dialogService = dialogService;
        _connectionManager = connectionManager;

        syncEngine.DataSynchronized += async () =>
        {
            await InitializeAsync();
        };

        _liveCalculationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveCalculationTimer.Tick += (s, e) => RecalculateLiveFee();

        _ticketService.TicketRegistered += (s, t) => _ = LoadActiveVehiclesAsync();
        _ticketService.TicketCompleted += (s, t) => _ = LoadActiveVehiclesAsync();
    }

    public override async Task InitializeAsync()
    {
        await LoadPaymentMethodsAsync();
        await LoadActiveVehiclesAsync();
        await LoadStoresAsync();
    }

    private async Task LoadPaymentMethodsAsync()
    {
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var methods = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(db.PaymentMethods.Where(p => p.State));

            AvailablePaymentMethods.Clear();
            foreach (var m in methods)
            {
                AvailablePaymentMethods.Add(m);
            }

            HasPaymentMethods = AvailablePaymentMethods.Count > 0;

            SelectedPaymentMethodEntity = AvailablePaymentMethods.FirstOrDefault(p => p.Name.Equals("Efectivo", StringComparison.OrdinalIgnoreCase)) ?? AvailablePaymentMethods.FirstOrDefault();
            if (SelectedPaymentMethodEntity != null)
            {
                SelectedPaymentMethod = SelectedPaymentMethodEntity.ToEnum();
            }
        }
        catch { }
    }

    [RelayCommand]
    private void SelectPaymentMethod(PaymentMethodEntity method)
    {
        if (method == null) return;
        SelectedPaymentMethodEntity = method;
        SelectedPaymentMethod = method.ToEnum();

        if (!method.RequiresCashTender)
        {
            AmountTendered = CalculatedFee;
            ChangeDue = 0m;
        }
        else
        {
            CalculateChange();
        }
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
        foreach (var s in stores)
        {
            AvailableStores.Add(s);
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var sanitized = SanitizePlateQuery(value);
        if (sanitized != value)
        {
            SearchQuery = sanitized;
            return;
        }
        _ = SearchTicketAsync();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
        HasFeedback = false;
        FeedbackMessage = null;
        SelectedTicket = null;
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

    partial void OnCustomerPurchaseAmountChanged(decimal value) => RecalculateLiveFee();

    partial void OnHasAgreementDiscountChanged(bool value)
    {
        if (!value)
        {
            SelectedStore = null;
            SelectedAgreement = null;
            CustomerPurchaseAmount = 0m;
            InvoiceNumber = string.Empty;
            DiscountAmount = 0m;
        }

        RecalculateLiveFee();
    }

    async partial void OnSelectedTicketChanged(ParkingTicket? value)
    {
        if (value != null)
        {
            _ticketSelectionTimeUtc = DateTime.UtcNow;
            _frozenExitTimeUtc = _ticketSelectionTimeUtc;
            _isPaymentTimeoutDialogShowing = false;

            var rateInfo = _pricingCalculator.GetRate(value.VehicleType);
            _currentGracePeriodSeconds = (rateInfo != null && rateInfo.GracePeriodMinutes > 0 ? rateInfo.GracePeriodMinutes : 15) * 60;
            MinuteRate = rateInfo != null && rateInfo.MinuteRate > 0
                ? rateInfo.MinuteRate
                : (rateInfo != null && rateInfo.HourRate > 0 ? Math.Round(rateInfo.HourRate / 60m, 2) : 0m);

            IsMonthlyTicket = value.HourlyRate == 0m || (value.Notes?.Contains("Mensualidad", StringComparison.OrdinalIgnoreCase) ?? false);
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
        }
        else
        {
            _liveCalculationTimer.Stop();
            _isPaymentTimeoutDialogShowing = false;
            _ticketSelectionTimeUtc = default;
            _frozenExitTimeUtc = default;
            _currentGracePeriodSeconds = 900;
            MinuteRate = 0m;
            IsMonthlyTicket = false;
            GrossFee = 0m;
            DiscountAmount = 0m;
            CalculatedFee = 0m;
            ElapsedTimeString = "0min 0seg";
            AmountTendered = 0m;
            ChangeDue = 0m;
            HasAgreementDiscount = false;
        }
    }

    partial void OnAmountTenderedChanged(decimal value) => CalculateChange();
    partial void OnCalculatedFeeChanged(decimal value) => CalculateChange();

    private void CalculateChange()
    {
        ChangeDue = Math.Max(0m, AmountTendered - CalculatedFee);
    }

    private void RecalculateLiveFee()
    {
        if (SelectedTicket == null || _ticketSelectionTimeUtc == default) return;

        var nowUtc = DateTime.UtcNow;

        // Si transcurrió el periodo de gracia configurado desde que se escaneó/seleccionó el tiquete sin cobrar
        var allowedGraceSeconds = _currentGracePeriodSeconds > 0 ? _currentGracePeriodSeconds : 900;
        if ((nowUtc - _ticketSelectionTimeUtc).TotalSeconds >= allowedGraceSeconds && !_isPaymentTimeoutDialogShowing)
        {
            _isPaymentTimeoutDialogShowing = true;
            _ = HandlePaymentTimeoutAsync();
            return;
        }

        var feeCalculationTime = _frozenExitTimeUtc;
        var duration = feeCalculationTime - SelectedTicket.EntryTimeUtc;
        if (duration.TotalSeconds < 0) duration = TimeSpan.Zero;

        if (duration.TotalDays >= 1)
        {
            ElapsedTimeString = $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}min {duration.Seconds}seg";
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

        GrossFee = _pricingCalculator.CalculateFee(SelectedTicket.VehicleType, SelectedTicket.EntryTimeUtc, feeCalculationTime);

        if (HasAgreementDiscount && SelectedAgreement != null)
        {
            DiscountAmount = _agreementService.CalculateDiscount(SelectedAgreement, CustomerPurchaseAmount, GrossFee);
        }
        else
        {
            DiscountAmount = 0m;
        }

        CalculatedFee = Math.Max(0m, GrossFee - DiscountAmount);

        if (AmountTendered < CalculatedFee && SelectedPaymentMethod != PaymentMethod.Cash)
        {
            AmountTendered = CalculatedFee;
        }
    }

    private async Task HandlePaymentTimeoutAsync()
    {
        _liveCalculationTimer.Stop();

        if (SelectedTicket == null || _ticketSelectionTimeUtc == default)
        {
            _isPaymentTimeoutDialogShowing = false;
            return;
        }

        var graceMinutes = _currentGracePeriodSeconds / 60;
        await _dialogService.ShowAlertAsync(
            "Periodo de Gracia Superado",
            $"Se ha superado el tiempo de gracia de liquidación ({graceMinutes} min). Se actualizará el cobro con el tiempo transcurrido.",
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

    [RelayCommand]
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
        if (method != PaymentMethod.Cash)
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

        if (HasAgreementDiscount && !IsMonthlyTicket)
        {
            if (SelectedStore == null || SelectedAgreement == null)
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = "Debe seleccionar un comercio y convenio válido.";
                return;
            }

            if (string.IsNullOrWhiteSpace(InvoiceNumber))
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = "El número de factura del comercio es obligatorio para aplicar el convenio.";
                return;
            }

            if (CustomerPurchaseAmount < SelectedAgreement.MinPurchaseAmount)
            {
                HasFeedback = true;
                IsSuccessFeedback = false;
                FeedbackMessage = $"El valor de compra (${CustomerPurchaseAmount:N0}) no alcanza el mínimo requerido (${SelectedAgreement.MinPurchaseAmount:N0}) para este convenio.";
                return;
            }
        }

        if (!IsMonthlyTicket && (!HasPaymentMethods || SelectedPaymentMethodEntity == null))
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

        var methodEnum = SelectedPaymentMethodEntity?.ToEnum() ?? PaymentMethod.Cash;
        var requiresCash = !IsMonthlyTicket && (SelectedPaymentMethodEntity?.RequiresCashTender ?? true);

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
                _frozenExitTimeUtc);

            if (completedTicket != null)
            {
                var clearedPlate = completedTicket.PlateNumber;
                var totalPaid = completedTicket.NetAmount;
                var change = completedTicket.ChangeGiven;

                SelectedTicket = null;
                SearchQuery = string.Empty;
                AmountTendered = 0m;
                ChangeDue = 0m;
                ExitNotes = string.Empty;
                HasAgreementDiscount = false;

                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = IsMonthlyTicket
                    ? $"Salida registrada para vehículo con mensualidad {clearedPlate}. Cupo liberado exitosamente (Sin cobro horario)."
                    : $"Pago procesado para {clearedPlate}. Total Neto: ${totalPaid:F2}. Cambio: ${change:F2}. Cupo liberado exitosamente.";

                await _dialogService.ShowReceiptPreviewAsync(completedTicket);
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
    private void CancelSelection() => ClearSelection();

    [RelayCommand]
    private void ClearSelection()
    {
        SelectedTicket = null;
        SearchQuery = string.Empty;
        AmountTendered = 0m;
        ChangeDue = 0m;
        ExitNotes = string.Empty;
        HasAgreementDiscount = false;
        HasFeedback = false;
        FeedbackMessage = null;
    }
}
