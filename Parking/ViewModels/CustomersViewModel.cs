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
        new IdentificationTypeItem { Id = 13, Name = "Cédula de Ciudadanía (CC)" },
        new IdentificationTypeItem { Id = 31, Name = "NIT - Identificación Tributaria" },
        new IdentificationTypeItem { Id = 22, Name = "Cédula de Extranjería (CE)" },
        new IdentificationTypeItem { Id = 12, Name = "Tarjeta de Identidad (TI)" },
        new IdentificationTypeItem { Id = 41, Name = "Pasaporte" },
        new IdentificationTypeItem { Id = 42, Name = "Documento Extranjero" }
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
    private int _formIdentificationTypeId = 13;

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
    private string? _formGeneralError;

    public bool IsNitSelected => FormIdentificationTypeId == 31;

    public bool CanManage => _permissionService.HasPermission("invoicing.customers.manage");
    public bool CanDelete => _permissionService.HasPermission("invoicing.customers.delete") || _permissionService.HasPermission("invoicing.customers.manage");

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
        if (value == 31)
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

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        IsBusy = true;
        BusyMessage = "Cargando directorio de clientes...";

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var q = db.Customers.AsNoTracking().Where(c => c.IsActive);

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
                var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId;
                var remote = await _apiClient.GetCustomersAsync(query, companyId);
                if (remote != null && remote.Count > 0)
                {
                    list = remote.Select(r => new Customer
                    {
                        CustomerId = r.CustomerId,
                        CompanyId = r.CompanyId,
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
                        IsActive = r.IsActive
                    }).ToList();
                }
            }

            Customers.Clear();
            foreach (var item in list)
            {
                Customers.Add(item);
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
        if (!CanManage)
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para registrar nuevos clientes.", DialogNotificationType.Warning);
            return;
        }

        IsEditing = false;
        FormCustomerId = null;
        FormIdentificationTypeId = 13;
        FormDocumentNumber = string.Empty;
        FormCheckDigit = null;
        FormPersonType = "Person";
        FormFullName = string.Empty;
        FormTradeName = string.Empty;
        FormEmail = string.Empty;
        FormPhone = string.Empty;
        FormAddress = string.Empty;
        FormCityCode = string.Empty;
        FormStateCode = string.Empty;

        FormDocumentError = null;
        FormFullNameError = null;
        FormEmailError = null;
        FormGeneralError = null;

        IsFormOpen = true;
    }

    [RelayCommand]
    private void OpenEditCustomer(Customer? customer)
    {
        if (customer == null) return;
        if (!CanManage)
        {
            _ = _dialogService.ShowAlertAsync("Acceso Denegado", "No tienes permisos para modificar clientes.", DialogNotificationType.Warning);
            return;
        }

        IsEditing = true;
        FormCustomerId = customer.CustomerId;
        FormIdentificationTypeId = customer.IdentificationTypeId;
        FormDocumentNumber = customer.DocumentNumber;
        FormCheckDigit = customer.CheckDigit;
        FormPersonType = customer.PersonType ?? (customer.IdentificationTypeId == 31 ? "Company" : "Person");
        FormFullName = customer.FullName;
        FormTradeName = customer.TradeName;
        FormEmail = customer.Email;
        FormPhone = customer.Phone;
        FormAddress = customer.Address;
        FormCityCode = customer.CityCode;
        FormStateCode = customer.StateCode;

        FormDocumentError = null;
        FormFullNameError = null;
        FormEmailError = null;
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
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            FormEmailError = "El correo electrónico es obligatorio para facturación DIAN.";
            isValid = false;
        }

        if (!isValid)
        {
            FormGeneralError = "Por favor complete los campos obligatorios marcados en rojo.";
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
                    existing.Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim();
                    existing.CityCode = string.IsNullOrWhiteSpace(FormCityCode) ? null : FormCityCode.Trim();
                    existing.StateCode = string.IsNullOrWhiteSpace(FormStateCode) ? null : FormStateCode.Trim();

                    await db.SaveChangesAsync();

                    try
                    {
                        await _apiClient.UpdateCustomerAsync(existing.CustomerId, new CreateCustomerApiRequest
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
                        });
                    }
                    catch { }

                    IsFormOpen = false;
                    await LoadCustomersAsync();
                    await _dialogService.ShowAlertAsync("Cliente Actualizado", $"Los datos de '{existing.FullName}' fueron actualizados correctamente.", DialogNotificationType.Success);
                }
            }
            else
            {
                var existingDoc = await db.Customers.FirstOrDefaultAsync(c => c.DocumentNumber == docClean);
                if (existingDoc != null)
                {
                    FormDocumentError = "Ya existe un cliente registrado con este documento.";
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
                    Address = string.IsNullOrWhiteSpace(FormAddress) ? null : FormAddress.Trim(),
                    CityCode = string.IsNullOrWhiteSpace(FormCityCode) ? null : FormCityCode.Trim(),
                    StateCode = string.IsNullOrWhiteSpace(FormStateCode) ? null : FormStateCode.Trim(),
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
                    await _apiClient.CreateCustomerAsync(new CreateCustomerApiRequest
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
                }
                catch { }

                IsFormOpen = false;
                await LoadCustomersAsync();
                await _dialogService.ShowAlertAsync("Cliente Creado", $"El cliente '{newCustomer.FullName}' ha sido registrado exitosamente.", DialogNotificationType.Success);
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
}
