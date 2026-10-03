using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Parking.Core.Enums;
using Parking.Core.Security;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

[RequirePermission("invoicing.customers.view", "Clientes de Facturación")]
public partial class CustomersViewModel : ViewModelBase
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IApiClientService _apiClient;
    private readonly ISessionService _sessionService;
    private readonly IDialogService _dialogService;
    private readonly IPermissionService _permissionService;

    public class IdentificationTypeItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public List<IdentificationTypeItem> IdentificationTypes { get; } = new()
    {
        new IdentificationTypeItem { Id = 1, Name = "Cédula de Ciudadanía (CC)" },
        new IdentificationTypeItem { Id = 3, Name = "NIT - Identificación Tributaria" },
        new IdentificationTypeItem { Id = 2, Name = "Cédula de Extranjería (CE)" },
        new IdentificationTypeItem { Id = 4, Name = "Pasaporte" },
        new IdentificationTypeItem { Id = 5, Name = "Documento Extranjero" }
    };

    [ObservableProperty]
    private string _searchText = string.Empty;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    [ObservableProperty]
    private Customer? _selectedCustomer;

    [ObservableProperty]
    private int _totalCustomersCount;

    [ObservableProperty]
    private bool _isFormOpen;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private Guid? _formCustomerId;

    [ObservableProperty]
    private int _formIdentificationTypeId = 1;

    [ObservableProperty]
    private string _formDocumentNumber = string.Empty;

    [ObservableProperty]
    private string? _formCheckDigit;

    [ObservableProperty]
    private string _formPersonType = "Person";

    [ObservableProperty]
    private string _formFullName = string.Empty;

    [ObservableProperty]
    private string? _formTradeName;

    [ObservableProperty]
    private string _formEmail = string.Empty;

    [ObservableProperty]
    private string? _formPhone;

    [ObservableProperty]
    private string? _formAddress;

    [ObservableProperty]
    private string? _formCityCode;

    [ObservableProperty]
    private string? _formStateCode;

    [ObservableProperty]
    private string? _formDocumentError;

    [ObservableProperty]
    private string? _formFullNameError;

    [ObservableProperty]
    private string? _formEmailError;

    [ObservableProperty]
    private string? _formAddressError;

    [ObservableProperty]
    private string? _formCityCodeError;

    [ObservableProperty]
    private string? _formGeneralError;

    public bool IsNitSelected => FormIdentificationTypeId == 3 || FormIdentificationTypeId == 31;

    public bool CanManage => _permissionService.HasPermission("invoicing.customers.manage");
    public bool CanCreate => _permissionService.HasPermission("invoicing.customers.create") || CanManage;
    public bool CanEdit => _permissionService.HasPermission("invoicing.customers.edit") || CanManage;
    public bool CanDelete => _permissionService.HasPermission("invoicing.customers.delete") || CanManage;

    public ObservableCollection<DaneMunicipality> AvailableMunicipalities { get; } = new();

    [ObservableProperty]
    private DaneMunicipality? _selectedDaneMunicipality;

    public ObservableCollection<Customer> Customers { get; } = new();

    public CustomersViewModel(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient,
        ISessionService sessionService,
        IDialogService dialogService,
        IPermissionService permissionService)
    {
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _dialogService = dialogService;
        _permissionService = permissionService;
    }

    public override async Task InitializeAsync()
    {
        await LoadMunicipalitiesAsync();
        await LoadCustomersAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchText));
        _ = LoadCustomersAsync();
    }

    partial void OnFormIdentificationTypeIdChanged(int value)
    {
        OnPropertyChanged(nameof(IsNitSelected));
        if (value == 3 || value == 31)
        {
            FormPersonType = "Company";
            if (!string.IsNullOrWhiteSpace(FormDocumentNumber))
            {
                FormCheckDigit = CalculateNitCheckDigit(FormDocumentNumber);
            }
        }
        else
        {
            FormPersonType = "Person";
            FormCheckDigit = null;
        }
    }

    partial void OnFormDocumentNumberChanged(string value)
    {
        if (FormDocumentError != null && !string.IsNullOrWhiteSpace(value))
        {
            FormDocumentError = null;
        }
        if (IsNitSelected && !string.IsNullOrWhiteSpace(value))
        {
            FormCheckDigit = CalculateNitCheckDigit(value);
        }
    }

    partial void OnFormFullNameChanged(string value)
    {
        if (FormFullNameError != null && !string.IsNullOrWhiteSpace(value))
        {
            FormFullNameError = null;
        }
    }

    partial void OnFormEmailChanged(string value)
    {
        if (FormEmailError != null && Regex.IsMatch(value?.Trim() ?? string.Empty, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            FormEmailError = null;
        }
    }

    public static string CalculateNitCheckDigit(string nit)
    {
        if (string.IsNullOrWhiteSpace(nit)) return string.Empty;
        var cleanNit = new string(nit.Where(char.IsDigit).ToArray());
        if (cleanNit.Length == 0) return string.Empty;

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

    partial void OnSelectedDaneMunicipalityChanged(DaneMunicipality? value)
    {
        if (value != null)
        {
            FormCityCode = value.Code;
            FormStateCode = value.DepartmentCode;
            FormCityCodeError = null;
        }
    }

    public async Task LoadMunicipalitiesAsync()
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
        catch (Exception ex)
        {
            App.LogException(ex, "CustomersViewModel.LoadMunicipalitiesAsync");
        }
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        IsBusy = true;
        BusyMessage = "Cargando directorio de clientes...";

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId;
            var q = db.Customers.AsNoTracking().Where(c => c.IsActive);
            if (companyId.HasValue && companyId.Value > 0)
            {
                q = q.Where(c => c.CompanyId == companyId.Value);
            }

            var query = SearchText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                q = q.Where(c => c.DocumentNumber.Contains(query) ||
                                 c.FullName.ToLower().Contains(query.ToLower()) ||
                                 c.Email.ToLower().Contains(query.ToLower()) ||
                                 (c.Phone != null && c.Phone.Contains(query)));
            }

            var list = await q.OrderBy(c => c.FullName).Take(100).ToListAsync();

            if (list.Count == 0 && !string.IsNullOrWhiteSpace(query))
            {
                var remote = await _apiClient.GetCustomersAsync(query, companyId);
                if (remote != null && remote.Count > 0)
                {
                    bool anyInsertedOrUpdated = false;
                    foreach (var r in remote)
                    {
                        var existing = await db.Customers.FirstOrDefaultAsync(c =>
                            c.CustomerId == r.CustomerId ||
                            (c.CompanyId == r.CompanyId && c.DocumentNumber == r.DocumentNumber));

                        if (existing != null)
                        {
                            existing.IdentificationTypeId = r.IdentificationTypeId;
                            existing.CheckDigit = r.CheckDigit;
                            existing.PersonType = r.PersonType;
                            existing.FullName = r.FullName;
                            existing.TradeName = r.TradeName;
                            existing.Email = r.Email;
                            existing.Phone = r.Phone;
                            existing.Address = r.Address;
                            existing.CityCode = r.CityCode;
                            existing.StateCode = r.StateCode;
                            existing.FiscalResponsibilities = r.FiscalResponsibilities;
                            if (r.SiigoCustomerId.HasValue) existing.SiigoCustomerId = r.SiigoCustomerId;
                            existing.IsActive = r.IsActive;
                            anyInsertedOrUpdated = true;
                        }
                        else
                        {
                            var newCust = new Customer
                            {
                                CustomerId = r.CustomerId != Guid.Empty ? r.CustomerId : Guid.NewGuid(),
                                CompanyId = r.CompanyId ?? companyId,
                                IdentificationTypeId = r.IdentificationTypeId,
                                DocumentNumber = r.DocumentNumber,
                                CheckDigit = r.CheckDigit,
                                PersonType = r.PersonType,
                                FullName = r.FullName,
                                TradeName = r.TradeName,
                                Email = r.Email,
                                Phone = r.Phone,
                                Address = r.Address,
                                CityCode = r.CityCode,
                                StateCode = r.StateCode,
                                FiscalResponsibilities = r.FiscalResponsibilities,
                                SiigoCustomerId = r.SiigoCustomerId,
                                IsActive = r.IsActive,
                                CreatedAtUtc = DateTime.UtcNow
                            };
                            db.Customers.Add(newCust);
                            anyInsertedOrUpdated = true;
                        }
                    }

                    if (anyInsertedOrUpdated)
                    {
                        await db.SaveChangesAsync();
                        list = await q.OrderBy(c => c.FullName).Take(100).ToListAsync();
                    }
                }
            }

            // Conciliar estado de sincronización con la cola local de SQLite
            try
            {
                var pendingItems = await db.PendingSyncItems.AsNoTracking()
                    .Where(p => p.OperationType == "CreateCustomer" && !p.IsProcessed)
                    .ToListAsync();

                if (pendingItems.Count > 0)
                {
                    var pendingCustomerIds = new HashSet<Guid>();
                    var pendingErrors = new Dictionary<Guid, string>();

                    foreach (var p in pendingItems)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(p.PayloadJson);
                            if (doc.RootElement.TryGetProperty("customerId", out var cIdProp) &&
                                Guid.TryParse(cIdProp.GetString(), out var cId))
                            {
                                pendingCustomerIds.Add(cId);
                                if (!string.IsNullOrWhiteSpace(p.LastError))
                                {
                                    pendingErrors[cId] = p.LastError;
                                }
                            }
                        }
                        catch { }
                    }

                    foreach (var item in list)
                    {
                        if (pendingCustomerIds.Contains(item.CustomerId))
                        {
                            item.IsSynchronized = false;
                            item.SyncError = pendingErrors.TryGetValue(item.CustomerId, out var err)
                                ? err
                                : "Pendiente de sincronizar con el servidor central.";
                        }
                        else
                        {
                            item.IsSynchronized = true;
                            item.SyncError = null;
                        }
                    }
                }
                else
                {
                    foreach (var item in list)
                    {
                        item.IsSynchronized = true;
                        item.SyncError = null;
                    }
                }
            }
            catch { }

            Customers.Clear();
            foreach (var item in list)
            {
                Customers.Add(item);
            }

            if (AvailableMunicipalities.Count == 0)
            {
                _ = LoadMunicipalitiesAsync();
            }

            TotalCustomersCount = Customers.Count;
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error de Consulta", $"No se pudo consultar el listado de clientes: {ex.Message}", DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [RelayCommand]
    private void OpenCreateCustomer()
    {
        if (!CanCreate)
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para registrar nuevos clientes.", DialogNotificationType.Warning);
            return;
        }

        IsEditing = false;
        FormCustomerId = null;
        FormIdentificationTypeId = 1;
        FormDocumentNumber = string.Empty;
        FormCheckDigit = null;
        FormPersonType = "Person";
        FormFullName = string.Empty;
        FormTradeName = string.Empty;
        FormEmail = string.Empty;
        FormPhone = string.Empty;
        FormAddress = string.Empty;
        if (AvailableMunicipalities.Count == 0)
        {
            _ = LoadMunicipalitiesAsync();
        }

        SelectedDaneMunicipality = null;
        FormCityCode = string.Empty;
        FormStateCode = string.Empty;

        FormDocumentError = null;
        FormFullNameError = null;
        FormEmailError = null;
        FormAddressError = null;
        FormCityCodeError = null;
        FormGeneralError = null;

        IsFormOpen = true;
    }

    [RelayCommand]
    private void OpenEditCustomer(Customer? customer)
    {
        if (customer == null) return;
        if (!CanEdit)
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para modificar clientes.", DialogNotificationType.Warning);
            return;
        }

        IsEditing = true;
        FormCustomerId = customer.CustomerId;
        FormIdentificationTypeId = customer.IdentificationTypeId switch
        {
            13 => 1,
            22 => 2,
            31 => 3,
            41 => 4,
            42 => 5,
            _ => customer.IdentificationTypeId
        };
        FormDocumentNumber = customer.DocumentNumber;
        FormCheckDigit = customer.CheckDigit;
        FormPersonType = customer.PersonType ?? (FormIdentificationTypeId == 3 ? "Company" : "Person");
        FormFullName = customer.FullName;
        FormTradeName = customer.TradeName;
        FormEmail = customer.Email;
        FormPhone = customer.Phone;
        FormAddress = customer.Address;

        if (AvailableMunicipalities.Count == 0)
        {
            _ = LoadMunicipalitiesAsync();
        }

        SelectedDaneMunicipality = AvailableMunicipalities.FirstOrDefault(m => m.Code == customer.CityCode)
            ?? AvailableMunicipalities.FirstOrDefault(m => m.MunicipalityName.Equals(customer.CityCode, StringComparison.OrdinalIgnoreCase))
            ?? AvailableMunicipalities.FirstOrDefault();
        FormCityCode = customer.CityCode;
        FormStateCode = customer.StateCode;

        FormDocumentError = null;
        FormFullNameError = null;
        FormEmailError = null;
        FormAddressError = null;
        FormCityCodeError = null;
        FormGeneralError = null;

        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        IsFormOpen = false;
        FormGeneralError = null;
    }

    private bool ValidateForm()
    {
        bool isValid = true;
        FormDocumentError = null;
        FormFullNameError = null;
        FormEmailError = null;
        FormAddressError = null;
        FormCityCodeError = null;
        FormGeneralError = null;

        var doc = FormDocumentNumber?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(doc) || doc.Length < 4)
        {
            FormDocumentError = "El número de documento es obligatorio (mínimo 4 caracteres).";
            isValid = false;
        }

        var name = FormFullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
        {
            FormFullNameError = "El nombre o razón social es obligatorio.";
            isValid = false;
        }

        var email = FormEmail?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase))
        {
            FormEmailError = "El correo electrónico es obligatorio para facturación DIAN (debe incluir dominio válido, ej: usuario@correo.com).";
            isValid = false;
        }

        var address = FormAddress?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address) || address.Length < 4)
        {
            FormAddressError = "La dirección fiscal es obligatoria para la DIAN (mínimo 4 caracteres).";
            isValid = false;
        }

        var city = SelectedDaneMunicipality?.Code ?? FormCityCode?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(city))
        {
            FormCityCodeError = "Debe seleccionar un municipio DANE.";
            isValid = false;
        }

        var phone = FormPhone?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (phone.Length < 7 || phone.Length > 10 || !phone.All(char.IsDigit))
            {
                FormGeneralError = "El teléfono debe contener entre 7 y 10 dígitos numéricos.";
                isValid = false;
            }
        }

        if (!isValid)
        {
            if (string.IsNullOrWhiteSpace(FormGeneralError))
            {
                FormGeneralError = "Por favor complete los campos obligatorios marcados en rojo.";
            }
        }

        return isValid;
    }

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (!ValidateForm()) return;

        IsBusy = true;
        BusyMessage = IsEditing ? "Actualizando cliente..." : "Registrando cliente...";

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var docClean = FormDocumentNumber.Trim();
            var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId ?? 1;
            var effectiveCityCode = SelectedDaneMunicipality?.Code ?? FormCityCode?.Trim() ?? "11001";
            var effectiveStateCode = SelectedDaneMunicipality?.DepartmentCode
                ?? (!string.IsNullOrWhiteSpace(FormStateCode) ? FormStateCode.Trim() : (effectiveCityCode.Length >= 2 ? effectiveCityCode.Substring(0, 2) : "11"));

            if (IsEditing && FormCustomerId.HasValue)
            {
                var existing = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == FormCustomerId.Value);
                if (existing != null)
                {
                    existing.IdentificationTypeId = FormIdentificationTypeId;
                    existing.DocumentNumber = docClean;
                    existing.CheckDigit = IsNitSelected ? FormCheckDigit?.Trim() : null;
                    existing.PersonType = FormPersonType;
                    existing.FullName = FormFullName.Trim();
                    existing.TradeName = string.IsNullOrWhiteSpace(FormTradeName) ? null : FormTradeName.Trim();
                    existing.Email = FormEmail.Trim();
                    existing.Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim();
                    existing.Address = FormAddress!.Trim();
                    existing.CityCode = effectiveCityCode;
                    existing.StateCode = effectiveStateCode;

                    var pendingUpdate = new PendingSyncItem
                    {
                        PendingSyncItemId = Guid.NewGuid(),
                        OperationType = "UpdateCustomer",
                        PayloadJson = JsonSerializer.Serialize(new CreateCustomerApiRequest
                        {
                            CustomerId = existing.CustomerId,
                            CompanyId = existing.CompanyId,
                            IdentificationTypeId = existing.IdentificationTypeId,
                            DocumentNumber = existing.DocumentNumber,
                            CheckDigit = existing.CheckDigit,
                            PersonType = existing.PersonType,
                            FullName = existing.FullName,
                            TradeName = existing.TradeName,
                            Email = existing.Email,
                            Phone = existing.Phone,
                            Address = existing.Address,
                            CityCode = existing.CityCode,
                            StateCode = existing.StateCode,
                            FiscalResponsibilities = existing.FiscalResponsibilities
                        }),
                        CreatedAtUtc = DateTime.UtcNow,
                        RetryCount = 0,
                        IsProcessed = false
                    };

                    db.PendingSyncItems.Add(pendingUpdate);
                    await db.SaveChangesAsync();

                    IsFormOpen = false;
                    await LoadCustomersAsync();

                    await _dialogService.ShowAlertAsync("Cliente Actualizado", $"Los datos de '{existing.FullName}' fueron actualizados correctamente.", DialogNotificationType.Success);
                }
            }
            else
            {
                var existingDoc = await db.Customers.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.DocumentNumber == docClean);
                if (existingDoc != null)
                {
                    FormDocumentError = "Ya existe un cliente registrado con este documento en esta empresa.";
                    FormGeneralError = "El documento ingresado ya se encuentra en uso.";
                    return;
                }

                var newCustomer = new Customer
                {
                    CustomerId = Guid.NewGuid(),
                    CompanyId = companyId,
                    IdentificationTypeId = FormIdentificationTypeId,
                    DocumentNumber = docClean,
                    CheckDigit = IsNitSelected ? FormCheckDigit?.Trim() : null,
                    PersonType = FormPersonType,
                    FullName = FormFullName.Trim(),
                    TradeName = string.IsNullOrWhiteSpace(FormTradeName) ? null : FormTradeName.Trim(),
                    Email = FormEmail.Trim(),
                    Phone = string.IsNullOrWhiteSpace(FormPhone) ? null : FormPhone.Trim(),
                    Address = FormAddress!.Trim(),
                    CityCode = effectiveCityCode,
                    StateCode = effectiveStateCode,
                    FiscalResponsibilities = "R-99-PN",
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                db.Customers.Add(newCustomer);

                var pending = new PendingSyncItem
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
                        TradeName = newCustomer.TradeName,
                        Email = newCustomer.Email,
                        Phone = newCustomer.Phone,
                        Address = newCustomer.Address,
                        CityCode = newCustomer.CityCode,
                        StateCode = newCustomer.StateCode,
                        FiscalResponsibilities = newCustomer.FiscalResponsibilities
                    }),
                    CreatedAtUtc = DateTime.UtcNow,
                    RetryCount = 0,
                    IsProcessed = false
                };

                db.PendingSyncItems.Add(pending);
                await db.SaveChangesAsync();

                try
                {
                    var apiResult = await _apiClient.CreateCustomerAsync(new CreateCustomerApiRequest
                    {
                        CustomerId = newCustomer.CustomerId,
                        CompanyId = newCustomer.CompanyId,
                        IdentificationTypeId = newCustomer.IdentificationTypeId,
                        DocumentNumber = newCustomer.DocumentNumber,
                        CheckDigit = newCustomer.CheckDigit,
                        PersonType = newCustomer.PersonType,
                        FullName = newCustomer.FullName,
                        TradeName = newCustomer.TradeName,
                        Email = newCustomer.Email,
                        Phone = newCustomer.Phone,
                        Address = newCustomer.Address,
                        CityCode = newCustomer.CityCode,
                        StateCode = newCustomer.StateCode,
                        FiscalResponsibilities = newCustomer.FiscalResponsibilities
                    });

                    if (apiResult != null)
                    {
                        pending.IsProcessed = true;
                        db.PendingSyncItems.Remove(pending);
                        await db.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    App.LogException(ex, "CustomersViewModel.SaveCustomerAsync.ImmediateSync");
                }

                IsFormOpen = false;
                await LoadCustomersAsync();

                if (pending.IsProcessed)
                {
                    await _dialogService.ShowAlertAsync("Cliente Creado", $"El cliente '{newCustomer.FullName}' ha sido registrado y sincronizado exitosamente con la nube.", DialogNotificationType.Success);
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Cliente Guardado Localmente", $"El cliente '{newCustomer.FullName}' se guardó en este equipo y se sincronizará automáticamente con la nube.", DialogNotificationType.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            FormGeneralError = $"Error al procesar el cliente: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task DeleteCustomerAsync(Customer? customer)
    {
        if (customer == null) return;
        if (!CanDelete)
        {
            await _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para inactivar o eliminar clientes.", DialogNotificationType.Warning);
            return;
        }

        var confirmed = await _dialogService.ShowConfirmationAsync(
            "Inactivar Cliente",
            $"¿Está seguro de que desea inactivar al cliente '{customer.FullName}' ({customer.DocumentNumber})?\n\nNo aparecerá en las búsquedas rápidas pero se conservará su historial de facturación.",
            DialogNotificationType.Warning,
            "Inactivar",
            "Cancelar");

        if (!confirmed) return;

        IsBusy = true;
        BusyMessage = "Inactivando cliente...";

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.CustomerId == customer.CustomerId);
            if (existing != null)
            {
                existing.IsActive = false;
                await db.SaveChangesAsync();
            }

            try
            {
                await _apiClient.DeleteCustomerAsync(customer.CustomerId);
            }
            catch { }

            await LoadCustomersAsync();
            await _dialogService.ShowAlertAsync("Cliente Inactivado", $"El cliente '{customer.FullName}' ha sido inactivado.", DialogNotificationType.Success);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error", $"No se pudo inactivar el cliente: {ex.Message}", DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task RetrySyncCustomerAsync(Customer? customer)
    {
        if (customer == null) return;

        IsBusy = true;
        BusyMessage = $"Reintentando sincronizar a {customer.FullName}...";

        try
        {
            var apiResult = await _apiClient.CreateCustomerAsync(new CreateCustomerApiRequest
            {
                CustomerId = customer.CustomerId,
                CompanyId = customer.CompanyId,
                IdentificationTypeId = customer.IdentificationTypeId,
                DocumentNumber = customer.DocumentNumber,
                CheckDigit = customer.CheckDigit,
                PersonType = customer.PersonType,
                FullName = customer.FullName,
                TradeName = customer.TradeName,
                Email = customer.Email,
                Phone = customer.Phone,
                Address = customer.Address,
                CityCode = customer.CityCode,
                StateCode = customer.StateCode,
                FiscalResponsibilities = customer.FiscalResponsibilities
            });

            if (apiResult != null)
            {
                using var db = _connectionManager.CreateDbContext();
                var pendingList = await db.PendingSyncItems
                    .Where(p => p.OperationType == "CreateCustomer" && !p.IsProcessed)
                    .ToListAsync();

                var match = pendingList.FirstOrDefault(p =>
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(p.PayloadJson);
                        return doc.RootElement.TryGetProperty("customerId", out var cIdProp) &&
                               Guid.TryParse(cIdProp.GetString(), out var cId) &&
                               cId == customer.CustomerId;
                    }
                    catch { return false; }
                });

                if (match != null)
                {
                    db.PendingSyncItems.Remove(match);
                    await db.SaveChangesAsync();
                }

                await LoadCustomersAsync();
                await _dialogService.ShowAlertAsync("Sincronización Exitosa", $"El cliente '{customer.FullName}' se ha sincronizado exitosamente con la nube.", DialogNotificationType.Success);
            }
            else
            {
                await LoadCustomersAsync();
                await _dialogService.ShowAlertAsync("Sincronización Pendiente", "No se pudo sincronizar con la nube en este momento. Verifique la conexión o el firewall de la red.", DialogNotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            App.LogException(ex, "CustomersViewModel.RetrySyncCustomerAsync");
            await LoadCustomersAsync();
            await _dialogService.ShowAlertAsync("Fallo de Sincronización", ex.Message, DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
