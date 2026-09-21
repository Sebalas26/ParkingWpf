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
        await vm.ConvertTicketToInvoiceCommand.ExecuteAsync(ticket);

        _mockTicketService.Verify(s => s.ConvertTicketToInvoiceAsync(ticket.TicketId, customer.CustomerId), Times.Once);
        ticket.IsElectronicInvoice.Should().BeTrue();
        ticket.InvoiceNumber.Should().Be("FE-101");
        _mockTicketService.Verify(s => s.GetCompletedTicketsByShiftAsync(It.IsAny<DateTime>(), It.IsAny<int?>()), Times.Once);
    }
}
