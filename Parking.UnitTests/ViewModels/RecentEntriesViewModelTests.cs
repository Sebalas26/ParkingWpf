using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class RecentEntriesViewModelTests
{
    private readonly Mock<IParkingTicketService> _mockTicketService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IShiftService> _mockShiftService;

    public RecentEntriesViewModelTests()
    {
        _mockTicketService = new Mock<IParkingTicketService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockShiftService = new Mock<IShiftService>();
    }

    [Fact]
    public void SetTabCommand_WithStringParameter_UpdatesSelectedTabWithoutThrowing()
    {
        // Arrange
        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);

        // Act - Simulate XAML CommandParameter="1"
        vm.SetTabCommand.Execute("1");

        // Assert
        vm.SelectedTab.Should().Be(1);
        vm.IsActiveTab.Should().BeFalse();
        vm.IsCompletedTab.Should().BeTrue();

        // Act - Switch back with CommandParameter="0"
        vm.SetTabCommand.Execute("0");

        // Assert
        vm.SelectedTab.Should().Be(0);
        vm.IsActiveTab.Should().BeTrue();
        vm.IsCompletedTab.Should().BeFalse();
    }

    [Fact]
    public void SetTabCommand_WithIntParameter_UpdatesSelectedTab()
    {
        // Arrange
        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);

        // Act
        vm.SetTabCommand.Execute(1);

        // Assert
        vm.SelectedTab.Should().Be(1);

        // Act
        vm.SetTabCommand.Execute(0);

        // Assert
        vm.SelectedTab.Should().Be(0);
    }

    [Fact]
    public void SetTabCommand_WithInvalidOrNullParameter_DoesNotThrow()
    {
        // Arrange
        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        var initialTab = vm.SelectedTab;

        // Act & Assert
        var actionNull = () => vm.SetTabCommand.Execute(null);
        actionNull.Should().NotThrow();
        vm.SelectedTab.Should().Be(initialTab);

        var actionInvalid = () => vm.SetTabCommand.Execute("not-a-number");
        actionInvalid.Should().NotThrow();
        vm.SelectedTab.Should().Be(initialTab);
    }

    [Fact]
    public async Task LoadEntriesAsync_PopulatesActiveAndCompletedEntries()
    {
        // Arrange
        var active = new List<ParkingTicket>
        {
            new() { TicketId = Guid.NewGuid(), PlateNumber = "ABC123", TicketNumber = "T-001", VehicleType = VehicleType.Car },
            new() { TicketId = Guid.NewGuid(), PlateNumber = "XYZ789", TicketNumber = "T-002", VehicleType = VehicleType.Motorcycle }
        };
        var completed = new List<ParkingTicket>
        {
            new() { TicketId = Guid.NewGuid(), PlateNumber = "SAL001", TicketNumber = "T-999", InvoiceNumber = "FE-101", VehicleType = VehicleType.Car }
        };

        _mockTicketService.Setup(s => s.GetActiveTicketsAsync()).ReturnsAsync(active);
        _mockTicketService.Setup(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>())).ReturnsAsync(completed);

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.ActiveEntries.Should().HaveCount(2);
        vm.CompletedEntries.Should().HaveCount(1);
        vm.ActiveTicketsCount.Should().Be(2);
        vm.CompletedTicketsCount.Should().Be(1);
        vm.Entries.Should().HaveCount(2); // Pestaña Activos por defecto

        // Cambiar a pestaña Completados
        vm.SetTabCommand.Execute("1");
        vm.Entries.Should().HaveCount(1);
        vm.Entries[0].PlateNumber.Should().Be("SAL001");
    }

    [Fact]
    public void ClearSearchCommand_ResetsSearchQueryAndHasSearchQueryFlag()
    {
        // Arrange
        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object)
        {
            SearchQuery = "ABC"
        };
        vm.HasSearchQuery.Should().BeTrue();

        // Act
        vm.ClearSearchCommand.Execute(null);

        // Assert
        vm.SearchQuery.Should().BeEmpty();
        vm.HasSearchQuery.Should().BeFalse();
    }

    [Fact]
    public async Task SetTabCommand_WithParameterTwo_SwitchesToHistoricalTabAndLoadsEntries()
    {
        var historical = new List<ParkingTicket>
        {
            new ParkingTicket { TicketId = Guid.NewGuid(), PlateNumber = "FE123", IsElectronicInvoice = true, InvoiceNumber = "FE-100" }
        };
        _mockTicketService.Setup(s => s.GetHistoricalTicketsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(historical);

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        vm.SetTabCommand.Execute("2");

        vm.SelectedTab.Should().Be(2);
        vm.IsActiveTab.Should().BeFalse();
        vm.IsCompletedTab.Should().BeFalse();
        vm.IsHistoricalTab.Should().BeTrue();

        await Task.Delay(50);
        vm.HistoricalEntries.Should().HaveCount(1);
        vm.HistoricalTicketsCount.Should().Be(1);
    }

    [Fact]
    public async Task ConvertTicketToInvoiceCommand_WhenTicketAlreadyInvoiced_DoesNotOpenDialog()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-999"
        };

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        await vm.ConvertTicketToInvoiceCommand.ExecuteAsync(ticket);

        _mockDialogService.Verify(d => d.ShowCustomerSelectionDialogAsync(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ConvertTicketToInvoiceCommand_WhenCustomerSelected_CallsServiceAndUpdates()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = false
        };

        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            FullName = "Cliente Prueba",
            DocumentNumber = "12345678"
        };

        var updated = new ParkingTicket
        {
            TicketId = ticket.TicketId,
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-101"
        };

        _mockDialogService.Setup(d => d.ShowCustomerSelectionDialogAsync("ABC123")).ReturnsAsync(customer);
        _mockTicketService.Setup(s => s.ConvertTicketToInvoiceAsync(ticket.TicketId, customer.CustomerId)).ReturnsAsync(updated);
        _mockTicketService.Setup(s => s.GetHistoricalTicketsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>()))
            .ReturnsAsync(new List<ParkingTicket> { updated });

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        vm.CanConvertTicketsToInvoice = true;
        await vm.ConvertTicketToInvoiceCommand.ExecuteAsync(ticket);

        _mockTicketService.Verify(s => s.ConvertTicketToInvoiceAsync(ticket.TicketId, customer.CustomerId), Times.Once);
        ticket.IsElectronicInvoice.Should().BeTrue();
        ticket.InvoiceNumber.Should().Be("FE-101");
        _mockTicketService.Verify(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task LoadEntriesAsync_WhenOnline_PerformsSyncBeforeLoading()
    {
        // Arrange
        var mockSyncEngine = new Mock<ISyncEngineService>();
        mockSyncEngine.Setup(s => s.IsOnline).Returns(true);
        mockSyncEngine.Setup(s => s.PerformFullSyncAsync()).ReturnsAsync(true);

        _mockTicketService.Setup(s => s.GetActiveTicketsAsync()).ReturnsAsync(new List<ParkingTicket>());
        _mockTicketService.Setup(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>())).ReturnsAsync(new List<ParkingTicket>());

        var vm = new RecentEntriesViewModel(
            _mockTicketService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            sessionService: null,
            syncEngine: mockSyncEngine.Object);

        // Act
        await vm.LoadEntriesCommand.ExecuteAsync(null);

        // Assert
        mockSyncEngine.Verify(s => s.PerformFullSyncAsync(), Times.Once);
        _mockTicketService.Verify(s => s.GetActiveTicketsAsync(), Times.Once);
        _mockTicketService.Verify(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Once);
    }

    [Fact]
    public async Task LoadEntriesAsync_WhenOffline_DoesNotPerformRemoteSync()
    {
        // Arrange
        var mockSyncEngine = new Mock<ISyncEngineService>();
        mockSyncEngine.Setup(s => s.IsOnline).Returns(false);

        _mockTicketService.Setup(s => s.GetActiveTicketsAsync()).ReturnsAsync(new List<ParkingTicket>());
        _mockTicketService.Setup(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>())).ReturnsAsync(new List<ParkingTicket>());

        var vm = new RecentEntriesViewModel(
            _mockTicketService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            sessionService: null,
            syncEngine: mockSyncEngine.Object);

        // Act
        await vm.LoadEntriesCommand.ExecuteAsync(null);

        // Assert
        mockSyncEngine.Verify(s => s.PerformFullSyncAsync(), Times.Never);
        _mockTicketService.Verify(s => s.GetActiveTicketsAsync(), Times.Once);
    }

    [Fact]
    public async Task ResendInvoiceEmailCommand_WithValidEmail_SendsSuccessfully()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-001",
            Customer = new Customer { CustomerId = Guid.NewGuid(), Email = "cliente@correo.com", FullName = "Test" }
        };

        _mockDialogService.Setup(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogNotificationType>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.ResendInvoiceEmailAsync(ticket.TicketId, "cliente@correo.com"))
            .ReturnsAsync((string?)null);

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        await vm.ResendInvoiceEmailCommand.ExecuteAsync(ticket);

        _mockTicketService.Verify(s => s.ResendInvoiceEmailAsync(ticket.TicketId, "cliente@correo.com"), Times.Once);
        _mockDialogService.Verify(d => d.ShowAlertAsync("Correo Enviado", It.IsAny<string>(), DialogNotificationType.Success), Times.Once);
    }

    [Fact]
    public async Task ResendInvoiceEmailCommand_WithoutCustomer_OpensSelectionDialog()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-001",
            Customer = null
        };

        var newCustomer = new Customer { CustomerId = Guid.NewGuid(), Email = "nuevo@correo.com", FullName = "Nuevo" };

        _mockDialogService.Setup(d => d.ShowCustomerSelectionDialogAsync("ABC123")).ReturnsAsync(newCustomer);
        _mockDialogService.Setup(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogNotificationType>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.ResendInvoiceEmailAsync(ticket.TicketId, "nuevo@correo.com"))
            .ReturnsAsync((string?)null);

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        await vm.ResendInvoiceEmailCommand.ExecuteAsync(ticket);

        _mockDialogService.Verify(d => d.ShowCustomerSelectionDialogAsync("ABC123"), Times.Once);
        _mockTicketService.Verify(s => s.ResendInvoiceEmailAsync(ticket.TicketId, "nuevo@correo.com"), Times.Once);
    }

    [Fact]
    public async Task ResendInvoiceEmailCommand_WithInvalidEmailFormat_ShowsWarning()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-001",
            Customer = new Customer { CustomerId = Guid.NewGuid(), Email = "correo-invalido", FullName = "Test" }
        };

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        await vm.ResendInvoiceEmailCommand.ExecuteAsync(ticket);

        _mockDialogService.Verify(d => d.ShowAlertAsync("Correo Inválido", It.IsAny<string>(), DialogNotificationType.Warning), Times.Once);
        _mockTicketService.Verify(s => s.ResendInvoiceEmailAsync(It.IsAny<Guid>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task ResendInvoiceEmailCommand_WhenServiceReturnsError_ShowsErrorMessage()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            IsElectronicInvoice = true,
            InvoiceNumber = "FE-001",
            Customer = new Customer { CustomerId = Guid.NewGuid(), Email = "cliente@correo.com", FullName = "Test" }
        };

        _mockDialogService.Setup(d => d.ShowConfirmationAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogNotificationType>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockTicketService.Setup(s => s.ResendInvoiceEmailAsync(ticket.TicketId, "cliente@correo.com"))
            .ReturnsAsync("Solo se puede reenviar el correo de facturas previamente emitidas.");

        var vm = new RecentEntriesViewModel(_mockTicketService.Object, _mockDialogService.Object, _mockShiftService.Object);
        await vm.ResendInvoiceEmailCommand.ExecuteAsync(ticket);

        _mockDialogService.Verify(d => d.ShowAlertAsync("Fallo de Envío",
            "Solo se puede reenviar el correo de facturas previamente emitidas.", DialogNotificationType.Error), Times.Once);
    }

    [Fact]
    public async Task UpdateCanConvertTicketsToInvoiceAsync_WhenElectronicResolutionExists_SetsCanConvertTrue()
    {
        var mockResolutionService = new Mock<IBillingResolutionService>();
        mockResolutionService.Setup(r => r.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution>
            {
                new BillingResolution { IsActive = true, IsElectronicResolution = true }
            });

        var vm = new RecentEntriesViewModel(
            _mockTicketService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            billingResolutionService: mockResolutionService.Object);

        await vm.UpdateCanConvertTicketsToInvoiceAsync();

        vm.CanConvertTicketsToInvoice.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCanConvertTicketsToInvoiceAsync_WhenNoElectronicResolutionExists_SetsCanConvertFalse()
    {
        var mockResolutionService = new Mock<IBillingResolutionService>();
        mockResolutionService.Setup(r => r.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution>
            {
                new BillingResolution { IsActive = true, IsElectronicResolution = false }
            });

        var vm = new RecentEntriesViewModel(
            _mockTicketService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            billingResolutionService: mockResolutionService.Object);

        await vm.UpdateCanConvertTicketsToInvoiceAsync();

        vm.CanConvertTicketsToInvoice.Should().BeFalse();
    }

    [Fact]
    public async Task ConvertTicketToInvoiceCommand_WhenCanConvertIsFalse_ShowsWarningAndDoesNotCallCustomerDialog()
    {
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "XYZ789",
            IsElectronicInvoice = false
        };

        var mockResolutionService = new Mock<IBillingResolutionService>();
        mockResolutionService.Setup(r => r.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution>());

        var vm = new RecentEntriesViewModel(
            _mockTicketService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            billingResolutionService: mockResolutionService.Object);

        await vm.UpdateCanConvertTicketsToInvoiceAsync();

        await vm.ConvertTicketToInvoiceCommand.ExecuteAsync(ticket);

        _mockDialogService.Verify(d => d.ShowAlertAsync(
            "Facturación Electrónica no disponible",
            It.IsAny<string>(),
            DialogNotificationType.Warning), Times.Once);

        _mockDialogService.Verify(d => d.ShowCustomerSelectionDialogAsync(It.IsAny<string>()), Times.Never);
        _mockTicketService.Verify(s => s.ConvertTicketToInvoiceAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
    }
}

