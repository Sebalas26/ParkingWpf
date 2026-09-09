using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.UnitTests.Common;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class CheckOutViewModelTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<IParkingTicketService> _mockTicketService;
    private readonly Mock<IPricingCalculatorService> _mockPricingCalculator;
    private readonly Mock<IMonthlySubscriptionService> _mockSubscriptionService;
    private readonly Mock<IStoreService> _mockStoreService;
    private readonly Mock<IAgreementService> _mockAgreementService;
    private readonly Mock<IBillingResolutionService> _mockBillingResolutionService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly BranchModel _testBranchModel;

    public CheckOutViewModelTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockTicketService = new Mock<IParkingTicketService>();
        _mockPricingCalculator = new Mock<IPricingCalculatorService>();
        _mockSubscriptionService = new Mock<IMonthlySubscriptionService>();
        _mockStoreService = new Mock<IStoreService>();
        _mockStoreService.Setup(s => s.GetActiveStoresAsync()).ReturnsAsync(new List<Store>());

        _mockAgreementService = new Mock<IAgreementService>();
        _mockAgreementService.Setup(a => a.GetAllAgreementsAsync()).ReturnsAsync(new List<CommercialAgreement>());
        _mockAgreementService.Setup(a => a.GetAgreementsByStoreAsync(It.IsAny<Guid>())).ReturnsAsync(new List<CommercialAgreement>());

        _mockBillingResolutionService = new Mock<IBillingResolutionService>();
        _mockBillingResolutionService.Setup(b => b.GetActiveResolutionsByBranchAsync(It.IsAny<int?>())).ReturnsAsync(new List<BillingResolution>());

        _mockSessionService = new Mock<ISessionService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockDialogService.Setup(d => d.ShowCheckOutDialogAsync(It.IsAny<CheckOutViewModel>())).ReturnsAsync(true);
        _mockSyncEngine = new Mock<ISyncEngineService>();

        _testBranchModel = new BranchModel
        {
            Id = 1,
            Name = "Sede Principal",
            AllowChargeByMinute = true,
            AllowChargeByHour = true,
            LostTicketFee = 15000m
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentCompanyId).Returns(10);

        using var db = _connectionManager.CreateDbContext();
        db.Branches.Add(new Branch
        {
            Id = 1,
            CompanyId = 10,
            Name = "Sede Principal",
            AllowChargeByMinute = true,
            AllowChargeByHour = true,
            LostTicketFee = 15000m
        });
        db.PaymentMethods.Add(new PaymentMethodEntity
        {
            Id = 1,
            Name = "Efectivo",
            State = true,
            RequiresCashTender = true
        });
        db.BranchPaymentMethods.Add(new BranchPaymentMethodEntity
        {
            BranchId = 1,
            PaymentMethodId = 1,
            IsActive = true
        });
        db.SaveChanges();
    }

    private CheckOutViewModel CreateViewModel()
    {
        return new CheckOutViewModel(
            _mockTicketService.Object,
            _mockPricingCalculator.Object,
            _mockSubscriptionService.Object,
            _mockStoreService.Object,
            _mockAgreementService.Object,
            _mockBillingResolutionService.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _connectionManager,
            _mockSyncEngine.Object);
    }

    [Fact]
    public async Task SelectedTicket_WhenSet_InitializesBranchRatesAndSummary()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate
            {
                VehicleType = VehicleType.Car,
                HourRate = 3000m,
                MinuteRate = 50m,
                GracePeriodMinutes = 15
            });

        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(4500m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-001",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-90),
            Status = TicketStatus.Active
        };

        // Act
        vm.SelectedTicket = ticket;
        await Task.Delay(100); // Esperar a que complete el async void OnSelectedTicketChanged

        // Assert
        vm.SelectedTicket.Should().Be(ticket);
        vm.MinuteRate.Should().Be(50m);
        vm.HourRate.Should().Be(3000m);
        vm.RateSummaryText.Should().Be("$50 / min");
        vm.LostTicketFee.Should().Be(15000m);
        vm.HasLostTicketFee.Should().BeTrue();
        vm.IsLostTicket.Should().BeFalse();
        vm.GrossFee.Should().Be(4500m);
        vm.CalculatedFee.Should().Be(4500m);
    }

    [Fact]
    public async Task IsLostTicket_Toggle_RecalculatesNetFeeWithLostTicketCharge()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, MinuteRate = 50m });

        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(3000m);

        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, true))
            .Returns(3000m + 15000m); // Con recargo de tiquete perdido

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-002",
            PlateNumber = "XYZ789",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-60),
            Status = TicketStatus.Active
        };
        vm.SelectedTicket = ticket;
        await Task.Delay(100); // Esperar inicialización del tiquete

        // Act: Activar tiquete extraviado
        vm.IsLostTicket = true;

        // Assert
        vm.GrossFee.Should().Be(18000m);
        vm.CalculatedFee.Should().Be(18000m);
    }

    [Fact]
    public void CalculateChange_WhenAmountTenderedChanges_ComputesChangeDueAccurately()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CalculatedFee = 15000m;

        // Act & Assert 1: Pago exacto
        vm.AmountTendered = 15000m;
        vm.ChangeDue.Should().Be(0m);

        // Act & Assert 2: Pago con billete de 20.000
        vm.AmountTendered = 20000m;
        vm.ChangeDue.Should().Be(5000m);

        // Act & Assert 3: Pago insuficiente (10.000)
        vm.AmountTendered = 10000m;
        vm.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public void OnSelectedPaymentMethodEntityChanged_WhenCash_AutoSelectsPosResolution_Successfully()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FVM", Name = "Facturación Electrónica" };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar" };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var cashPaymentMethod = new PaymentMethodEntity
        {
            Id = 1,
            Name = "Efectivo",
            RequiresCashTender = true
        };

        // Act
        vm.SelectedPaymentMethodEntity = cashPaymentMethod;

        // Assert
        vm.SelectedResolution.Should().NotBeNull();
        vm.SelectedResolution.Should().Be(posRes);
    }

    [Fact]
    public void OnSelectedPaymentMethodEntityChanged_WhenCard_AutoSelectsFvmResolution_Successfully()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FVM", Name = "Facturación Electrónica" };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar" };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var cardPaymentMethod = new PaymentMethodEntity
        {
            Id = 2,
            Name = "Tarjeta de Crédito",
            RequiresCashTender = false
        };

        // Act
        vm.SelectedPaymentMethodEntity = cardPaymentMethod;

        // Assert
        vm.SelectedResolution.Should().NotBeNull();
        vm.SelectedResolution.Should().Be(fvmRes);
    }

    [Fact]
    public void TicketCompleted_WhenMatchingSelectedTicket_ClearsSelectedTicketAndSetsFeedback()
    {
        // Arrange
        var vm = CreateViewModel();
        var ticketId = Guid.NewGuid();
        var selected = new ParkingTicket
        {
            TicketId = ticketId,
            PlateNumber = "REM123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };
        vm.SelectedTicket = selected;

        var completedRemote = new ParkingTicket
        {
            TicketId = ticketId,
            PlateNumber = "REM123",
            Status = TicketStatus.Completed
        };

        // Act - Simular evento TicketCompleted emitido por EfParkingTicketService ante notificación SignalR
        _mockTicketService.Raise(s => s.TicketCompleted += null, this, completedRemote);

        // Assert
        vm.SelectedTicket.Should().BeNull();
        vm.HasFeedback.Should().BeTrue();
        vm.FeedbackMessage.Should().Contain("REM123");
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
