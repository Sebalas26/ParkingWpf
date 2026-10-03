using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        new IdTypeOption { Id = 1, Name = "Cédula de Ciudadanía (CC)" },
        new IdTypeOption { Id = 3, Name = "NIT - Número Identificación Tributaria" },
        new IdTypeOption { Id = 2, Name = "Cédula de Extranjería (CE)" },
        new IdTypeOption { Id = 4, Name = "Pasaporte" },
        new IdTypeOption { Id = 5, Name = "Documento de Identificación Extranjero" }
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
            if (Owner != null)
            {
                if (Owner.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
                else
                {
                    this.WindowState = WindowState.Normal;
                    this.Left = Owner.Left;
                    this.Top = Owner.Top;
                    this.Width = Owner.ActualWidth;
                    this.Height = Owner.ActualHeight;
                }
            }
            else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
            {
                var main = Application.Current.MainWindow;
                if (main.WindowState == WindowState.Maximized)
                {
                    this.WindowState = WindowState.Maximized;
                }
                else
                {
                    this.WindowState = WindowState.Normal;
                    this.Left = main.Left;
                    this.Top = main.Top;
                    this.Width = main.ActualWidth;
                    this.Height = main.ActualHeight;
                }
            }

            await LoadCustomersAsync();
        };
    }

    private async System.Threading.Tasks.Task LoadCustomersAsync(string? query = null)
    {
        try
        {
            using var db = _connectionManager.CreateDbContext();
            var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId;
            var q = db.Customers.AsNoTracking().Where(c => c.IsActive);
            if (companyId.HasValue && companyId.Value > 0)
            {
                q = q.Where(c => c.CompanyId == companyId.Value);
            }

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
                        localList = await q.OrderBy(c => c.FullName).Take(25).ToListAsync();
                    }
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
        if (IdTypeComboBox.SelectedValue is int id && (id == 3 || id == 31))
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
        if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.IgnoreCase))
        {
            FormErrorText.Text = "El correo electrónico es obligatorio para emitir la factura a la DIAN (debe incluir dominio válido, ej: cliente@correo.com).";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var address = AddressBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(address) || address.Length < 4)
        {
            FormErrorText.Text = "La dirección fiscal es obligatoria para la DIAN (mínimo 4 caracteres).";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var city = CityBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(city) || city.Length < 4)
        {
            FormErrorText.Text = "El código DANE del municipio es obligatorio (ej: 11001).";
            FormErrorText.Visibility = Visibility.Visible;
            return;
        }

        var rawIdType = IdTypeComboBox.SelectedValue is int val ? val : 1;
        var idType = rawIdType switch
        {
            13 => 1,
            22 => 2,
            31 => 3,
            41 => 4,
            42 => 5,
            _ => rawIdType
        };
        var companyId = _sessionService.CurrentBranch?.CompanyId ?? _sessionService.CurrentUser?.CompanyId ?? 1;

        try
        {
            using var db = _connectionManager.CreateDbContext();
            var existing = await db.Customers.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.DocumentNumber == doc);
            if (existing != null)
            {
                existing.IdentificationTypeId = idType;
                existing.CheckDigit = (idType == 3 || idType == 31) ? CheckDigitBox.Text?.Trim() : null;
                existing.PersonType = (idType == 3 || idType == 31) ? "Company" : "Person";
                existing.FullName = name;
                existing.Email = email;
                existing.Phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim();
                existing.Address = address;
                existing.CityCode = city;
                existing.FiscalResponsibilities = "R-99-PN";
                existing.IsActive = true;

                if (!string.IsNullOrWhiteSpace(_defaultPlate))
                {
                    var cleanPlate = _defaultPlate.Trim().ToUpperInvariant();
                    var hasPlate = await db.CustomerVehicles
                        .AnyAsync(v => v.CustomerId == existing.CustomerId && v.PlateNumber == cleanPlate);

                    if (!hasPlate)
                    {
                        db.CustomerVehicles.Add(new CustomerVehicle
                        {
                            CustomerId = existing.CustomerId,
                            PlateNumber = cleanPlate,
                            CreatedAtUtc = DateTime.UtcNow
                        });
                    }
                }

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
                        FiscalResponsibilities = existing.FiscalResponsibilities,
                        InitialPlateNumber = _defaultPlate?.Trim().ToUpperInvariant()
                    }),
                    CreatedAtUtc = DateTime.UtcNow,
                    RetryCount = 0,
                    IsProcessed = false
                };

                db.PendingSyncItems.Add(pendingUpdate);
                await db.SaveChangesAsync();

                SetSelectedCustomer(existing);
                return;
            }

            var customer = new Customer
            {
                CustomerId = Guid.NewGuid(),
                CompanyId = companyId,
                IdentificationTypeId = idType,
                DocumentNumber = doc,
                CheckDigit = (idType == 3 || idType == 31) ? CheckDigitBox.Text?.Trim() : null,
                PersonType = (idType == 3 || idType == 31) ? "Company" : "Person",
                FullName = name,
                Email = email,
                Phone = string.IsNullOrWhiteSpace(PhoneBox.Text) ? null : PhoneBox.Text.Trim(),
                Address = address,
                CityCode = city,
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

    private void Backdrop_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource == sender)
        {
            DialogResult = false;
            Close();
        }
    }

    private void ModalContent_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
            Close();
        }
    }
}
