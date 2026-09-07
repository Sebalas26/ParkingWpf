using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class CheckInViewModelTests
{
    private readonly Mock<IParkingTicketService> _mockTicketService;
    private readonly Mock<IPricingCalculatorService> _mockPricingCalculator;
    private readonly Mock<IMonthlySubscriptionService> _mockSubscriptionService;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IShiftService> _mockShiftService;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IPermissionService> _mockPermissionService;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly BranchModel _testBranchModel;

    public CheckInViewModelTests()
    {
        _mockTicketService = new Mock<IParkingTicketService>();
        _mockPricingCalculator = new Mock<IPricingCalculatorService>();
        _mockSubscriptionService = new Mock<IMonthlySubscriptionService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockShiftService = new Mock<IShiftService>();
        _mockSyncEngine = new Mock<ISyncEngineService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockPermissionService = new Mock<IPermissionService>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _testBranchModel = new BranchModel
        {
            Id = 1,
            Name = "Sede Norte",
            TotalCapacity = 50,
            OperatingHours = new List<BranchOperatingHour>()
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
        _mockShiftService.Setup(s => s.HasActiveShift).Returns(true);
        _mockPermissionService.Setup(p => p.HasPermission(It.IsAny<string>())).Returns(true);
    }

    private CheckInViewModel CreateViewModel()
    {
        return new CheckInViewModel(
            _mockTicketService.Object,
            _mockPricingCalculator.Object,
            _mockSubscriptionService.Object,
            _mockAuthService.Object,
            _mockDialogService.Object,
            _mockShiftService.Object,
            _mockSyncEngine.Object,
            _mockSessionService.Object,
            _mockPermissionService.Object,
            _mockServiceProvider.Object);
    }

    [Fact]
    public void PlateNumber_UpdatesAndEnablesRegisterCommand()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.PlateNumber = "XYZ123";

        // Assert
        vm.PlateNumber.Should().Be("XYZ123");
        vm.RegisterAndPrintCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void PreClosingAlert_WhenWithin5MinutesOfClosing_ActivatesBanner()
    {
        // Arrange
        var now = DateTime.Now;
        var closingTime = now.TimeOfDay.Add(TimeSpan.FromMinutes(3)); // Cierra en 3 minutos
        _testBranchModel.OperatingHours = new List<BranchOperatingHour>
        {
            new()
            {
                BranchId = 1,
                DayOfWeek = now.DayOfWeek,
                IsOpen = true,
                OpeningTime = new TimeSpan(6, 0, 0),
                ClosingTime = closingTime,
                BufferMinutesBefore = 0,
                BufferMinutesAfter = 15
            }
        };

        var vm = CreateViewModel();

        // Assert
        vm.IsPreClosingAlertVisible.Should().BeTrue();
        vm.PreClosingAlertMessage.Should().Contain("ALERTA DE CIERRE");
    }

    [Fact]
    public void PreClosingAlert_WhenClosingTimePassed_ShowsClosedWarningMessage()
    {
        // Arrange
        var now = DateTime.Now;
        var closingTime = now.TimeOfDay.Subtract(TimeSpan.FromMinutes(5)); // Cerró hace 5 minutos
        _testBranchModel.OperatingHours = new List<BranchOperatingHour>
        {
            new()
            {
                BranchId = 1,
                DayOfWeek = now.DayOfWeek,
                IsOpen = true,
                OpeningTime = new TimeSpan(6, 0, 0),
                ClosingTime = closingTime,
                BufferMinutesBefore = 0,
                BufferMinutesAfter = 30 // Aún en tiempo de buffer
            }
        };

        var vm = CreateViewModel();

        // Assert
        vm.IsPreClosingAlertVisible.Should().BeTrue();
        vm.PreClosingAlertMessage.Should().Contain("HORARIO DE CIERRE SUPERADO");
    }

    [Fact]
    public void PreClosingAlert_WhenDuringNormalOperatingHours_DoesNotShowBanner()
    {
        // Arrange
        var now = DateTime.Now;
        _testBranchModel.OperatingHours = new List<BranchOperatingHour>
        {
            new()
            {
                BranchId = 1,
                DayOfWeek = now.DayOfWeek,
                IsOpen = true,
                OpeningTime = now.TimeOfDay.Subtract(TimeSpan.FromHours(2)),
                ClosingTime = now.TimeOfDay.Add(TimeSpan.FromHours(4)), // Falta mucho para el cierre
                BufferMinutesBefore = 15,
                BufferMinutesAfter = 15
            }
        };

        var vm = CreateViewModel();

        // Assert
        vm.IsPreClosingAlertVisible.Should().BeFalse();
        vm.PreClosingAlertMessage.Should().BeNull();
    }
}
