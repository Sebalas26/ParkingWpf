using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class AgreementSettingsViewModel : ViewModelBase
{
    private readonly IAgreementService _agreementService;
    private readonly IStoreService _storeService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private Store? _selectedStore;

    [ObservableProperty]
    private decimal _minPurchaseAmount = 20000m;

    [ObservableProperty]
    private DiscountType _discountType = DiscountType.Percentage;

    [ObservableProperty]
    private decimal _discountValue = 20m;

    [ObservableProperty]
    private int? _maxHoursApplicable;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private CommercialAgreement? _selectedAgreement;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    public ObservableCollection<CommercialAgreement> Agreements { get; } = new();
    public ObservableCollection<Store> AvailableStores { get; } = new();

    public AgreementSettingsViewModel(IAgreementService agreementService, IStoreService storeService, IDialogService dialogService)
    {
        _agreementService = agreementService;
        _storeService = storeService;
        _dialogService = dialogService;
    }

    public override async Task InitializeAsync()
    {
        await LoadStoresAsync();
        await LoadAgreementsAsync();
    }

    private async Task LoadStoresAsync()
    {
        var stores = await _storeService.GetActiveStoresAsync();
        AvailableStores.Clear();
        foreach (var s in stores)
        {
            AvailableStores.Add(s);
        }

        if (AvailableStores.Count > 0 && SelectedStore == null)
        {
            SelectedStore = AvailableStores[0];
        }
    }

    private async Task LoadAgreementsAsync()
    {
        var items = await _agreementService.GetAllAgreementsAsync();
        Agreements.Clear();
        foreach (var item in items)
        {
            Agreements.Add(item);
        }
    }

    partial void OnSelectedAgreementChanged(CommercialAgreement? value)
    {
        if (value != null)
        {
            IsEditing = true;
            Name = value.Name;
            SelectedStore = AvailableStores.FirstOrDefault(s => s.StoreId == value.StoreId);
            MinPurchaseAmount = value.MinPurchaseAmount;

            if (value.DiscountPercentage.HasValue)
            {
                DiscountType = DiscountType.Percentage;
                DiscountValue = value.DiscountPercentage.Value;
            }
            else
            {
                DiscountType = DiscountType.FixedAmount;
                DiscountValue = value.DiscountFixedAmount ?? 0m;
            }

            MaxHoursApplicable = value.MaxHoursApplicable;
            IsActive = value.IsActive;
        }
        else
        {
            ClearForm();
        }
    }

    [RelayCommand]
    private async Task SaveAgreementAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Por favor ingrese el nombre del convenio.";
            return;
        }

        if (SelectedStore == null)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Debe seleccionar un almacén asociado.";
            return;
        }

        if (MinPurchaseAmount <= 0)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "El valor mínimo de compra debe ser mayor a cero.";
            return;
        }

        if (DiscountValue <= 0)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "El valor o porcentaje de descuento debe ser mayor a cero.";
            return;
        }

        decimal? percent = DiscountType == DiscountType.Percentage ? DiscountValue : null;
        decimal? fixedAmount = DiscountType == DiscountType.FixedAmount ? DiscountValue : null;

        IsBusy = true;
        BusyMessage = "Guardando regla de convenio...";

        try
        {
            if (IsEditing && SelectedAgreement != null)
            {
                await _agreementService.UpdateAgreementAsync(
                    SelectedAgreement.AgreementId,
                    Name,
                    MinPurchaseAmount,
                    percent,
                    fixedAmount,
                    MaxHoursApplicable,
                    IsActive);

                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Convenio '{Name}' actualizado exitosamente.";
            }
            else
            {
                var agreement = await _agreementService.CreateAgreementAsync(
                    SelectedStore.StoreId,
                    Name,
                    MinPurchaseAmount,
                    percent,
                    fixedAmount,
                    MaxHoursApplicable);

                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Convenio '{agreement.Name}' registrado exitosamente.";
            }

            ClearForm();
            await LoadAgreementsAsync();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al guardar convenio: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedAgreement = null;
        IsEditing = false;
        Name = string.Empty;
        MinPurchaseAmount = 20000m;
        DiscountType = DiscountType.Percentage;
        DiscountValue = 20m;
        MaxHoursApplicable = null;
        IsActive = true;
    }
}
