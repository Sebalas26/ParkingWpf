using System;
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

namespace Parking.UnitTests.Shifts;

public class EfShiftServiceTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly BranchModel _testBranchModel;

    public EfShiftServiceTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockApiClient = new Mock<IApiClientService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockSessionService = new Mock<ISessionService>();

        _testBranchModel = new BranchModel
        {
            Id = 1,
            Name = "Sede Centro",
            TotalCapacity = 40
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentCompanyId).Returns(5);

        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            ServerUserId = 1,
            Username = "operador",
            FullName = "Operador de Prueba"
        });

        using var db = _connectionManager.CreateDbContext();
        db.Branches.Add(new Branch
        {
            Id = 1,
            CompanyId = 5,
            Name = "Sede Centro",
            TotalCapacity = 40
        });
        db.SaveChanges();
    }

    private EfShiftService CreateService()
    {
        return new EfShiftService(
            _connectionManager,
            _mockApiClient.Object,
            _mockAuthService.Object,
            _mockSessionService.Object);
    }

    [Fact]
    public async Task OpenShiftAsync_ValidBaseAmount_CreatesActiveShift()
    {
        // Arrange
        var service = CreateService();

        // Act
        var shift = await service.OpenShiftAsync(baseAmount: 100000m, notes: "Base inicial en efectivo");

        // Assert
        shift.Should().NotBeNull();
        shift.Status.Should().Be(0); // Activo
        shift.BaseAmount.Should().Be(100000m);
        shift.BranchId.Should().Be(1);
        shift.CompanyId.Should().Be(5);
        service.HasActiveShift.Should().BeTrue();
        service.CurrentShift.Should().Be(shift);
    }

    [Fact]
    public async Task GetCurrentShiftSummaryAsync_CalculatesCashExpectedAndDifferenceCorrectly()
    {
        // Arrange
        var service = CreateService();
        var shift = await service.OpenShiftAsync(baseAmount: 50000m);

        shift.StartTimeUtc = DateTime.UtcNow.AddHours(-1);
        using (var db = _connectionManager.CreateDbContext())
        {
            var dbShift = await db.WorkShifts.FindAsync(shift.ShiftId);
            if (dbShift != null)
            {
                dbShift.StartTimeUtc = DateTime.UtcNow.AddHours(-1);
            }

            // Simular tiquete pagado en efectivo de 10.000 dentro del turno
            db.ParkingTickets.Add(new ParkingTicket
            {
                TicketId = Guid.NewGuid(),
                BranchId = 1,
                CompanyId = 5,
                TicketNumber = "T-001",
                PlateNumber = "ABC123",
                Status = TicketStatus.Completed,
                PaymentMethod = PaymentMethod.Cash,
                GrossAmount = 10000m,
                NetAmount = 10000m,
                AmountPaid = 10000m,
                EntryTimeUtc = DateTime.UtcNow.AddMinutes(-45),
                ExitTimeUtc = DateTime.UtcNow.AddMinutes(-15)
            });

            // Simular retiro parcial de caja de 20.000
            db.CashWithdrawals.Add(new CashWithdrawal
            {
                WithdrawalId = Guid.NewGuid(),
                ShiftId = shift.ShiftId,
                Amount = 20000m,
                Reason = "Entrega a administración",
                AuthorizedByAdminName = "Administrador",
                CashierName = "Operador",
                CreatedAtUtc = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // Act
        var summary = await service.GetCurrentShiftSummaryAsync();

        // Assert:
        // Base = 50.000, Efectivo cobrado = 10.000, Retiros = 20.000
        // Esperado en caja = 50.000 + 10.000 - 20.000 = 40.000
        summary.Should().NotBeNull();
        summary.BaseAmount.Should().Be(50000m);
        summary.TotalCashCollected.Should().Be(10000m);
        summary.TotalCashWithdrawals.Should().Be(20000m);
        summary.ExpectedCash.Should().Be(40000m);
    }

    [Fact]
    public async Task CloseShiftAsync_ClosesShiftAndPersistsDiscrepancy()
    {
        // Arrange
        var service = CreateService();
        await service.OpenShiftAsync(baseAmount: 50000m);

        // Act: En el conteo físico hay 45.000 (un descuadre / faltante de 5.000)
        var closedShift = await service.CloseShiftAsync(actualCashCounted: 45000m, notes: "Arqueo con faltante de 5000");

        // Assert
        closedShift.Should().NotBeNull();
        closedShift!.Status.Should().Be(1); // Cerrado
        closedShift.ActualCashCounted.Should().Be(45000m);
        closedShift.ExpectedCash.Should().Be(50000m);
        closedShift.CashDifference.Should().Be(-5000m); // Faltante

        service.HasActiveShift.Should().BeFalse();
        service.CurrentShift.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCashWithdrawalAsync_PersistsWithdrawalInDatabase()
    {
        // Arrange
        var service = CreateService();
        var shift = await service.OpenShiftAsync(baseAmount: 100000m);

        // Act
        var withdrawal = await service.RegisterCashWithdrawalAsync(
            shift.ShiftId,
            amount: 30000m,
            reason: "Retiro para consignación",
            authorizedByAdminName: "Supervisor General",
            cashierName: "Cajero 1");

        // Assert
        withdrawal.Should().NotBeNull();
        withdrawal.Amount.Should().Be(30000m);
        withdrawal.ShiftId.Should().Be(shift.ShiftId);

        using var db = _connectionManager.CreateDbContext();
        var saved = await db.CashWithdrawals.FindAsync(withdrawal.WithdrawalId);
        saved.Should().NotBeNull();
        saved!.Amount.Should().Be(30000m);
    }

    [Fact]
    public async Task RefreshCurrentShiftAsync_WhenShiftClosedRemotely_SetsCurrentShiftToNullAndFiresEvent()
    {
        // Arrange
        var service = CreateService();
        await service.OpenShiftAsync(baseAmount: 50000m);
        service.HasActiveShift.Should().BeTrue();

        bool eventFired = false;
        service.ShiftStateChanged += () => eventFired = true;

        // Simular que el API retorna null porque se cerró desde PWA
        _mockApiClient.Setup(a => a.GetActiveShiftAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync((WorkShift?)null);

        // Act
        await service.RefreshCurrentShiftAsync();

        // Assert
        service.CurrentShift.Should().BeNull();
        service.HasActiveShift.Should().BeFalse();
        eventFired.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshCurrentShiftAsync_WhenShiftActiveInApi_UpdatesCurrentShiftAndPersistsInLocalDb()
    {
        // Arrange
        var service = CreateService();
        var shiftId = Guid.NewGuid();
        var remoteShift = new WorkShift
        {
            ShiftId = shiftId,
            BranchId = 1,
            CompanyId = 5,
            UserId = 1,
            OperatorName = "Carlos Operador",
            Status = 0,
            BaseAmount = 80000m,
            StartTimeUtc = DateTime.UtcNow
        };

        _mockApiClient.Setup(a => a.GetActiveShiftAsync(It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(remoteShift);

        // Act
        await service.RefreshCurrentShiftAsync();

        // Assert
        service.CurrentShift.Should().NotBeNull();
        service.CurrentShift!.ShiftId.Should().Be(shiftId);
        service.CurrentShift.OperatorName.Should().Be("Carlos Operador");
        service.HasActiveShift.Should().BeTrue();

        using var db = _connectionManager.CreateDbContext();
        var savedInSqlite = await db.WorkShifts.FindAsync(shiftId);
        savedInSqlite.Should().NotBeNull();
        savedInSqlite!.Status.Should().Be(0);
        savedInSqlite.BaseAmount.Should().Be(80000m);
    }

    [Theory]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000001\",\"operatorName\":\"Test\",\"status\":\"Open\"}", 0)]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000002\",\"operatorName\":\"Test\",\"status\":\"Closed\"}", 1)]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000003\",\"operatorName\":\"Test\",\"status\":0}", 0)]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000004\",\"operatorName\":\"Test\",\"status\":1}", 1)]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000005\",\"operatorName\":\"Test\",\"status\":\"0\"}", 0)]
    [InlineData("{\"shiftId\":\"a3b2c1d0-0000-0000-0000-000000000006\",\"operatorName\":\"Test\",\"status\":\"1\"}", 1)]
    public void WorkShift_JsonDeserialization_StatusStringOrNumber_DeserializesCorrectly(string json, int expectedStatus)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var shift = System.Text.Json.JsonSerializer.Deserialize<WorkShift>(json, options);

        shift.Should().NotBeNull();
        shift!.Status.Should().Be(expectedStatus);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
