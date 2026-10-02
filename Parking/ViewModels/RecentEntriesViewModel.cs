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

[RequirePermission("monitoring.view_occupancy", "Entradas del Turno / Patio")]
public partial class RecentEntriesViewModel : ViewModelBase
{
    private readonly IParkingTicketService _ticketService;
    private readonly IDialogService _dialogService;
    private readonly IShiftService _shiftService;
    private readonly ISessionService? _sessionService;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private int _selectedTab = 0;

    public bool IsActiveTab => SelectedTab == 0;
    public bool IsCompletedTab => SelectedTab == 1;
    public bool IsHistoricalTab => SelectedTab == 2;
    public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);

    [ObservableProperty]
    private ParkingTicket? _selectedTicket;

    [ObservableProperty]
    private int _totalEntriesCount;

    [ObservableProperty]
    private int _activeTicketsCount;

    [ObservableProperty]
    private int _completedTicketsCount;

    [ObservableProperty]
    private int _historicalTicketsCount;

    [ObservableProperty]
    private DateTime _historicalDateFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime _historicalDateTo = DateTime.Today.AddDays(1).AddSeconds(-1);

    public ObservableCollection<ParkingTicket> Entries { get; } = new();
    public ObservableCollection<ParkingTicket> ActiveEntries { get; } = new();
    public ObservableCollection<ParkingTicket> CompletedEntries { get; } = new();
    public ObservableCollection<ParkingTicket> HistoricalEntries { get; } = new();

    public RecentEntriesViewModel(
        IParkingTicketService ticketService,
        IDialogService dialogService,
        IShiftService shiftService,
        ISessionService? sessionService = null)
    {
        _ticketService = ticketService;
        _dialogService = dialogService;
        _shiftService = shiftService;
        _sessionService = sessionService;

        _ticketService.TicketRegistered += (s, e) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(async () => await LoadEntriesAsync());
            }
            else
            {
                _ = LoadEntriesAsync();
            }
        };
        _ticketService.TicketCompleted += (s, e) =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(async () =>
                {
                    await LoadEntriesAsync();
                    if (SelectedTab == 2)
                    {
                        await LoadHistoricalEntriesAsync();
                    }
                });
            }
            else
            {
                _ = LoadEntriesAsync();
                if (SelectedTab == 2)
                {
                    _ = LoadHistoricalEntriesAsync();
                }
            }
        };
    }

    public override async Task InitializeAsync()
    {
        await LoadEntriesAsync();
    }

    partial void OnSearchQueryChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearchQuery));
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
        else
        {
            _ = LoadEntriesAsync();
        }
    }

    partial void OnHistoricalDateFromChanged(DateTime value)
    {
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    partial void OnHistoricalDateToChanged(DateTime value)
    {
        if (SelectedTab == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    partial void OnSelectedTabChanged(int value)
    {
        OnPropertyChanged(nameof(IsActiveTab));
        OnPropertyChanged(nameof(IsCompletedTab));
        OnPropertyChanged(nameof(IsHistoricalTab));
        Entries.Clear();
        if (value == 0)
        {
            foreach (var t in ActiveEntries) Entries.Add(t);
        }
        else if (value == 1)
        {
            foreach (var t in CompletedEntries) Entries.Add(t);
        }
        else if (value == 2)
        {
            _ = LoadHistoricalEntriesAsync();
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void SetTab(object? parameter)
    {
        if (parameter is int intVal)
        {
            SelectedTab = intVal;
        }
        else if (parameter != null && int.TryParse(parameter.ToString(), out var parsedVal))
        {
            SelectedTab = parsedVal;
        }
    }

    [RelayCommand]
    public async Task LoadEntriesAsync()
    {
        if (SelectedTab == 2)
        {
            await LoadHistoricalEntriesAsync();
            return;
        }

        IsBusy = true;
        BusyMessage = "Cargando vehículos del turno...";

        try
        {
            var activeTickets = await _ticketService.GetActiveTicketsAsync() ?? Array.Empty<ParkingTicket>();

            var shiftStart = _shiftService.CurrentShift?.StartTimeUtc ?? DateTime.UtcNow.Date;
            var completedTickets = await _ticketService.GetCompletedTicketsByShiftAsync(shiftStart) ?? Array.Empty<ParkingTicket>();

            var query = SearchQuery?.Trim() ?? string.Empty;

            var filteredActive = string.IsNullOrWhiteSpace(query)
                ? activeTickets
                : activeTickets.Where(t => t.PlateNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                           t.TicketNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            var filteredCompleted = string.IsNullOrWhiteSpace(query)
                ? completedTickets
                : completedTickets.Where(t => t.PlateNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                              t.TicketNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                              (!string.IsNullOrWhiteSpace(t.InvoiceNumber) && t.InvoiceNumber.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

            ActiveEntries.Clear();
            foreach (var ticket in filteredActive)
            {
                ActiveEntries.Add(ticket);
            }

            CompletedEntries.Clear();
            foreach (var ticket in filteredCompleted)
            {
                CompletedEntries.Add(ticket);
            }

            Entries.Clear();
            var currentList = SelectedTab == 0 ? ActiveEntries : CompletedEntries;
            foreach (var t in currentList)
            {
                Entries.Add(t);
            }

            ActiveTicketsCount = ActiveEntries.Count;
            CompletedTicketsCount = CompletedEntries.Count;
            TotalEntriesCount = ActiveTicketsCount;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    public async Task LoadHistoricalEntriesAsync()
    {
        IsBusy = true;
        BusyMessage = "Consultando histórico de facturación...";

        try
        {
            var fromUtc = HistoricalDateFrom.Date.ToUniversalTime();
            var toUtc = HistoricalDateTo.Date.AddDays(1).AddTicks(-1).ToUniversalTime();

            var tickets = await _ticketService.GetHistoricalTicketsAsync(fromUtc, toUtc, SearchQuery);

            HistoricalEntries.Clear();
            foreach (var ticket in tickets)
            {
                HistoricalEntries.Add(ticket);
            }

            HistoricalTicketsCount = HistoricalEntries.Count;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ConvertTicketToInvoiceAsync(ParkingTicket? ticket)
    {
        if (ticket == null) return;

        if (ticket.IsElectronicInvoice && !string.IsNullOrWhiteSpace(ticket.InvoiceNumber))
        {
            await _dialogService.ShowAlertAsync("Tiquete Ya Facturado", $"Este tiquete ya cuenta con la factura electrónica {ticket.InvoiceNumber}.", DialogNotificationType.Information);
            return;
        }

        var customer = await _dialogService.ShowCustomerSelectionDialogAsync(ticket.PlateNumber);
        if (customer == null) return;

        IsBusy = true;
        BusyMessage = "Convirtiendo comprobante a Factura Electrónica...";

        try
        {
            var updatedTicket = await _ticketService.ConvertTicketToInvoiceAsync(ticket.TicketId, customer.CustomerId);
            if (updatedTicket != null)
            {
                ticket.IsElectronicInvoice = updatedTicket.IsElectronicInvoice;
                ticket.InvoiceNumber = updatedTicket.InvoiceNumber;
                ticket.Customer = updatedTicket.Customer;
                ticket.CustomerId = updatedTicket.CustomerId;

                await LoadEntriesAsync();
                await LoadHistoricalEntriesAsync();

                var invNumber = !string.IsNullOrWhiteSpace(updatedTicket.InvoiceNumber)
                    ? updatedTicket.InvoiceNumber
                    : "FE-Pendiente";

                await _dialogService.ShowAlertAsync(
                    "Facturación Exitosa",
                    $"El tiquete con placa {ticket.PlateNumber} ha sido emitido como Factura Electrónica DIAN ({invNumber}).",
                    DialogNotificationType.Success);

                var wantPrint = await _dialogService.ShowConfirmationAsync(
                    "Imprimir Factura Electrónica",
                    "¿Desea visualizar e imprimir el comprobante fiscal ahora?",
                    DialogNotificationType.Question,
                    "Imprimir",
                    "No imprimir");

                if (wantPrint)
                {
                    await _dialogService.ShowReceiptPreviewAsync(updatedTicket);
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync("Aviso de Emisión", "El comprobante fue encolado para sincronización con la DIAN.", DialogNotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync("Error de Emisión", $"No se pudo emitir la factura electrónica: {ex.Message}", DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ReprintTicketAsync(ParkingTicket ticket)
    {
        if (ticket == null) return;
        await _dialogService.ShowReceiptPreviewAsync(ticket);
    }

    [RelayCommand]
    private async Task RetryInvoiceAsync(ParkingTicket? ticket)
    {
        if (ticket == null) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Reintentar Facturación Electrónica",
            $"¿Desea reintentar la emisión de factura electrónica para el comprobante #{ticket.TicketNumber} (Placa {ticket.PlateNumber})?",
            DialogNotificationType.Question,
            "Reintentar",
            "Cancelar");

        if (!confirm) return;

        IsBusy = true;
        BusyMessage = "Reintentando emisión DIAN...";

        try
        {
            var updatedTicket = await _ticketService.RetryInvoiceAsync(ticket.TicketId);
            if (updatedTicket != null)
            {
                await _dialogService.ShowAlertAsync(
                    "Emisión Encolada",
                    "El comprobante ha sido reencolado para sincronización inmediata con la DIAN.",
                    DialogNotificationType.Success);

                await LoadEntriesAsync();
                if (SelectedTab == 2)
                {
                    await LoadHistoricalEntriesAsync();
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Error de Emisión",
                    "No fue posible reintentar la emisión. Verifique la conexión o el estado del documento.",
                    DialogNotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                "Error al Reintentar",
                $"Ocurrió un error al reintentar la factura: {ex.Message}",
                DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ResendInvoiceEmailAsync(ParkingTicket? ticket)
    {
        if (ticket == null) return;

        var email = ticket.Customer?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            var customer = await _dialogService.ShowCustomerSelectionDialogAsync(ticket.PlateNumber);
            if (customer != null && !string.IsNullOrWhiteSpace(customer.Email))
            {
                email = customer.Email;
                ticket.Customer = customer;
                ticket.CustomerId = customer.CustomerId;
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Correo Requerido",
                    "Debe seleccionar o registrar un cliente con correo electrónico válido para enviar la factura.",
                    DialogNotificationType.Warning);
                return;
            }
        }

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Reenviar Factura por Correo",
            $"¿Desea reenviar la factura electrónica al correo {email}?",
            DialogNotificationType.Question,
            "Enviar",
            "Cancelar");

        if (!confirm) return;

        IsBusy = true;
        BusyMessage = $"Enviando factura a {email}...";

        try
        {
            var success = await _ticketService.ResendInvoiceEmailAsync(ticket.TicketId, email);
            if (success)
            {
                await _dialogService.ShowAlertAsync(
                    "Correo Enviado",
                    $"La factura electrónica ha sido enviada exitosamente a {email}.",
                    DialogNotificationType.Success);
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Fallo de Envío",
                    "No se pudo completar el envío del correo electrónico. Verifique la configuración de correo en Siigo.",
                    DialogNotificationType.Error);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                "Error al Enviar",
                $"Ocurrió un error al enviar el correo: {ex.Message}",
                DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task SyncDianStatusAsync(ParkingTicket? ticket)
    {
        if (ticket == null) return;

        IsBusy = true;
        BusyMessage = "Sincronizando estado con la DIAN...";

        try
        {
            var updatedTicket = await _ticketService.SyncTicketDianStatusAsync(ticket.TicketId);
            if (updatedTicket != null)
            {
                await LoadEntriesAsync();
                if (SelectedTab == 2)
                {
                    await LoadHistoricalEntriesAsync();
                }

                if (updatedTicket.DianStatus == DianStatus.Issued)
                {
                    await _dialogService.ShowAlertAsync(
                        "Estado DIAN Actualizado",
                        $"La factura {updatedTicket.InvoiceNumber} fue validada exitosamente por la DIAN.",
                        DialogNotificationType.Success);
                }
                else if (updatedTicket.DianStatus == DianStatus.Rejected)
                {
                    await _dialogService.ShowAlertAsync(
                        "Factura Rechazada",
                        $"La DIAN o Siigo rechazó la factura. Detalle: {updatedTicket.ElectronicInvoiceError ?? "Sin detalle"}",
                        DialogNotificationType.Error);
                }
                else
                {
                    await _dialogService.ShowAlertAsync(
                        "Estado DIAN",
                        "El documento continúa en procesamiento por la DIAN.",
                        DialogNotificationType.Information);
                }
            }
            else
            {
                await _dialogService.ShowAlertAsync(
                    "Error de Sincronización",
                    "No se pudo consultar el estado del comprobante. Verifique la conexión con el servidor.",
                    DialogNotificationType.Warning);
            }
        }
        catch (Exception ex)
        {
            await _dialogService.ShowAlertAsync(
                "Error al Consultar DIAN",
                $"Ocurrió un error al sincronizar el estado: {ex.Message}",
                DialogNotificationType.Error);
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
