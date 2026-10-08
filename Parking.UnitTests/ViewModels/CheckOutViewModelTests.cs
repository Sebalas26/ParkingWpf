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
        _mockTicketService.Setup(s => s.GetActiveTicketsAsync()).ReturnsAsync(new List<ParkingTicket>());
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
            IsActive = true,
            RequiresCashTender = true
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
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FVM", Name = "Facturación Electrónica", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var cashPaymentMethod = new PaymentMethodEntity
        {
            Id = 1,
            Name = "Efectivo",
            RequiresResolution = false,
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
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FVM", Name = "Facturación Electrónica", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var cardPaymentMethod = new PaymentMethodEntity
        {
            Id = 2,
            Name = "Tarjeta de Crédito",
            RequiresResolution = true,
            RequiresCashTender = false
        };

        // Act
        vm.SelectedPaymentMethodEntity = cardPaymentMethod;

        // Assert
        vm.SelectedResolution.Should().NotBeNull();
        vm.SelectedResolution.Should().Be(fvmRes);
    }

    [Fact]
    public void OnSelectedPaymentMethodEntityChanged_WithDefaultResolutionId_SelectsConfiguredElectronicResolution()
    {
        // Arrange
        var vm = CreateViewModel();
        var feRes1 = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE1", Name = "Facturación Electrónica 1", IsElectronicResolution = true };
        var feRes2 = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE2", Name = "Facturación Electrónica 2", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(feRes1);
        vm.AvailableResolutions.Add(feRes2);
        vm.AvailableResolutions.Add(posRes);

        var customPaymentMethod = new PaymentMethodEntity
        {
            Id = 3,
            Name = "Datafono Redeban",
            RequiresResolution = true,
            DefaultResolutionId = feRes2.ResolutionId.ToString(),
            RequiresCashTender = false
        };

        // Act
        vm.SelectedPaymentMethodEntity = customPaymentMethod;

        // Assert
        vm.SelectedResolution.Should().NotBeNull();
        vm.SelectedResolution.Should().Be(feRes2);
        vm.IsResolutionLocked.Should().BeTrue();
        vm.EmitElectronicInvoice.Should().BeTrue();
    }

    [Fact]
    public void OnSelectedPaymentMethodEntityChanged_WhenRequiresCashTenderFalse_SetsAmountTenderedToCalculatedFee()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.CalculatedFee = 15000m;
        vm.AmountTendered = 5000m;

        var electronicPaymentMethod = new PaymentMethodEntity
        {
            Id = 4,
            Name = "Transferencia QR",
            RequiresCashTender = false
        };

        // Act
        vm.SelectedPaymentMethodEntity = electronicPaymentMethod;

        // Assert
        vm.AmountTendered.Should().Be(15000m);
        vm.ChangeDue.Should().Be(0m);
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

    [Fact]
    public async Task InitializeAsync_WhenElectronicInvoicingEnabled_LoadsCustomersAndSetsFlags()
    {
        // Arrange
        var userSession = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero",
            HasElectronicInvoicingEnabled = true,
            ForceElectronicInvoiceOnCheckout = false
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(userSession);

        using (var db = _connectionManager.CreateDbContext())
        {
            db.Customers.Add(new Customer
            {
                CustomerId = Guid.NewGuid(),
                DocumentNumber = "12345678",
                FullName = "Empresa Test SAS",
                Email = "test@empresa.com",
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.HasElectronicInvoicingEnabled.Should().BeTrue();
        vm.ForceElectronicInvoiceOnCheckout.Should().BeFalse();
        vm.AvailableCustomers.Should().HaveCount(1);
        vm.AvailableCustomers[0].FullName.Should().Be("Empresa Test SAS");
    }

    [Fact]
    public async Task OnSelectedTicketChanged_WhenForceElectronicInvoiceOnCheckout_ForcesEmitElectronicInvoice()
    {
        // Arrange
        var userSession = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero",
            HasElectronicInvoicingEnabled = true,
            ForceElectronicInvoiceOnCheckout = true
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(userSession);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "XYZ789",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-45)
        };

        // Act
        vm.SelectedTicket = ticket;

        // Assert
        vm.EmitElectronicInvoice.Should().BeTrue();
        vm.CanToggleElectronicInvoice.Should().BeFalse();
    }

    [Fact]
    public async Task OnSelectedTicketChanged_WhenCustomerVehicleMatchesPlate_AutoSelectsCustomer()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var userSession = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero",
            HasElectronicInvoicingEnabled = true
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(userSession);

        using (var db = _connectionManager.CreateDbContext())
        {
            var cust = new Customer
            {
                CustomerId = customerId,
                DocumentNumber = "900123456",
                FullName = "Inversiones ABC",
                Email = "contacto@abc.com",
                IsActive = true
            };
            cust.Vehicles.Add(new CustomerVehicle
            {
                CustomerId = customerId,
                PlateNumber = "ABC123"
            });
            db.Customers.Add(cust);
            await db.SaveChangesAsync();
        }

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        // Act
        vm.SelectedTicket = ticket;

        // Assert
        vm.SelectedCustomer.Should().NotBeNull();
        vm.SelectedCustomer!.CustomerId.Should().Be(customerId);
        vm.SelectedCustomer!.FullName.Should().Be("Inversiones ABC");
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenEmitElectronicInvoiceAndNoCustomerSelected_ShowsAlertAndBlocksPayment()
    {
        // Arrange
        var userSession = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero",
            HasElectronicInvoicingEnabled = true,
            ForceElectronicInvoiceOnCheckout = false
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(userSession);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "TEST01",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        vm.SelectedTicket = ticket;
        vm.SelectedResolution = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", IsActive = true };
        vm.EmitElectronicInvoice = true;
        vm.IsCustomCustomer = true;
        vm.SelectedCustomer = null;

        // Act
        await vm.ProcessPaymentCommand.ExecuteAsync(null);

        // Assert
        vm.ShowCustomerWarning.Should().BeTrue();
        vm.HasFeedback.Should().BeTrue();
        _mockTicketService.Verify(s => s.ProcessExitAsync(
            It.IsAny<Guid>(), It.IsAny<PaymentMethod>(), It.IsAny<decimal>(),
            It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPaymentAsync_WhenEmitElectronicInvoiceAndConsumidorFinal_ProcessesExitWithNullCustomer()
    {
        // Arrange
        var userSession = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero",
            HasElectronicInvoicingEnabled = true,
            ForceElectronicInvoiceOnCheckout = false
        };
        _mockSessionService.Setup(s => s.CurrentUser).Returns(userSession);

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "FINAL01",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        _mockTicketService.Setup(s => s.ProcessExitAsync(
            It.IsAny<Guid>(), It.IsAny<PaymentMethod>(), It.IsAny<decimal>(),
            It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string?>(),
            It.IsAny<decimal?>(), It.IsAny<decimal>(), It.IsAny<int?>(),
            It.IsAny<string?>(), It.IsAny<DateTime?>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<bool>(),
            It.IsAny<decimal>(), It.IsAny<bool>(), It.IsAny<Guid?>()))
            .ReturnsAsync(ticket);

        vm.SelectedTicket = ticket;
        vm.SelectedPaymentMethodEntity = vm.AvailablePaymentMethods.First();
        vm.SelectedResolution = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", IsActive = true, IsElectronicResolution = true };
        vm.EmitElectronicInvoice = true;
        vm.IsCustomCustomer = false; // Consumidor Final por defecto
        vm.SelectedCustomer = null;
        vm.AmountTendered = 10000m;

        // Act
        await vm.ProcessPaymentCommand.ExecuteAsync(null);

        // Assert
        vm.ShowCustomerWarning.Should().BeFalse();
        _mockTicketService.Verify(s => s.ProcessExitAsync(
            ticket.TicketId,
            It.IsAny<PaymentMethod>(),
            It.IsAny<decimal>(),
            It.IsAny<Guid?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<decimal>(),
            true, // EmitElectronicInvoice
            null // customerId: null para Consumidor Final (222222222222)
        ), Times.Once);
    }

    [Fact]
    public async Task SaveQuickCustomerCommand_WhenValid_SavesCustomerToDbAndSelectsIt()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.ToggleQuickRegisterCustomerCommand.Execute(null);

        vm.NewCustomerDocumentNumber = "1098765432";
        vm.NewCustomerFullName = "Cliente Rápido";
        vm.NewCustomerEmail = "cliente@rapido.com";
        vm.NewCustomerPhone = "3109998877";
        vm.NewCustomerAddress = "Calle 100 # 15-20";
        vm.NewCustomerCityCode = "11001";

        // Act
        await vm.SaveQuickCustomerCommand.ExecuteAsync(null);

        // Assert
        vm.SelectedCustomer.Should().NotBeNull();
        vm.SelectedCustomer!.DocumentNumber.Should().Be("1098765432");
        vm.SelectedCustomer.FullName.Should().Be("Cliente Rápido");
        vm.IsQuickRegisterCustomerOpen.Should().BeFalse();

        using var db = _connectionManager.CreateDbContext();
        var savedInDb = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(
            db.Customers, c => c.DocumentNumber == "1098765432");
        savedInDb.Should().NotBeNull();
        savedInDb!.Address.Should().Be("Calle 100 # 15-20");
        savedInDb.CityCode.Should().Be("11001");
        savedInDb.StateCode.Should().Be("11");
    }

    [Fact]
    public async Task SaveQuickCustomerCommand_WhenAddressOrCityMissing_FailsValidationAndDoesNotSave()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();
        vm.ToggleQuickRegisterCustomerCommand.Execute(null);

        vm.NewCustomerDocumentNumber = "1098765432";
        vm.NewCustomerFullName = "Cliente Rápido";
        vm.NewCustomerEmail = "cliente@rapido.com";
        vm.NewCustomerAddress = "";
        vm.NewCustomerCityCode = "";

        // Act
        await vm.SaveQuickCustomerCommand.ExecuteAsync(null);

        // Assert
        vm.SelectedCustomer.Should().BeNull();
        vm.IsQuickRegisterCustomerOpen.Should().BeTrue();
        vm.NewCustomerAddressError.Should().NotBeNullOrEmpty();
        vm.NewCustomerCityError.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void OnSelectedPaymentMethodEntityChanged_WhenRequiresResolution_LocksResolutionToElectronicAndExcludesPos()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica DIAN", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var paymentMethod = new PaymentMethodEntity
        {
            Id = 3,
            Name = "Transferencia Bancaria",
            RequiresResolution = true,
            RequiresCashTender = false
        };

        // Act
        vm.SelectedPaymentMethodEntity = paymentMethod;

        // Assert
        vm.IsResolutionLocked.Should().BeTrue();
        vm.ResolutionLockReason.Should().Contain("Transferencia Bancaria");
        vm.FilteredResolutions.Should().Contain(fvmRes);
        vm.FilteredResolutions.Should().NotContain(posRes);
        vm.SelectedResolution.Should().Be(fvmRes);
        vm.EmitElectronicInvoice.Should().BeTrue();
        vm.CanToggleElectronicInvoice.Should().BeFalse();
        vm.IsElectronicInvoicingSectionVisible.Should().BeTrue();
    }

    [Fact]
    public void SelectResolution_WhenResolutionLocked_RejectsPosSelection()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica DIAN", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        var paymentMethod = new PaymentMethodEntity
        {
            Id = 4,
            Name = "Tarjeta Prepago",
            RequiresResolution = true
        };
        vm.SelectedPaymentMethodEntity = paymentMethod;
        vm.SelectedResolution.Should().Be(fvmRes);

        // Act - Operator tries to manually pick POS
        vm.SelectResolutionCommand.Execute(posRes);

        // Assert - Selection remains the electronic resolution
        vm.SelectedResolution.Should().Be(fvmRes);
    }

    [Fact]
    public async Task DataSynchronized_WhenTicketIsSelected_DoesNotResetCheckoutStateOrCustomerDrawer()
    {
        // Arrange
        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "XYZ789",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-40)
        };
        vm.SelectedTicket = ticket;
        vm.EmitElectronicInvoice = true;
        vm.ToggleQuickRegisterCustomerCommand.Execute(null);
        vm.IsQuickRegisterCustomerOpen.Should().BeTrue();

        // Act - Trigger background sync notification
        _mockSyncEngine.Raise(m => m.DataSynchronized += null);

        // Assert - State remains untouched
        vm.SelectedTicket.Should().Be(ticket);
        vm.EmitElectronicInvoice.Should().BeTrue();
        vm.IsQuickRegisterCustomerOpen.Should().BeTrue();
    }

    [Fact]
    public void ApplyPaymentMethodResolutionFilter_WhenCashAndEmitElectronicInvoiceIsTrue_PreservesElectronicResolution()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica DIAN", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);

        vm.EmitElectronicInvoice = true;
        vm.SelectedResolution = fvmRes;

        var cashMethod = new PaymentMethodEntity
        {
            Id = 1,
            Name = "Efectivo",
            RequiresResolution = false,
            RequiresCashTender = true
        };

        // Act
        vm.SelectedPaymentMethodEntity = cashMethod;

        // Assert - Should NOT downgrade to POS
        vm.EmitElectronicInvoice.Should().BeTrue();
        vm.SelectedResolution.Should().Be(fvmRes);
    }

    [Fact]
    public void OnEmitElectronicInvoiceChanged_WhenToggled_SynchronizesSelectedResolution()
    {
        // Arrange
        var vm = CreateViewModel();
        var fvmRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica DIAN", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(fvmRes);
        vm.AvailableResolutions.Add(posRes);
        vm.SelectedResolution = posRes;

        // Act - Cashier enables electronic invoice
        vm.EmitElectronicInvoice = true;

        // Assert - Switches to electronic resolution
        vm.SelectedResolution.Should().Be(fvmRes);

        // Act - Cashier disables electronic invoice
        vm.EmitElectronicInvoice = false;

        // Assert - Switches back to POS
        vm.SelectedResolution.Should().Be(posRes);
    }

    [Fact]
    public async Task LoadPaymentMethodsAsync_AppliesBranchOverrideForRequiresCashTender()
    {
        // Arrange
        using (var db = _connectionManager.CreateDbContext())
        {
            db.PaymentMethods.Add(new PaymentMethodEntity
            {
                Id = 2,
                Name = "Tarjeta Débito",
                State = true,
                RequiresCashTender = true // Base entity says true
            });
            db.BranchPaymentMethods.Add(new BranchPaymentMethodEntity
            {
                BranchId = 1,
                PaymentMethodId = 2,
                IsActive = true,
                RequiresCashTender = false // Branch overrides to false
            });
            db.SaveChanges();
        }

        var vm = CreateViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        var debitMethod = vm.AvailablePaymentMethods.FirstOrDefault(pm => pm.Id == 2);
        debitMethod.Should().NotBeNull();
        debitMethod!.RequiresCashTender.Should().BeFalse();
    }

    [Fact]
    public void ApplyPaymentMethodResolutionFilter_WhenSwitchingFromLockedCardToCash_ResetsElectronicInvoice()
    {
        // Arrange
        var vm = CreateViewModel();
        var feRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica DIAN", IsElectronicResolution = true };
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        vm.AvailableResolutions.Add(feRes);
        vm.AvailableResolutions.Add(posRes);

        var cardMethod = new PaymentMethodEntity
        {
            Id = 2,
            Name = "Tarjeta Débito",
            RequiresResolution = true,
            RequiresCashTender = false
        };

        var cashMethod = new PaymentMethodEntity
        {
            Id = 1,
            Name = "Efectivo",
            RequiresResolution = false,
            RequiresCashTender = true
        };

        // Act 1 - Select Card: Locks resolution to FE and enables electronic invoice
        vm.SelectedPaymentMethodEntity = cardMethod;

        vm.IsResolutionLocked.Should().BeTrue();
        vm.EmitElectronicInvoice.Should().BeTrue();
        vm.SelectedResolution.Should().Be(feRes);

        // Act 2 - Switch to Cash: Should unlock, reset EmitElectronicInvoice, clear warnings and restore POS resolution
        vm.SelectedPaymentMethodEntity = cashMethod;

        // Assert
        vm.IsResolutionLocked.Should().BeFalse();
        vm.EmitElectronicInvoice.Should().BeFalse();
        vm.CanToggleElectronicInvoice.Should().BeTrue();
        vm.ShowResolutionWarning.Should().BeFalse();
        vm.ShowCustomerWarning.Should().BeFalse();
        vm.SelectedResolution.Should().Be(posRes);
    }

    [Fact]
    public void ToggleSelectAgreement_RequiresPurchase_InitializesCleanAndRequiresPurchaseAmount()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 5000m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(10000m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-AG1",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };
        vm.SelectedTicket = ticket;

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "Exito 20% Dcto",
            MinPurchaseAmount = 50000m,
            DiscountPercentage = 20m,
            DiscountType = 0
        };

        // Act
        vm.ToggleSelectAgreementCommand.Execute(agreement);

        // Assert - REGLA DE ORO: Formulario 100% limpio (NO pre-llenar con MinPurchaseAmount)
        vm.CustomerPurchaseAmount.Should().Be(0m);
        vm.CustomerPurchaseAmountText.Should().BeEmpty();
        vm.AgreementRequiresPurchase.Should().BeTrue();
        vm.AgreementMinPurchaseMet.Should().BeFalse();
        vm.IsAgreementEligible.Should().BeFalse();
        vm.DiscountAmount.Should().Be(0m);
        vm.CalculatedFee.Should().Be(10000m);
        vm.AgreementMinPurchaseStatusText.Should().Be("⚠️ Monto insuficiente");
        vm.AgreementMinPurchaseTooltipStatusText.Should().Be("⚠️ Pendiente");
        vm.IsAgreementTooltipOpen.Should().BeFalse();
    }

    [Fact]
    public void RecalculateLiveFee_WhenPurchaseAmountIsSufficient_AppliesDiscount()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 5000m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(10000m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-AG2",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };
        vm.SelectedTicket = ticket;

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "Exito 20% Dcto",
            MinPurchaseAmount = 50000m,
            DiscountPercentage = 20m,
            DiscountType = 0
        };

        vm.ToggleSelectAgreementCommand.Execute(agreement);

        // Act - User types purchase amount into CustomerPurchaseAmountText
        vm.CustomerPurchaseAmountText = "60000";

        // Assert
        vm.CustomerPurchaseAmount.Should().Be(60000m);
        vm.AgreementMinPurchaseMet.Should().BeTrue();
        vm.IsAgreementEligible.Should().BeTrue();
        vm.DiscountAmount.Should().Be(2000m); // 20% of 10000m
        vm.CalculatedFee.Should().Be(8000m);
        vm.AgreementMinPurchaseStatusText.Should().Be("✓ Cumple compra mínima");
        vm.AgreementMinPurchaseTooltipStatusText.Should().Be("✓ Cumplida");
        vm.AgreementRuleStatusTitle.Should().Contain("✓ Convenio aplicado");
    }

    [Fact]
    public async Task RecalculateLiveFee_WhenStayExceedsMaxHours_DeniesDiscountAndFlagsExceeded()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 5000m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(15000m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-AG3",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-180) // 3 hours stay
        };
        vm.SelectedTicket = ticket;

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "Cine Colombia 2h Max",
            MaxHoursApplicable = 2, // 120 minutes allowed max
            DiscountPercentage = 50m,
            MinPurchaseAmount = 0m
        };

        // Act
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreement);

        // Assert - El convenio debe ser bloqueado y desmarcado inmediatamente, notificando al operador
        vm.SelectedAgreement.Should().BeNull();
        vm.HasAgreementDiscount.Should().BeFalse();
        vm.DiscountAmount.Should().Be(0m);
        vm.CalculatedFee.Should().Be(15000m);
        _mockDialogService.Verify(d => d.ShowAlertAsync(
            It.Is<string>(s => s.Contains("Convenio")),
            It.Is<string>(s => s.Contains("supera el tiempo")),
            DialogNotificationType.Warning), Times.Once);
    }

    [Fact]
    public async Task ToggleSelectAgreement_FixedFeeWithCoverage_WhenExceedsMaxHours_DoesNotBlockAndCalculatesExcess()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 5000m, MinuteRate = 100m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(45000m); // 9 horas normal = 45.000
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 480, false))
            .Returns(5000m); // 1 hora de excedente = 5.000

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-COV1",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-540) // 9 hours stay
        };
        vm.SelectedTicket = ticket;

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "Convenio Proveedor Aliado",
            DiscountType = 3, // FixedFeeWithCoverage
            DiscountFixedAmount = 30000m,
            MaxHoursApplicable = 8, // 480 min cobertura
            MinPurchaseAmount = 0m
        };

        // Act
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreement);

        // Assert - No se bloquea, aplica tarifa fija + excedente
        vm.SelectedAgreement.Should().Be(agreement);
        vm.HasAgreementDiscount.Should().BeTrue();
        vm.IsAgreementEligible.Should().BeTrue();
        vm.CalculatedFee.Should().Be(35000m); // 30.000 base + 5.000 excedente
        vm.AgreementEligibilityBannerStatus.Should().Be("warning");
        vm.AgreementEligibilityBannerText.Should().Contain("Excedente");
        _mockDialogService.Verify(d => d.ShowAlertAsync(
            It.Is<string>(s => s.Contains("Convenio")),
            It.IsAny<string>(),
            DialogNotificationType.Warning), Times.Never);
    }

    [Fact]
    public void RecalculateLiveFee_FreeTimeAgreement_EvaluatesMinPurchaseAndCalculatesFeeDifference()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 5000m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(8000m);
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 60, false))
            .Returns(2000m); // After 60 free minutes, fee drops to 2000m

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-AG4",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-50)
        };
        vm.SelectedTicket = ticket;

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "Gimnasio 1 Hora Gratis",
            DiscountType = 2, // Free time
            FreeHours = 1,
            MinPurchaseAmount = 25000m
        };

        // Act 1: Initial selection without purchase amount
        vm.ToggleSelectAgreementCommand.Execute(agreement);

        // Assert 1: Not eligible until purchase amount met
        vm.IsAgreementEligible.Should().BeFalse();
        vm.DiscountAmount.Should().Be(0m);
        vm.CalculatedFee.Should().Be(8000m);

        // Act 2: Fulfill purchase amount
        vm.CustomerPurchaseAmountText = "30000";

        // Assert 2: Discount is fee difference (8000 - 2000 = 6000)
        vm.IsAgreementEligible.Should().BeTrue();
        vm.DiscountAmount.Should().Be(6000m);
        vm.CalculatedFee.Should().Be(2000m);
    }

    [Fact]
    public void MoreDataPopup_ToggleAndCloseCommands_ChangePopupState()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsMoreDataPopupOpen.Should().BeFalse();

        // Act 1: Toggle to open
        vm.ToggleMoreDataPopupCommand.Execute(null);
        vm.IsMoreDataPopupOpen.Should().BeTrue();

        // Act 2: Toggle to close
        vm.ToggleMoreDataPopupCommand.Execute(null);
        vm.IsMoreDataPopupOpen.Should().BeFalse();

        // Act 3: Open then call Close
        vm.ToggleMoreDataPopupCommand.Execute(null);
        vm.IsMoreDataPopupOpen.Should().BeTrue();
        vm.CloseMoreDataPopupCommand.Execute(null);
        vm.IsMoreDataPopupOpen.Should().BeFalse();
    }

    [Fact]
    public void FormattedCustomerPhoneAndNotes_WhenEmptyOrNull_ReturnNA()
    {
        // Arrange
        var vm = CreateViewModel();

        // Without SelectedTicket
        vm.FormattedCustomerPhone.Should().Be("N/A");
        vm.FormattedNotes.Should().Be("N/A");

        // With SelectedTicket with null/empty values
        vm.SelectedTicket = new ParkingTicket
        {
            CustomerPhone = null,
            Notes = "   "
        };

        // Assert
        vm.FormattedCustomerPhone.Should().Be("N/A");
        vm.FormattedNotes.Should().Be("N/A");
    }

    [Fact]
    public void FormattedCustomerPhoneAndNotes_WhenProvided_ReturnValues()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.SelectedTicket = new ParkingTicket
        {
            CustomerPhone = "3001234567",
            Notes = "Dejó llaves en administración"
        };

        // Assert
        vm.FormattedCustomerPhone.Should().Be("3001234567");
        vm.FormattedNotes.Should().Be("Dejó llaves en administración");
    }

    [Fact]
    public void ExitNotes_TruncatesTo50Characters()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.ExitNotes = "123456789012345678901234567890123456789012345678901234567890"; // 60 chars

        // Assert
        vm.ExitNotes.Should().HaveLength(50);
        vm.ExitNotes.Should().Be("12345678901234567890123456789012345678901234567890");
    }

    [Fact]
    public void ExitNotes_CleansOnCancelSelection()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ExitNotes = "Observación de prueba";

        // Act
        vm.CancelSelectionCommand.Execute(null);

        // Assert
        vm.ExitNotes.Should().BeEmpty();
    }

    [Fact]
    public void IsNitDocumentType_TogglesCorrectly_AndCleansCheckDigitWhenNotNit()
    {
        // Arrange
        var vm = CreateViewModel();
        var ccOption = vm.IdentificationTypeOptions.First(o => o.Id == 1);
        var nitOption = vm.IdentificationTypeOptions.First(o => o.Id == 3);

        // Act & Assert 1: NIT
        vm.SelectedIdentificationTypeOption = nitOption;
        vm.IsNitDocumentType.Should().BeTrue();
        vm.NewCustomerDocumentNumber = "900123456";
        vm.NewCustomerCheckDigit.Should().NotBeNullOrEmpty();

        // Act & Assert 2: CC
        vm.SelectedIdentificationTypeOption = ccOption;
        vm.IsNitDocumentType.Should().BeFalse();
        vm.NewCustomerCheckDigit.Should().BeNull();
        vm.NewCustomerCheckDigitError.Should().BeNull();
    }

    [Fact]
    public void ApplyCustomerFilter_FiltersByDocumentNumberAndFullName()
    {
        // Arrange
        var vm = CreateViewModel();
        var c1 = new Customer { CustomerId = Guid.NewGuid(), FullName = "Carlos Perez", DocumentNumber = "10102020" };
        var c2 = new Customer { CustomerId = Guid.NewGuid(), FullName = "Maria Gomez", DocumentNumber = "1030679725" };
        var c3 = new Customer { CustomerId = Guid.NewGuid(), FullName = "Inversiones SAS", DocumentNumber = "900123456" };

        vm.AvailableCustomers.Add(c1);
        vm.AvailableCustomers.Add(c2);
        vm.AvailableCustomers.Add(c3);
        vm.ApplyCustomerFilter();

        vm.FilteredAvailableCustomers.Should().HaveCount(3);

        // Act 1: Buscar por cédula
        vm.CustomerSearchText = "10306";
        vm.FilteredAvailableCustomers.Should().ContainSingle();
        vm.FilteredAvailableCustomers.First().FullName.Should().Be("Maria Gomez");
        vm.IsCustomerDropDownOpen.Should().BeTrue();

        // Act 2: Buscar por nombre
        vm.CustomerSearchText = "Inversiones";
        vm.FilteredAvailableCustomers.Should().ContainSingle();
        vm.FilteredAvailableCustomers.First().DocumentNumber.Should().Be("900123456");
        vm.IsCustomerDropDownOpen.Should().BeTrue();

        // Act 3: Limpiar búsqueda
        vm.CustomerSearchText = string.Empty;
        vm.FilteredAvailableCustomers.Should().HaveCount(3);
        vm.IsCustomerDropDownOpen.Should().BeFalse();
    }

    [Fact]
    public void CustomerSelection_WhenSelectedFromList_RetainsSelectionAndClosesDropDown()
    {
        // Arrange
        var vm = CreateViewModel();
        var c1 = new Customer { CustomerId = Guid.NewGuid(), FullName = "Sebastian Redondo", DocumentNumber = "1018504647" };
        var c2 = new Customer { CustomerId = Guid.NewGuid(), FullName = "Maria Gomez", DocumentNumber = "1030679725" };

        vm.AvailableCustomers.Add(c1);
        vm.AvailableCustomers.Add(c2);
        vm.ApplyCustomerFilter();

        // Act 1: Tipear cédula abre dropdown y filtra
        vm.CustomerSearchText = "1018";
        vm.FilteredAvailableCustomers.Should().ContainSingle();
        vm.IsCustomerDropDownOpen.Should().BeTrue();

        // Act 2: Seleccionar cliente desde la lista (simula clic de usuario en ComboBox)
        vm.SelectedCustomer = c1;

        // Assert
        vm.SelectedCustomer.Should().NotBeNull();
        vm.SelectedCustomer.Should().Be(c1);
        vm.CustomerSearchText.Should().Be(c1.DisplayText);
        vm.IsCustomerDropDownOpen.Should().BeFalse();
        vm.FilteredAvailableCustomers.Should().Contain(c1);
        vm.ShowCustomerWarning.Should().BeFalse();
    }

    [Fact]
    public async Task OnSelectedTicketChanged_WhenTicketSelected_InitializesPaymentMethodAndResolutionAsNull()
    {
        // Arrange
        var posRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Factura POS Estándar", IsElectronicResolution = false };
        var feRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "FE", Name = "Facturación Electrónica", IsElectronicResolution = true };
        _mockBillingResolutionService.Setup(b => b.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution> { posRes, feRes });

        var vm = CreateViewModel();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        // Act
        vm.SelectedTicket = ticket;
        await Task.Delay(100);

        // Assert: Initially empty (null) as required
        vm.SelectedPaymentMethodEntity.Should().BeNull();
        vm.SelectedPaymentMethod.Should().BeNull();
        vm.SelectedResolution.Should().BeNull();
        vm.ShowPaymentMethodWarning.Should().BeFalse();
        vm.ShowResolutionWarning.Should().BeFalse();
        vm.HasNoPaymentMethodSelected.Should().BeTrue();
        vm.IsCashPaymentSelected.Should().BeFalse();
        vm.IsNonCashPaymentSelected.Should().BeFalse();

        // Act 2: When operator explicitly selects Cash
        var cashMethod = vm.AvailablePaymentMethods.First(p => p.Id == 1);
        vm.SelectedPaymentMethodEntity = cashMethod;

        // Assert 2: Selected method is configured
        vm.SelectedPaymentMethodEntity.Should().Be(cashMethod);
        vm.SelectedPaymentMethod.Should().Be(PaymentMethod.Cash);
        vm.IsCashPaymentSelected.Should().BeTrue();
        vm.HasNoPaymentMethodSelected.Should().BeFalse();
    }

    [Fact]
    public async Task OnSelectedTicketChanged_WithConfiguredDefaultResolutionOnMethod_LoadsAndSelectsTargetResolution()
    {
        // Arrange
        var customRes = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "RES-CUSTOM", Name = "Resolución Especial", IsElectronicResolution = false };
        _mockBillingResolutionService.Setup(b => b.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution> { customRes });

        using (var db = _connectionManager.CreateDbContext())
        {
            var pm = db.PaymentMethods.First(p => p.Id == 1);
            pm.DefaultResolutionId = customRes.ResolutionId.ToString();
            db.SaveChanges();
        }

        var vm = CreateViewModel();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            PlateNumber = "XYZ987",
            VehicleType = VehicleType.Motorcycle,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-15)
        };

        // Act
        vm.SelectedTicket = ticket;
        await Task.Delay(100);

        // Assert: Initially empty (null)
        vm.SelectedPaymentMethodEntity.Should().BeNull();
        vm.SelectedResolution.Should().BeNull();

        // Act 2: Operator chooses configured method
        var configuredMethod = vm.AvailablePaymentMethods.First(p => p.Id == 1);
        vm.SelectedPaymentMethodEntity = configuredMethod;

        // Assert 2: Method and configured default resolution are loaded
        vm.SelectedPaymentMethodEntity.Should().NotBeNull();
        vm.SelectedResolution.Should().NotBeNull();
        vm.SelectedResolution.Should().Be(customRes);
        vm.ShowResolutionWarning.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleSelectAgreement_WhenDiscountReachesZero_AdaptsAmountTenderedAndChangeToZero()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 1150m, MinuteRate = 20m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(1150m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-ZERO",
            PlateNumber = "XYZ123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        vm.SelectedTicket = ticket;
        await Task.Delay(50);
        vm.AmountTendered.Should().Be(1150m);
        vm.CalculatedFee.Should().Be(1150m);

        var agreementPuppis = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "PUPPIS",
            DiscountFixedAmount = 1150m,
            IsActive = true
        };

        // Act
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreementPuppis);

        // Assert: Al dar 100% de descuento (Neto = 0), Monto recibido y Cambio deben ser 0
        vm.CalculatedFee.Should().Be(0m);
        vm.DiscountAmount.Should().Be(1150m);
        vm.AmountTendered.Should().Be(0m);
        vm.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public async Task ToggleSelectAgreement_WhenPartialDiscount_AdaptsAmountTenderedToNetFeeAndZeroChange()
    {
        // Arrange
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 1150m, MinuteRate = 20m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(1150m);

        var vm = CreateViewModel();
        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-PARTIAL",
            PlateNumber = "XYZ123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        vm.SelectedTicket = ticket;
        await Task.Delay(50);

        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            Name = "DESCUENTO_1000",
            DiscountFixedAmount = 1000m,
            IsActive = true
        };

        // Act
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreement);

        // Assert: Si cobro era 1150 y descuenta 1000, neto es 150 y monto recibido debe adaptarse a 150 con cambio 0
        vm.CalculatedFee.Should().Be(150m);
        vm.DiscountAmount.Should().Be(1000m);
        vm.AmountTendered.Should().Be(150m);
        vm.ChangeDue.Should().Be(0m);

        // Si luego el usuario digita un billete mayor de $5.000, el cambio se calcula automáticamente
        vm.AmountTendered = 5000m;
        vm.ChangeDue.Should().Be(4850m);

        // Act 2: Desmarcar el convenio restaura el monto recibido al cobro bruto sin saldo residual
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreement);
        vm.CalculatedFee.Should().Be(1150m);
        vm.DiscountAmount.Should().Be(0m);
        vm.AmountTendered.Should().Be(1150m);
        vm.ChangeDue.Should().Be(0m);
    }

    [Fact]
    public async Task ProcessPaymentAsync_PreservesSelectedAgreementWithoutOverwriting()
    {
        // Arrange
        var storeArchiesId = Guid.NewGuid();
        var storePuppisId = Guid.NewGuid();

        var storeArchies = new Store { StoreId = storeArchiesId, Name = "Archies Store" };
        var storePuppis = new Store { StoreId = storePuppisId, Name = "Puppis Store" };

        var agreementArchies = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            StoreId = storeArchiesId,
            Name = "ARCHIES",
            DiscountFixedAmount = 500m,
            IsActive = true
        };

        var agreementPuppis = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            StoreId = storePuppisId,
            Name = "PUPPIS",
            DiscountFixedAmount = 1150m,
            IsActive = true
        };

        _mockStoreService.Setup(s => s.GetActiveStoresAsync())
            .ReturnsAsync(new List<Store> { storeArchies, storePuppis });
        _mockAgreementService.Setup(a => a.GetAgreementsByStoreAsync(storeArchiesId))
            .ReturnsAsync(new List<CommercialAgreement> { agreementArchies });
        _mockAgreementService.Setup(a => a.GetAgreementsByStoreAsync(storePuppisId))
            .ReturnsAsync(new List<CommercialAgreement> { agreementPuppis });

        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, It.IsAny<DayOfWeek?>()))
            .Returns(new VehicleRate { VehicleType = VehicleType.Car, HourRate = 1150m });
        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(1150m);

        var resolution = new BillingResolution { ResolutionId = Guid.NewGuid(), Prefix = "POS", Name = "Resolución POS" };
        _mockBillingResolutionService.Setup(b => b.GetActiveResolutionsByBranchAsync(It.IsAny<int?>()))
            .ReturnsAsync(new List<BillingResolution> { resolution });

        _mockTicketService.Setup(s => s.ProcessExitAsync(
                It.IsAny<Guid>(),
                It.IsAny<PaymentMethod>(),
                It.IsAny<decimal>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<decimal?>(),
                It.IsAny<decimal>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<decimal>(),
                It.IsAny<bool>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new ParkingTicket { TicketId = Guid.NewGuid(), NetAmount = 0m, DiscountAmount = 1150m });

        var vm = CreateViewModel();
        await vm.InitializeAsync();

        var ticket = new ParkingTicket
        {
            TicketId = Guid.NewGuid(),
            TicketNumber = "PKF-PUPPIS-TEST",
            PlateNumber = "ABC123",
            VehicleType = VehicleType.Car,
            EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
        };

        vm.SelectedTicket = ticket;
        await Task.Delay(50);

        // Act: El usuario selecciona PUPPIS
        await vm.ToggleSelectAgreementCommand.ExecuteAsync(agreementPuppis);

        // Simular medio de pago y resolución
        vm.SelectedPaymentMethodEntity = new PaymentMethodEntity { Id = 1, Name = "Efectivo", RequiresCashTender = true };
        vm.SelectedResolution = resolution;

        // Procesar pago
        await vm.ProcessPaymentCommand.ExecuteAsync(null);

        // Assert: El convenio enviado a ProcessExitAsync debe ser estrictamente PUPPIS y NO ARCHIES
        _mockTicketService.Verify(s => s.ProcessExitAsync(
            It.IsAny<Guid>(),
            It.IsAny<PaymentMethod>(),
            It.IsAny<decimal>(),
            It.IsAny<Guid?>(),
            agreementPuppis.AgreementId,
            It.IsAny<string?>(),
            It.IsAny<decimal?>(),
            It.IsAny<decimal>(),
            It.IsAny<int?>(),
            It.IsAny<string?>(),
            It.IsAny<DateTime>(),
            It.IsAny<Guid?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<bool>(),
            It.IsAny<decimal>(),
            It.IsAny<bool>(),
            It.IsAny<Guid?>()), Times.Once);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}

