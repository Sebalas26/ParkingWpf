using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;

namespace Parking.Views;

public partial class CustomerSelectionDialog : Window
{
    private readonly IDbConnectionManager _connectionManager;
    private readonly IApiClientService _apiClient;
    private readonly ISessionService _sessionService;
    private readonly string? _defaultPlate;

    public Customer? SelectedCustomer { get; private set; }

    public class IdTypeOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private readonly List<IdTypeOption> _idTypes = new()
    {
        new IdTypeOption { Id = 13, Name = "Cédula de Ciudadanía (CC)" },
        new IdTypeOption { Id = 31, Name = "NIT - Número Identificación Tributaria" },
        new IdTypeOption { Id = 22, Name = "Cédula de Extranjería (CE)" },
        new IdTypeOption { Id = 12, Name = "Tarjeta de Identidad (TI)" },
        new IdTypeOption { Id = 41, Name = "Pasaporte" },
        new IdTypeOption { Id = 42, Name = "Documento de Identificación Extranjero" }
    };

    public CustomerSelectionDialog(
        IDbConnectionManager connectionManager,
        IApiClientService apiClient,
        ISessionService sessionService,
        string? defaultPlate = null)
    {
        InitializeComponent();
        _connectionManager = connectionManager;
        _apiClient = apiClient;
        _sessionService = sessionService;
        _defaultPlate = defaultPlate;

        IdTypeComboBox.ItemsSource = _idTypes;
        IdTypeComboBox.SelectedIndex = 0;

        Loaded += async (s, e) =>
        {
            await LoadCustomersAsync();
            if (!string.IsNullOrWhiteSpace(_defaultPlate))
            {
                SearchBox.Text = _defaultPlate;
            }
        };
    }

    private async System.Threading.Tasks.Task LoadCustomersAsync(string? query = null)
    {
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var q = db.Customers.AsNoTracking().Where(c => c.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var clean = query.Trim();
                q = q.Where(c => c.DocumentNumber.Contains(clean) ||
                                 c.FullName.ToLower().Contains(clean.ToLower()) ||
                                 c.Vehicles.Any(v => v.PlateNumber.Contains(clean)));
            }

            var localList = await q.OrderBy(c => c.FullName).Take(25).ToListAsync();

            if (localList.Count == 0 && !string.IsNullOrWhiteSpace(query))
            {
                var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId;
                var remote = await _apiClient.GetCustomersAsync(query, companyId);
                if (remote != null && remote.Count > 0)
                {
                    localList = remote.Select(r => new Customer
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

            CustomersListBox.ItemsSource = localList;
            EmptyCustomersText.Visibility = localList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch
        {
            CustomersListBox.ItemsSource = null;
            EmptyCustomersText.Visibility = Visibility.Visible;
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = SearchBox.Text;
        SearchPlaceholder.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Visible : Visibility.Collapsed;
        await LoadCustomersAsync(text);
    }

    private void ToggleNewCustomer_Click(object sender, RoutedEventArgs e)
    {
        var isVisible = NewCustomerFormCard.Visibility == Visibility.Visible;
        NewCustomerFormCard.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
        SearchResultsContainer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        ToggleNewCustomerText.Text = isVisible ? "Nuevo Cliente" : "Buscar Existente";
        FormErrorText.Visibility = Visibility.Collapsed;
    }

    private void IdTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateNitCheckDigit();
    }

    private void DocumentNumberBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateNitCheckDigit();
    }

    private void UpdateNitCheckDigit()
    {
        if (IdTypeComboBox.SelectedValue is int id && id == 31)
        {
            CheckDigitBox.IsEnabled = true;
            CheckDigitBox.Text = CalculateNitCheckDigit(DocumentNumberBox.Text);
        }
        else
        {
            CheckDigitBox.Text = string.Empty;
            CheckDigitBox.IsEnabled = false;
        }
    }

    private static string CalculateNitCheckDigit(string nit)
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

    private async void SaveNewCustomer_Click(object sender, RoutedEventArgs e)
    {
        FormErrorText.Visibility = Visibility.Collapsed;

        var doc = DocumentNumberBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(doc) || doc.Length < 4)
        {
            FormErrorText.Text = "El número de documento es obligatorio (mínimo 4 caracteres).";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var name = FullNameBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || name.Length < 3)
        {
            FormErrorText.Text = "El nombre o razón social es obligatorio.";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var email = EmailBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.IgnoreCase))
        {
            FormErrorText.Text = "El correo electrónico es obligatorio para emitir la factura a la DIAN.";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var idType = IdTypeComboBox.SelectedValue is int val ? val : 13;
        var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId ?? 1;

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.DocumentNumber == doc);
            if (existing != null)
            {
                SetSelectedCustomer(existing);
                return;
            }

            var customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                CompanyId = companyId,
                IdentificationTypeId = idType,
                DocumentNumber = doc,
                CheckDigit = idType == 31 ? CheckDigitBox.Text?.Trim() : null,
                PersonType = idType == 31 ? "Company" : "Person",
                FullName = name,
                Email = email,
                Phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim(),
                Address = string.IsNullOrWhiteSpace(AddressBox.Text) ? null : AddressBox.Text.Trim(),
                CityCode = string.IsNullOrWhiteSpace(CityBox.Text) ? null : CityBox.Text.Trim(),
                FiscalResponsibilities = "R-99-PN",
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            if (!string.IsNullOrWhiteSpace(_defaultPlate))
            {
                customer.Vehicles.Add(new CustomerVehicle
                {
                    CustomerId = customer.CustomerId,
                    PlateNumber = _defaultPlate.Trim().ToUpperInvariant(),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }

            db.Customers.Add(customer);

            var pendingItem = new PendingSyncItem
            {
                PendingSyncItemId = Guid.NewGuid(),
                OperationType = "CreateCustomer",
                PayloadJson = JsonSerializer.Serialize(new CreateCustomerApiRequest
                {
                    CustomerId = customer.CustomerId,
                    CompanyId = customer.CompanyId,
                    IdentificationTypeId = customer.IdentificationTypeId,
                    DocumentNumber = customer.DocumentNumber,
                    CheckDigit = customer.CheckDigit,
                    PersonType = customer.PersonType,
                    FullName = customer.FullName,
                    Email = customer.Email,
                    Phone = customer.Phone,
                    Address = customer.Address,
                    CityCode = customer.CityCode,
                    FiscalResponsibilities = customer.FiscalResponsibilities,
                    InitialPlateNumber = _defaultPlate?.Trim().ToUpperInvariant()
                }),
                CreatedAtUtc = DateTime.UtcNow,
                RetryCount = 0,
                IsProcessed = false
            };

            db.PendingSyncItems.Add(pendingItem);
            await db.SaveChangesAsync();

            SetSelectedCustomer(customer);
        }
        catch (Exception ex)
        {
            FormErrorText.Text = $"Error al guardar el cliente: {ex.Message}";
            FormErrorText.Visibility = Visibility.Visible;
        }
    }

    private void SelectCustomerItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Customer customer)
        {
            SetSelectedCustomer(customer);
        }
    }

    private void CustomersListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomersListBox.SelectedItem is Customer customer)
        {
            SetSelectedCustomer(customer);
        }
    }

    private void SetSelectedCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        SelectedSummaryText.Text = $"Cliente: {customer.FullName} ({customer.DocumentNumber}) - {customer.Email}";
        ConfirmSelectionButton.IsEnabled = true;
    }

    private void ConfirmSelection_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedCustomer != null)
        {
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
