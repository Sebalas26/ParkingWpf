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
            FullDayCoverageMinutes = 360,
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
            FullDayCoverageMinutes = 480, // 8 horas de cobertura en vehículo
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

    [Fact]
    public async Task CalculateFee_FullDay_EarlyTriggerWithBroadCoverage_ChargesSingleFullDayWithinCoverage()
    {
        // Caso: Umbral de activación a las 3 horas (180 min), pero cobertura de 12 horas (720 min).
        // Si el vehículo permanece 9 horas (540 min), supera el umbral de 3h y está amparado dentro de las 12h.
        // Debe cobrar únicamente la tarifa plena (18000), sin cobrar horas ordinarias ni excedentes adicionales.
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayThresholdMinutes = 180; // 3h activación
        _testBranchModel.FullDayApplicableDays = "All";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            FullDayThresholdMinutes = 180,
            FullDayCoverageMinutes = 720, // 12h cobertura
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc); // 8:00 COT
        var exitUtc = entryUtc.AddHours(9); // 17:00 COT (9 horas de estadía)

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);

        fee.Should().Be(18000m);
    }

    [Fact]
    public async Task CalculateFee_FullDay_CyclicRecurrence_TriggersSecondFullDayWhenExceedingCoveragePlusTrigger()
    {
        // Caso recurrente cíclico: Cobertura de 12h (720 min), umbral de 3h (180 min).
        // A las 15 horas y 10 minutos (910 min): 
        // 1er ciclo completo = 720 min (18000).
        // Excedente = 190 min. Como 190 min >= 180 min (umbral), se activa la SEGUNDA tarifa plena!
        // Cobro esperado = 18000 + 18000 = 36000.
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayApplicableDays = "All";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            FullDayThresholdMinutes = 180,
            FullDayCoverageMinutes = 720,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc); // 8:00 COT
        var exitUtc = entryUtc.AddHours(15).AddMinutes(10); // 15h 10m

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);

        fee.Should().Be(36000m);
    }

    [Fact]
    public async Task CalculateFee_FullDay_DayToNightTransition_AppliesNightRateWhenOverstayExceedsNightMinStay()
    {
        // Caso solapamiento diurno -> nocturno:
        // Ingreso 8:00 COT (13:00 UTC). Plena de 12 horas (720 min) que ampara hasta las 20:00 COT.
        // Franja nocturna: 18:00 a 06:00 COT.
        // Mínimo de permanencia nocturna: 6 horas (360 min). Tarifa noche = 25000.
        // El vehículo se retira al día siguiente a las 03:00 COT (19 horas en total).
        // Excedente después de las 20:00 COT = 7 horas (420 min en la noche) >= 360 min mínimo nocturno.
        // Cobro esperado = 1 Plena diurna (18000) + 1 Tarifa nocturna (25000) = 43000.
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.AllowChargeByNight = true;
        _testBranchModel.NightStartTime = new TimeSpan(18, 0, 0);
        _testBranchModel.NightEndTime = new TimeSpan(6, 0, 0);
        _testBranchModel.NightStayMinMinutes = 360;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 18000m,
            FullDayThresholdMinutes = 180,
            FullDayCoverageMinutes = 720,
            NightRate = 25000m,
            NightStartTime = new TimeSpan(18, 0, 0),
            NightEndTime = new TimeSpan(6, 0, 0),
            NightStayMinMinutes = 360,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc); // 8:00 COT
        var exitUtc = entryUtc.AddHours(19); // 03:00 COT del día siguiente

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);

        fee.Should().Be(43000m);
    }

    [Fact]
    public async Task CalculateFee_FullDay_JsonRules_AppliesSegmentedThresholdAndCoveragePerDay()
    {
        // Reglas JSON de la sede: L-V umbral 8h (480 min) cobertura 12h (720 min). S-D umbral 4h (240 min) cobertura 8h (480 min).
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayRulesJson = "[{\"days\":\"1,2,3,4,5\",\"triggerMinutes\":480,\"coverageMinutes\":720},{\"days\":\"6,0\",\"triggerMinutes\":240,\"coverageMinutes\":480}]";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = 50m,
            HourRate = 3000m,
            FullDayRate = 20000m,
            GracePeriodMinutes = 0,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        // 1. Domingo (0): 5 horas de permanencia (300 min).
        // En fin de semana el umbral es 4h (240 min) y cobertura 8h (480 min).
        // 300 min supera 240 min -> aplica tarifa plena de 20000!
        var sundayEntryUtc = new DateTime(2026, 9, 13, 18, 0, 0, DateTimeKind.Utc);
        var sundayExitUtc = sundayEntryUtc.AddHours(5);
        var sundayFee = service.CalculateFee(VehicleType.Car, sundayEntryUtc, sundayExitUtc);
        sundayFee.Should().Be(20000m);

        // 2. Lunes (1): 5 horas de permanencia (300 min).
        // Entre semana el umbral es 8h (480 min).
        // 300 min NO alcanza 480 min -> cobra 5 horas * 3000 = 15000!
        var mondayEntryUtc = new DateTime(2026, 9, 14, 18, 0, 0, DateTimeKind.Utc);
        var mondayExitUtc = mondayEntryUtc.AddHours(5);
        var mondayFee = service.CalculateFee(VehicleType.Car, mondayEntryUtc, mondayExitUtc);
        mondayFee.Should().Be(15000m);
    }

    [Theory]
    // Martes (Weekday L-V: FullDay = $15.000, trigger=180m, coverage=480m, hora=$4.000, minuto=$66.67):
    [InlineData("2026-09-08T13:00:00Z", 10, 0)] // Gracia 15m -> $0
    [InlineData("2026-09-08T13:00:00Z", 60, 4000)] // 1 hora -> $4.000
    [InlineData("2026-09-08T13:00:00Z", 120, 8000)] // 2 horas -> $8.000
    [InlineData("2026-09-08T13:00:00Z", 180, 15000)] // 3 horas -> Plena L-V ($15.000)
    [InlineData("2026-09-08T13:00:00Z", 300, 15000)] // 5 horas -> Plena L-V ($15.000)
    [InlineData("2026-09-08T13:00:00Z", 480, 15000)] // 8 horas -> Plena L-V ($15.000)
    [InlineData("2026-09-08T13:00:00Z", 540, 19000)] // 9 horas (Plena 1 + 1 hora) -> $15.000 + $4.000 = $19.000
    [InlineData("2026-09-08T13:00:00Z", 600, 23000)] // 10 horas (Plena 1 + 2 horas) -> $15.000 + $8.000 = $23.000
    [InlineData("2026-09-08T13:00:00Z", 660, 30000)] // 11 horas (Plena 1 + Excedente superó trigger 3h) -> 2 * $15.000 = $30.000
    [InlineData("2026-09-08T13:00:00Z", 960, 30000)] // 16 horas (Límite cobertura Plena 2) -> $30.000
    [InlineData("2026-09-08T13:00:00Z", 1020, 34000)] // 17 horas (2 Plenas + 1 hora) -> $34.000
    // Sábado (Weekend S-D: FullDay = $25.000, trigger=240m, coverage=720m, hora=$4.000, minuto=$66.67):
    [InlineData("2026-09-12T13:00:00Z", 10, 0)] // Gracia 15m -> $0
    [InlineData("2026-09-12T13:00:00Z", 60, 4000)] // 1 hora -> $4.000
    [InlineData("2026-09-12T13:00:00Z", 120, 8000)] // 2 horas -> $8.000
    [InlineData("2026-09-12T13:00:00Z", 180, 12000)] // 3 horas (no alcanza trigger fin de semana de 4h) -> $12.000
    [InlineData("2026-09-12T13:00:00Z", 240, 25000)] // 4 horas (alcanza trigger fin de semana) -> Plena S-D ($25.000)
    [InlineData("2026-09-12T13:00:00Z", 480, 25000)] // 8 horas -> Plena S-D ($25.000)
    [InlineData("2026-09-12T13:00:00Z", 720, 25000)] // 12 horas (límite cobertura Plena 1) -> Plena S-D ($25.000)
    [InlineData("2026-09-12T13:00:00Z", 780, 29000)] // 13 horas (Plena 1 + 1 hora) -> $25.000 + $4.000 = $29.000
    [InlineData("2026-09-12T13:00:00Z", 960, 50000)] // 16 horas (Plena 1 + Excedente superó trigger 4h) -> 2 * $25.000 = $50.000
    [InlineData("2026-09-12T13:00:00Z", 1440, 50000)] // 24 horas (2 ciclos completos de 12h) -> $50.000
    public async Task CalculateFee_DynamicFullDayRatesJson_ResolvesExactRateByDayBlock(
        string entryTimeIso, int durationMinutes, decimal expectedFee)
    {
        _testBranchModel.AllowChargeByMinute = true;
        _testBranchModel.AllowChargeByHour = true;
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayRulesJson = @"[
            { ""days"": ""1,2,3,4,5"", ""triggerMinutes"": 180, ""coverageMinutes"": 480 },
            { ""days"": ""6,0"", ""triggerMinutes"": 240, ""coverageMinutes"": 720 }
        ]";

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            HourRate = 4000m,
            MinuteRate = 4000m / 60m,
            FullDayRate = 18000m,
            FullDayRatesJson = @"[
                { ""days"": ""1,2,3,4,5"", ""rate"": 15000 },
                { ""days"": ""6,0"", ""rate"": 25000 }
            ]",
            GracePeriodMinutes = 15,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = DateTime.Parse(entryTimeIso, null, System.Globalization.DateTimeStyles.RoundtripKind);
        var exitUtc = entryUtc.AddMinutes(durationMinutes);

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);
        fee.Should().Be(expectedFee);
    }

    [Theory]
    [InlineData("not a json")]
    [InlineData("[]")]
    [InlineData(@"[{ ""days"": ""6,0"", ""rate"": 25000 }]")] // Martes no hace match en este JSON
    public async Task CalculateFee_DynamicFullDayRatesJson_WhenMalformedOrUnmatched_FallsBackToDefaultFullDayRate(string jsonPayload)
    {
        _testBranchModel.AllowChargeByDay = true;
        _testBranchModel.FullDayApplicableDays = "All";
        _testBranchModel.FullDayThresholdMinutes = 180;
        _testBranchModel.FullDayRulesJson = null;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            HourRate = 4000m,
            FullDayRate = 18000m,
            FullDayThresholdMinutes = 180,
            FullDayCoverageMinutes = 480,
            FullDayRatesJson = jsonPayload,
            GracePeriodMinutes = 15,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = new DateTime(2026, 9, 8, 13, 0, 0, DateTimeKind.Utc); // Martes
        var exitUtc = entryUtc.AddMinutes(240); // 4h

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);
        fee.Should().Be(18000m);
    }

    [Theory]
    [MemberData(nameof(GetExtensivePricingStressScenarios))]
    public async Task CalculateFee_ExtensivePricingStressScenarios_CalculatesExactExpectedFee(
        int durationMinutes, decimal minuteRate, decimal hourRate, decimal fullDayRate, int graceMinutes, decimal expectedFee)
    {
        _testBranchModel.AllowChargeByMinute = minuteRate > 0;
        _testBranchModel.AllowChargeByHour = hourRate > 0;
        _testBranchModel.AllowChargeByDay = fullDayRate > 0;
        _testBranchModel.FullDayApplicableDays = "All";
        _testBranchModel.FullDayThresholdMinutes = fullDayRate > 0 ? 360 : 0;
        _testBranchModel.FullDayRulesJson = null;

        await SeedRateAsync(new VehicleRate
        {
            BranchId = 1,
            VehicleType = VehicleType.Car,
            MinuteRate = minuteRate,
            HourRate = hourRate,
            FullDayRate = fullDayRate,
            FullDayThresholdMinutes = fullDayRate > 0 ? 360 : null,
            FullDayCoverageMinutes = fullDayRate > 0 ? 1440 : null,
            GracePeriodMinutes = graceMinutes,
            IsActive = true
        });

        var service = new EfPricingCalculatorService(_connectionManager, _mockSyncEngine.Object, _mockSessionService.Object);
        await service.ReloadRatesAsync();

        var entryUtc = new DateTime(2026, 9, 7, 13, 0, 0, DateTimeKind.Utc);
        var exitUtc = entryUtc.AddMinutes(durationMinutes);

        var fee = service.CalculateFee(VehicleType.Car, entryUtc, exitUtc);
        fee.Should().Be(expectedFee);
    }

    public static IEnumerable<object[]> GetExtensivePricingStressScenarios()
    {
        // 1. Minuto a minuto en umbral de gracia (1 a 20 min con gracia de 15 min, $100/min)
        for (int m = 1; m <= 15; m++)
        {
            yield return new object[] { m, 100m, 6000m, 30000m, 15, 0m }; // Dentro de gracia
        }
        for (int m = 16; m <= 30; m++)
        {
            yield return new object[] { m, 100m, 0m, 0m, 15, m * 100m }; // Minuto exacto puro
        }

        // 2. Progresión horaria pura (1h a 24h a $5.000/hora, sin tarifa plena)
        for (int h = 1; h <= 24; h++)
        {
            yield return new object[] { h * 60, 0m, 5000m, 0m, 15, h * 5000m };
        }

        // 3. Progresión multi-día de 1 a 5 días con ciclos de 24h plena ($40.000/día, trigger 6h = 360m, coverage 24h = 1440m)
        for (int day = 1; day <= 5; day++)
        {
            // Exactamente el final de cada día completo (24h, 48h, 72h, 96h, 120h)
            yield return new object[] { day * 1440, 0m, 5000m, 40000m, 15, day * 40000m };
            // Cada día + 2 horas (excedente de 120m no alcanza umbral de 6h -> cobra horas normales)
            yield return new object[] { (day * 1440) + 120, 0m, 5000m, 40000m, 15, (day * 40000m) + 10000m };
            // Cada día + 7 horas (excedente de 420m superó umbral de 6h -> cobra nuevo día pleno completo)
            yield return new object[] { (day * 1440) + 420, 0m, 5000m, 40000m, 15, (day + 1) * 40000m };
        }

        // 4. Fracciones con minutos y horas combinadas (ej: 1h30m, 2h30m, etc.)
        for (int h = 1; h <= 10; h++)
        {
            int mins = (h * 60) + 30;
            // 30 mins a $100/min = $3.000 + h * $5.000
            yield return new object[] { mins, 100m, 5000m, 0m, 15, (h * 5000m) + 3000m };
        }
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
