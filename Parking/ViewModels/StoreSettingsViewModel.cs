using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class StoreSettingsViewModel : ViewModelBase
{
    private readonly IStoreService _storeService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _taxId = string.Empty;

    [ObservableProperty]
    private string? _phoneNumber;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private Store? _selectedStore;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    public ObservableCollection<Store> Stores { get; } = new();

    public StoreSettingsViewModel(IStoreService storeService, IDialogService dialogService)
    {
        _storeService = storeService;
        _dialogService = dialogService;
    }

    public override async Task InitializeAsync()
    {
        await LoadStoresAsync();
    }

    private async Task LoadStoresAsync()
    {
        var items = await _storeService.GetAllStoresAsync();
        Stores.Clear();
        foreach (var item in items)
        {
            Stores.Add(item);
        }
    }

    partial void OnSelectedStoreChanged(Store? value)
    {
        if (value != null)
        {
            IsEditing = true;
            Name = value.Name;
            TaxId = value.TaxId;
            PhoneNumber = value.PhoneNumber;
            IsActive = value.IsActive;
        }
        else
        {
            ClearForm();
        }
    }

    [RelayCommand]
    private async Task SaveStoreAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(Name))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Por favor ingrese el nombre del comercio aliado.";
            return;
        }

        if (string.IsNullOrWhiteSpace(TaxId))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Por favor ingrese el NIT o identificación tributaria.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Guardando información del almacén...";

        try
        {
            if (IsEditing && SelectedStore != null)
            {
                await _storeService.UpdateStoreAsync(SelectedStore.StoreId, Name, TaxId, PhoneNumber, IsActive);
                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Comercio '{Name}' actualizado exitosamente.";
            }
            else
            {
                var store = await _storeService.CreateStoreAsync(Name, TaxId, PhoneNumber);
                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Comercio '{store.Name}' registrado exitosamente.";
            }

            ClearForm();
            await LoadStoresAsync();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al guardar: {ex.Message}";
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
        SelectedStore = null;
        IsEditing = false;
        Name = string.Empty;
        TaxId = string.Empty;
        PhoneNumber = null;
        IsActive = true;
    }
}
