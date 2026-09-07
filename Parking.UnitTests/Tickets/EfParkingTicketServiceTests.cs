using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Tickets;

public class EfParkingTicketServiceTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<IPricingCalculatorService> _mockPricingCalculator;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly BranchModel _testBranchModel;

    public EfParkingTicketServiceTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockPricingCalculator = new Mock<IPricingCalculatorService>();
        _mockApiClient = new Mock<IApiClientService>();
        _mockSyncEngine = new Mock<ISyncEngineService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockConfiguration = new Mock<IConfiguration>();

        _testBranchModel = new BranchModel
        {
            Id = 1,
            Name = "Sede Principal",
            TotalCapacity = 50,
            LostTicketFee = 15000m
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentCompanyId).Returns(10);
        _mockSyncEngine.Setup(s => s.IsOnline).Returns(false); // Operación local segura

        using var db = _connectionManager.CreateDbContext();
        db.Branches.Add(new Branch
        {
            Id = 1,
            CompanyId = 10,
            Name = "Sede Principal",
            TotalCapacity = 50,
            LostTicketFee = 15000m
        });
        db.SaveChanges();
    }

    private EfParkingTicketService CreateService()
    {
        return new EfParkingTicketService(
            _connectionManager,
            _mockPricingCalculator.Object,
            _mockApiClient.Object,
            _mockSyncEngine.Object,
            _mockSessionService.Object,
            _mockConfiguration.Object);
    }

    [Fact]
    public async Task RegisterEntryAsync_ValidInput_RegistersActiveTicket()
    {
        // Arrange
        var service = CreateService();
        _mockPricingCalculator.Setup(p => p.GetRate(VehicleType.Car, null))
            .Returns(new VehicleRate { HourRate = 3000m });

        // Act
        var ticket = await service.RegisterEntryAsync(
            plateNumber: "ABC123",
            vehicleType: VehicleType.Car,
            phoneNumber: "3001234567",
            notes: "Sin rayones",
            operatorName: "Carlos Operador");

        // Assert
        ticket.Should().NotBeNull();
        ticket.PlateNumber.Should().Be("ABC123");
        ticket.Status.Should().Be(TicketStatus.Active);
        ticket.BranchId.Should().Be(1);
        ticket.CompanyId.Should().Be(10);
        ticket.TicketNumber.Should().StartWith("PKF-C10-");

        using var db = _connectionManager.CreateDbContext();
        var savedTicket = await db.ParkingTickets.FindAsync(ticket.TicketId);
        savedTicket.Should().NotBeNull();
        savedTicket!.PlateNumber.Should().Be("ABC123");
    }

    [Fact]
    public async Task RegisterEntryAsync_AlreadyParkedPlate_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = CreateService();
        await service.RegisterEntryAsync("XYZ789", VehicleType.Car, null, null, "Operador 1");

        // Act & Assert
        Func<Task> act = async () => await service.RegisterEntryAsync("XYZ789", VehicleType.Car, null, null, "Operador 2");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ya se encuentra registrado y activo adentro*");
    }

    [Fact]
    public async Task RegisterEntryAsync_BlockedPlateIncident_ThrowsInvalidOperationException()
    {
        // Arrange
        using (var db = _connectionManager.CreateDbContext())
        {
            db.VehicleIncidents.Add(new VehicleIncident
            {
                IncidentId = Guid.NewGuid(),
                BranchId = 1,
                CompanyId = 10,
                PlateNumber = "BLO001",
                IncidentType = "HURTO",
                Description = "Vehículo investigado por hurto",
                IsBlocked = true,
                Status = "Activa",
                ReportedBy = "Seguridad",
                CreatedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () => await service.RegisterEntryAsync("BLO001", VehicleType.Car, null, null, "Operador");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*VEHÍCULO BLOQUEADO*");
    }

    [Fact]
    public async Task RegisterEntryAsync_OutsideOperatingHours_GeneratesExtemporaneousIncidentWithoutBlocking()
    {
        // Arrange: Sede cerrada hoy
        var cotNow = DateTime.UtcNow.AddHours(-5);
        using (var db = _connectionManager.CreateDbContext())
        {
            db.BranchOperatingHours.Add(new BranchOperatingHour
            {
                BranchId = 1,
                DayOfWeek = cotNow.DayOfWeek,
                IsOpen = false, // Cerrado hoy
                OpeningTime = new TimeSpan(8, 0, 0),
                ClosingTime = new TimeSpan(18, 0, 0),
                BufferMinutesBefore = 0,
                BufferMinutesAfter = 0
            });
            await db.SaveChangesAsync();
        }

        var service = CreateService();

        // Act
        var ticket = await service.RegisterEntryAsync("EXT999", VehicleType.Car, null, null, "Operador Noche");

        // Assert: El tiquete se registra correctamente (no se bloquea la barrera)
        ticket.Should().NotBeNull();
        ticket.Status.Should().Be(TicketStatus.Active);

        // Pero se registró silenciosamente un incidente de auditoría
        using (var db = _connectionManager.CreateDbContext())
        {
            var incident = await db.VehicleIncidents
                .FirstOrDefaultAsync(i => i.PlateNumber == "EXT999" && i.IncidentType == "INGRESO_EXTEMPORANEO");
            incident.Should().NotBeNull();
            incident!.IsBlocked.Should().BeFalse();
            incident.Status.Should().Be("Activa");
        }
    }

    [Fact]
    public async Task ProcessExitAsync_ValidTicket_MarksCompletedAndCalculatesNet()
    {
        // Arrange
        var service = CreateService();
        var ticket = await service.RegisterEntryAsync("SAL123", VehicleType.Car, null, null, "Operador");

        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, false))
            .Returns(5000m);

        // Act: Salida con descuento de 1.000 y pago de 5.000
        var completed = await service.ProcessExitAsync(
            ticket.TicketId,
            PaymentMethod.Cash,
            amountPaid: 5000m,
            storeId: null,
            agreementId: null,
            invoiceNumber: null,
            purchaseAmount: null,
            discountAmount: 1000m);

        // Assert
        completed.Should().NotBeNull();
        completed!.Status.Should().Be(TicketStatus.Completed);
        completed.GrossAmount.Should().Be(5000m);
        completed.DiscountAmount.Should().Be(1000m);
        completed.NetAmount.Should().Be(4000m);
        completed.AmountPaid.Should().Be(5000m);
        completed.ChangeGiven.Should().Be(1000m);
    }

    [Fact]
    public async Task ProcessExitAsync_WithLostTicket_PersistsIsLostTicketAndFee()
    {
        // Arrange
        var service = CreateService();
        var ticket = await service.RegisterEntryAsync("PER777", VehicleType.Car, null, null, "Operador");

        _mockPricingCalculator.Setup(p => p.CalculateFee(VehicleType.Car, It.IsAny<DateTime>(), It.IsAny<DateTime>(), 0, true))
            .Returns(20000m); // 5.000 estancia + 15.000 tiquete perdido

        // Act
        var completed = await service.ProcessExitAsync(
            ticket.TicketId,
            PaymentMethod.Cash,
            amountPaid: 20000m,
            storeId: null,
            agreementId: null,
            invoiceNumber: null,
            purchaseAmount: null,
            discountAmount: 0m,
            isLostTicket: true,
            lostTicketFee: 15000m);

        // Assert
        completed.Should().NotBeNull();
        completed!.IsLostTicket.Should().BeTrue();
        completed.LostTicketFee.Should().Be(15000m);
        completed.GrossAmount.Should().Be(20000m);
        completed.NetAmount.Should().Be(20000m);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
