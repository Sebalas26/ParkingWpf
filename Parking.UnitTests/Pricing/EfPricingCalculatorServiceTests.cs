using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Pricing;

public class EfPricingCalculatorServiceTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Branch _testBranch;
    private readonly BranchModel _testBranchModel;

    public EfPricingCalculatorServiceTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockSyncEngine = new Mock<ISyncEngineService>();
        _mockSessionService = new Mock<ISessionService>();

        _testBranch = new Branch
        {
            Id = 1,
            Name = "Sede Norte",
            AllowChargeByMinute = true,
            AllowChargeByHour = true,
            AllowChargeByDay = true,
            AllowChargeByNight = true,
            FullDayThresholdMinutes = 360, // 6 Horas
            FullDayApplicableDays = "All",
            NightStartTime = new TimeSpan(20, 0, 0),
            NightEndTime = new TimeSpan(6, 0, 0),
            NightStayMinMinutes = 240, // 4 Horas
            LostTicketFee = 15000m
        };

        _testBranchModel = new BranchModel
        {
            Id = _testBranch.Id,
            Name = _testBranch.Name,
            AllowChargeByMinute = _testBranch.AllowChargeByMinute,
            AllowChargeByHour = _testBranch.AllowChargeByHour,
            AllowChargeByDay = _testBranch.AllowChargeByDay,
            AllowChargeByNight = _testBranch.AllowChargeByNight,
            FullDayThresholdMinutes = _testBranch.FullDayThresholdMinutes,
            FullDayApplicableDays = _testBranch.FullDayApplicableDays,
            NightStartTime = _testBranch.NightStartTime,
            NightEndTime = _testBranch.NightEndTime,
            NightStayMinMinutes = _testBranch.NightStayMinMinutes,
            LostTicketFee = _testBranch.LostTicketFee
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
    }

    private async Task SeedRateAsync(VehicleRate rate)
    {
        using var db = _connectionManager.CreateDbContext();
        db.VehicleRates.Add(rate);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CalculateFee_WithinGracePeriod_ReturnsZero()
    {
        // Arrange
        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            GracePeriodMinutes = 15,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(12);

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert
        fee.Should().Be(0m);
    }

    [Fact]
    public async Task CalculateFee_MinuteOnly_CalculatesAccurately()
    {
        // Arrange
        _testBranchModel.AllowChargeByHour = false;
        _testBranchModel.AllowChargeByMinute = true;
        _testBranchModel.AllowChargeByDay = false;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 60m,
            HourRate = 0m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(45);

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert: 45 min * 60 = 2700
        fee.Should().Be(2700m);
    }

    [Fact]
    public async Task CalculateFee_HourOnly_CeilsToNextHour()
    {
        // Arrange
        _testBranchModel.AllowChargeByMinute = false;
        _testBranchModel.AllowChargeByHour = true;
        _testBranchModel.AllowChargeByDay = false;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 0m,
            HourRate = 3500m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(65); // 1 hora y 5 minutos -> se factura 2 horas

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert: 2 * 3500 = 7000
        fee.Should().Be(7000m);
    }

    [Fact]
    public async Task CalculateFee_MixedMinuteAndHour_CalculatesHoursPlusMinutesCappedAtHourRate()
    {
        // Arrange
        _testBranchModel.AllowChargeByMinute = true;
        _testBranchModel.AllowChargeByHour = true;
        _testBranchModel.AllowChargeByDay = false;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 2000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(80); // 1h (2000) + 20min * 50 (1000) = 3000

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert
        fee.Should().Be(3000m);
    }

    [Fact]
    public async Task CalculateFee_FullDayRate_AppliesWhenThresholdExceeded()
    {
        // Arrange
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayThresholdMinutes = 360; // 6 horas

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(400); // Supera 360 min

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert: Aplica plena cíclica 1 día base = 18000 + rem(40min*50=2000) = 20000
        fee.Should().Be(20000m);
    }

    [Fact]
    public async Task CalculateFee_FullDayRate_DoesNotApplyWhenAllowChargeByDayIsFalse()
    {
        // Arrange
        _testBranchModel.AllowChargeByDay = false; // Desactivado explícitamente en la sede

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(420); // 7 horas

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert: 7 horas * 3000 = 21000 (no aplica plena porque AllowChargeByDay = false)
        fee.Should().Be(21000m);
    }

    [Fact]
    public async Task CalculateFee_CommercialAgreementFreeMinutes_DeductedBeforeEvaluatingFullDayThreshold()
    {
        // Arrange
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayThresholdMinutes = 360; // 6 horas

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(390); // 390 minutos de estancia bruta

        // Si se descuentan 60 minutos de cortesía de convenio: 390 - 60 = 330 min efectivos.
        // Como 330 < 360 (umbral de plena), NO califica para plena y se liquida regular: 5h (15000) + 30m*50 (1500) = 16500.
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime, discountFreeMinutes: 60);

        // Assert
        fee.Should().Be(16500m);
    }

    [Fact]
    public async Task CalculateFee_LostTicket_AddsLostTicketFeeWhenIsLostTicketIsTrue()
    {
        // Arrange
        _testBranchModel.LostTicketFee = 15000m;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 2000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTime = new DateTime(2026, 9, 6, 10, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddMinutes(60); // 1 hora = 2000m

        // Act
        var feeWithoutLost = service.CalculateFee(VehicleType.Car, entryTime, exitTime, 0, isLostTicket: false);
        var feeWithLost = service.CalculateFee(VehicleType.Car, entryTime, exitTime, 0, isLostTicket: true);

        // Assert
        feeWithoutLost.Should().Be(2000m);
        feeWithLost.Should().Be(2000m + 15000m);
    }

    [Fact]
    public async Task CalculateFee_NightRate_AppliesWhenWithinNightWindowAndMinStayMet()
    {
        // Arrange
        _testBranchModel.AllowChargeByNight = true;
        _testBranchModel.NightStartTime = new TimeSpan(20, 0, 0); // 8:00 PM
        _testBranchModel.NightEndTime = new TimeSpan(6, 0, 0);    // 6:00 AM
        _testBranchModel.NightStayMinMinutes = 240;               // 4 horas

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            NightRate = 12000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        // UTC = COT + 5h.
        // 21:00 COT = 02:00 UTC siguiente día.
        // 05:00 COT = 10:00 UTC siguiente día.
        var entryTimeUtc = new DateTime(2026, 9, 7, 2, 0, 0, DateTimeKind.Utc);
        var exitTimeUtc = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc); // 8 horas

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTimeUtc, exitTimeUtc);

        // Assert
        fee.Should().Be(12000m);
    }

    [Fact]
    public async Task CalculateFee_NightRate_DoesNotApplyWhenAllowChargeByNightIsFalse()
    {
        // Arrange
        _testBranchModel.AllowChargeByNight = false; // Deshabilitado en sede
        _testBranchModel.AllowChargeByDay = false;
        _testBranchModel.NightStartTime = new TimeSpan(20, 0, 0);
        _testBranchModel.NightEndTime = new TimeSpan(6, 0, 0);
        _testBranchModel.NightStayMinMinutes = 240;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            NightRate = 12000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryTimeUtc = new DateTime(2026, 9, 7, 2, 0, 0, DateTimeKind.Utc);
        var exitTimeUtc = new DateTime(2026, 9, 7, 10, 0, 0, DateTimeKind.Utc); // 8 horas

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTimeUtc, exitTimeUtc);

        // Assert: 8 horas * 3000 = 24000 (no cobra pernocta porque AllowChargeByNight es falso)
        fee.Should().Be(24000m);
    }

    [Fact]
    public async Task GetRate_Hierarchy_SpecificDayOfWeekTakesPrecedenceOverGeneralRate()
    {
        // Arrange
        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Motorcycle,
            DisplayName = "Tarifa General Moto",
            MinuteRate = 30m,
            HourRate = 1500m,
            DayOfWeek = null,
            IsActive = true
        });

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Motorcycle,
            DisplayName = "Tarifa Domingo Moto",
            MinuteRate = 40m,
            HourRate = 2000m,
            DayOfWeek = DayOfWeek.Sunday,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        // Act
        var sundayRate = service.GetRate(VehicleType.Motorcycle, DayOfWeek.Sunday);
        var mondayRate = service.GetRate(VehicleType.Motorcycle, DayOfWeek.Monday);

        // Assert
        sundayRate.Should().NotBeNull();
        sundayRate!.DisplayName.Should().Be("Tarifa Domingo Moto");
        sundayRate.HourRate.Should().Be(2000m);

        mondayRate.Should().NotBeNull();
        mondayRate!.DisplayName.Should().Be("Tarifa General Moto");
        mondayRate.HourRate.Should().Be(1500m);
    }

    [Fact]
    public async Task CalculateFee_FullDayRate_WhenVehicleThresholdOverridesBranchThreshold_UsesVehicleThreshold()
    {
        // Arrange: Sede tiene umbral de 12 horas (720 min), pero el vehículo tiene umbral personalizado de 8 horas (480 min)
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayThresholdMinutes = 720; // 12 horas en sede
        _testBranchModel.FullDayApplicableDays = "1,2,3,4,5,6,0";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            FullDayThresholdMinutes = 480, // 8 horas en vehículo
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        // 9 horas de permanencia (540 minutos): Mayor a 8 horas (umbral vehículo), pero menor a 12 horas (umbral sede).
        // Cobra 1 día pleno (18000) + 1 hora excedente (3000) = 21000.
        // Si no hubiera aplicado el umbral de 8h del vehículo y se usara el de 12h de sede, cobraría 9h * 3000 = 27000.
        var entryTime = new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc);
        var exitTime = entryTime.AddHours(9);

        // Act
        var fee = service.CalculateFee(VehicleType.Car, entryTime, exitTime);

        // Assert: Aplica tarifa plena de 18000 + 3000 = 21000 (en vez de 27000 por horas ordinarias)
        fee.Should().Be(21000m);
    }

    [Fact]
    public async Task CalculateFee_FullDayRate_RespectsApplicableDaysCommaSeparatedNumbers()
    {
        // Arrange: Plena solo aplica lunes a viernes ("1,2,3,4,5")
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayThresholdMinutes = 480; // 8 horas
        _testBranchModel.FullDayApplicableDays = "1,2,3,4,5";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 15000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        // Domingo 13 de Septiembre 2026: 8 horas de permanencia.
        // Como domingo (0) no está en "1,2,3,4,5", NO aplica plena y cobra horas ordinarias (8 * 3000 = 24000).
        // 13:00 COT = 18:00 UTC
        var sundayEntryUtc = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var sundayExitUtc = sundayEntryUtc.AddHours(8);

        var sundayFee = service.CalculateFee(VehicleType.Car, sundayEntryUtc, sundayExitUtc);
        sundayFee.Should().Be(24000m);

        // Lunes 14 de Septiembre 2026: 8 horas de permanencia.
        // Como lunes (1) SÍ está en "1,2,3,4,5", aplica plena exacta de 8 horas = 15000.
        var mondayEntryUtc = new DateTime(2026, 9, 14, 18, 0, 0, DateTimeKind.Utc);
        var mondayExitUtc = mondayEntryUtc.AddHours(8);

        var mondayFee = service.CalculateFee(VehicleType.Car, mondayEntryUtc, mondayExitUtc);
        mondayFee.Should().Be(15000m);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
