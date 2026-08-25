using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

[RequirePermission("subscriptions.view", "Mensualidades y Abonados")]
public partial class MonthlySubscriptionsViewModel : ViewModelBase
{
    private readonly IMonthlySubscriptionService _subscriptionService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    [ObservableProperty]
    private int _activeCount;

    [ObservableProperty]
    private int _expiringSoonCount;

    [ObservableProperty]
    private int _expiredCount;

    [ObservableProperty]
    private decimal _totalMonthlyRevenue;

    [ObservableProperty]
    private bool _isFormOpen;

    [ObservableProperty]
    private bool _isRenewMode;

    [ObservableProperty]
    private MonthlySubscription? _selectedSubscription;

    // Form fields
    [ObservableProperty]
    private string _formCustomerName = string.Empty;

    [ObservableProperty]
    private string _formCustomerDocument = string.Empty;

    [ObservableProperty]
    private string _formCustomerPhone = string.Empty;

    [ObservableProperty]
    private string _formCustomerEmail = string.Empty;

    [ObservableProperty]
    private string _formPlateNumber = string.Empty;

    [ObservableProperty]
    private VehicleType _formVehicleType = VehicleType.Car;

    [ObservableProperty]
    private DateTime _formStartDate = DateTime.Today;

    [ObservableProperty]
    private DateTime _formEndDate = DateTime.Today.AddMonths(1);

    [ObservableProperty]
    private decimal _formMonthlyFee = 150000m;

    [ObservableProperty]
    private decimal _formAmountPaid = 150000m;

    [ObservableProperty]
    private PaymentMethod _formPaymentMethod = PaymentMethod.Cash;

    [ObservableProperty]
    private string _formNotes = string.Empty;

    [ObservableProperty]
    private string? _formErrorMessage;

    public ObservableCollection<MonthlySubscription> Subscriptions { get; } = new();

    public MonthlySubscriptionsViewModel(
        IMonthlySubscriptionService subscriptionService,
        IDialogService dialogService,
        ISyncEngineService syncEngine)
    {
        _subscriptionService = subscriptionService;
        _dialogService = dialogService;

        syncEngine.DataSynchronized += async () =>
        {
            await LoadSubscriptionsAsync();
        };

        _subscriptionService.SubscriptionsChanged += (s, e) => _ = LoadSubscriptionsAsync();
    }

    public override async Task InitializeAsync()
    {
        await LoadSubscriptionsAsync();
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadSubscriptionsAsync();
    partial void OnSelectedFilterChanged(string value) => _ = LoadSubscriptionsAsync();

    [RelayCommand]
    public async Task LoadSubscriptionsAsync()
    {
        IsBusy = true;
        BusyMessage = "Cargando listado de mensualidades y abonados...";

        try
        {
            var all = await _subscriptionService.GetAllSubscriptionsAsync();

            ActiveCount = all.Count(s => s.IsCurrentlyValid && s.DaysRemaining > 5);
            ExpiringSoonCount = all.Count(s => s.IsCurrentlyValid && s.DaysRemaining <= 5);
            ExpiredCount = all.Count(s => !s.IsCurrentlyValid);
            TotalMonthlyRevenue = all.Sum(s => s.AmountPaid);

            var query = (SearchQuery ?? string.Empty).Trim().ToUpperInvariant();
            var filtered = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                filtered = filtered.Where(s =>
                    s.PlateNumber.Contains(query) ||
                    s.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.CustomerDocument.Contains(query) ||
                    s.CustomerPhone.Contains(query));
            }

            if (SelectedFilter == "Activos")
            {
                filtered = filtered.Where(s => s.IsCurrentlyValid);
            }
            else if (SelectedFilter == "PorVencer")
            {
                filtered = filtered.Where(s => s.IsCurrentlyValid && s.DaysRemaining <= 5);
            }
            else if (SelectedFilter == "Vencidos")
            {
                filtered = filtered.Where(s => !s.IsCurrentlyValid);
            }

            Subscriptions.Clear();
            foreach (var sub in filtered)
            {
                Subscriptions.Add(sub);
            }
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void FilterBy(string filter)
    {
        SelectedFilter = filter;
    }

    [RelayCommand]
    private void OpenNewForm()
    {
        IsRenewMode = false;
        FormCustomerName = string.Empty;
        FormCustomerDocument = string.Empty;
        FormCustomerPhone = string.Empty;
        FormCustomerEmail = string.Empty;
        FormPlateNumber = string.Empty;
        FormVehicleType = VehicleType.Car;
        FormStartDate = DateTime.Today;
        FormEndDate = DateTime.Today.AddMonths(1);
        FormMonthlyFee = 150000m;
        FormAmountPaid = 150000m;
        FormPaymentMethod = PaymentMethod.Cash;
        FormNotes = string.Empty;
        FormErrorMessage = null;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void OpenRenewForm(MonthlySubscription subscription)
    {
        if (subscription == null) return;
        SelectedSubscription = subscription;
        IsRenewMode = true;

        FormCustomerName = subscription.CustomerName;
        FormCustomerDocument = subscription.CustomerDocument;
        FormCustomerPhone = subscription.CustomerPhone;
        FormCustomerEmail = subscription.CustomerEmail ?? string.Empty;
        FormPlateNumber = subscription.PlateNumber;
        FormVehicleType = subscription.VehicleType;
        FormStartDate = subscription.EndDate > DateTime.Today ? subscription.EndDate : DateTime.Today;
        FormEndDate = FormStartDate.AddMonths(1);
        FormMonthlyFee = subscription.MonthlyFee;
        FormAmountPaid = subscription.MonthlyFee;
        FormPaymentMethod = PaymentMethod.Cash;
        FormNotes = string.Empty;
        FormErrorMessage = null;
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        IsFormOpen = false;
        FormErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveSubscriptionAsync()
    {
        if (string.IsNullOrWhiteSpace(FormPlateNumber))
        {
            FormErrorMessage = "La placa del vehículo es obligatoria.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormCustomerName))
        {
            FormErrorMessage = "El nombre del cliente abonado es obligatorio.";
            return;
        }

        if (string.IsNullOrWhiteSpace(FormCustomerPhone))
        {
            FormErrorMessage = "El teléfono de contacto es obligatorio.";
            return;
        }

        if (FormAmountPaid <= 0)
        {
            FormErrorMessage = "El monto pagado debe ser mayor a $0.";
            return;
        }

        IsBusy = true;
        BusyMessage = IsRenewMode ? "Renovando suscripción mensual..." : "Registrando nueva mensualidad...";

        try
        {
            if (IsRenewMode && SelectedSubscription != null)
            {
                await _subscriptionService.RenewSubscriptionAsync(
                    SelectedSubscription.SubscriptionId,
                    1,
                    FormAmountPaid,
                    FormPaymentMethod,
                    FormNotes);

                await _dialogService.ShowAlertAsync(
                    "Renovación Exitosa",
                    $"La mensualidad para la placa {FormPlateNumber.ToUpperInvariant()} ha sido renovada hasta {FormEndDate:dd/MM/yyyy}.");
            }
            else
            {
                var newSub = new MonthlySubscription
                {
                    SubscriptionId = Guid.NewGuid(),
                    CustomerName = FormCustomerName.Trim(),
                    CustomerDocument = FormCustomerDocument.Trim(),
                    CustomerPhone = FormCustomerPhone.Trim(),
                    CustomerEmail = string.IsNullOrWhiteSpace(FormCustomerEmail) ? null : FormCustomerEmail.Trim(),
                    PlateNumber = FormPlateNumber.Trim().ToUpperInvariant(),
                    VehicleType = FormVehicleType,
                    StartDateUtc = FormStartDate.ToUniversalTime(),
                    EndDateUtc = FormEndDate.ToUniversalTime(),
                    MonthlyFee = FormMonthlyFee,
                    AmountPaid = FormAmountPaid,
                    PaymentMethod = FormPaymentMethod,
                    IsActive = true,
                    Notes = string.IsNullOrWhiteSpace(FormNotes) ? null : FormNotes.Trim(),
                    CreatedAtUtc = DateTime.UtcNow
                };

                await _subscriptionService.CreateSubscriptionAsync(newSub);

                await _dialogService.ShowAlertAsync(
                    "Mensualidad Registrada",
                    $"Suscripción mensual creada para {newSub.CustomerName} ({newSub.PlateNumber}). Vigencia hasta {FormEndDate:dd/MM/yyyy}.");
            }

            IsFormOpen = false;
            await LoadSubscriptionsAsync();
        }
        catch (Exception ex)
        {
            FormErrorMessage = $"Error al guardar: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task CancelSubscriptionAsync(MonthlySubscription subscription)
    {
        if (subscription == null) return;
        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Cancelar Mensualidad",
            $"¿Está seguro de cancelar la mensualidad del vehículo {subscription.PlateNumber} ({subscription.CustomerName})?");

        if (confirmed)
        {
            await _subscriptionService.CancelSubscriptionAsync(subscription.SubscriptionId);
            await LoadSubscriptionsAsync();
        }
    }
}
