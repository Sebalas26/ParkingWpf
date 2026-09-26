using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
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

    [Fact]
    public async Task OpenShiftAsync_WhenServerThrowsInvalidOperationException_PropagatesException()
    {
        // Arrange
        var service = CreateService();
        _mockApiClient.Setup(a => a.OpenShiftAsync(It.IsAny<OpenShiftApiRequest>()))
            .ThrowsAsync(new InvalidOperationException("Monto base obligatorio"));

        // Act
        var act = async () => await service.OpenShiftAsync(50000m);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Monto base obligatorio");
        service.CurrentShift.Should().BeNull();
    }

    [Fact]
    public async Task OpenShiftAsync_WhenSuccessful_SendsUserIdAndSavesAsSynchronized()
    {
        // Arrange
        var service = CreateService();
        OpenShiftApiRequest? capturedRequest = null;
        var remoteShift = new WorkShift
        {
            ShiftId = Guid.NewGuid(),
            BranchId = 1,
            CompanyId = 5,
            UserId = 1,
            OperatorName = "Operador de Prueba",
            Status = 0,
            BaseAmount = 70000m,
            IsSynchronized = true
        };

        _mockApiClient.Setup(a => a.OpenShiftAsync(It.IsAny<OpenShiftApiRequest>()))
            .Callback<OpenShiftApiRequest>(r => capturedRequest = r)
            .ReturnsAsync(remoteShift);

        // Act
        var result = await service.OpenShiftAsync(70000m);

        // Assert
        result.Should().NotBeNull();
        capturedRequest.Should().NotBeNull();
        capturedRequest!.UserId.Should().Be(1); // ServerUserId from mockAuth
        result.IsSynchronized.Should().BeTrue();
        service.HasActiveShift.Should().BeTrue();
    }

    [Fact]
    public async Task OpenShiftAsync_WhenProxyOrFirewallBlocksWith403_FallsBackToLocalShiftCreation()
    {
        // Arrange
        var service = CreateService();
        _mockApiClient.Setup(a => a.OpenShiftAsync(It.IsAny<OpenShiftApiRequest>()))
            .ThrowsAsync(new InvalidOperationException("Error del servidor al abrir turno (Forbidden)"));

        // Act
        var result = await service.OpenShiftAsync(50000m, "Apertura con firewall");

        // Assert
        result.Should().NotBeNull();
        result.BaseAmount.Should().Be(50000m);
        result.IsSynchronized.Should().BeFalse();
        service.HasActiveShift.Should().BeTrue();
        service.CurrentShift.Should().Be(result);
    }

    [Fact]
    public async Task OpenShiftAsync_WithCashRegisterName_PersistsCashRegisterName()
    {
        // Arrange
        var service = CreateService();

        // Act
        var shift = await service.OpenShiftAsync(50000m, "Apertura caja 2", "Caja 2");

        // Assert
        shift.Should().NotBeNull();
        shift.CashRegisterName.Should().Be("Caja 2");
    }

    [Fact]
    public async Task GetActiveShiftsByBranchAsync_ReturnsAllActiveShiftsForBranch()
    {
        // Arrange
        var service = CreateService();
        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = Guid.NewGuid(),
                BranchId = 1,
                CompanyId = 5,
                UserId = 10,
                OperatorName = "Otro Operador",
                CashRegisterName = "Caja 1",
                Status = 0,
                BaseAmount = 50000m,
                StartTimeUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Mock API client returns empty or fails so it falls back to local SQLite
        _mockApiClient.Setup(a => a.GetActiveShiftsAsync(1))
            .ThrowsAsync(new InvalidOperationException("Offline"));

        // Act
        var shifts = await service.GetActiveShiftsByBranchAsync(1);

        // Assert
        shifts.Should().NotBeEmpty();
        shifts.Should().Contain(s => s.OperatorName == "Otro Operador" && s.CashRegisterName == "Caja 1");
    }

    [Fact]
    public async Task RefreshCurrentShiftAsync_WhenUserHasNoShift_ReturnsNullEvenIfOtherUserHasShiftInBranch()
    {
        // Arrange
        var service = CreateService();
        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = Guid.NewGuid(),
                BranchId = 1,
                CompanyId = 5,
                UserId = 99, // Another user
                OperatorName = "Usuario Diferente",
                CashRegisterName = "Caja Entrada",
                Status = 0,
                BaseAmount = 40000m,
                StartTimeUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Mock API returns null for current user (UserId 1)
        _mockApiClient.Setup(a => a.GetActiveShiftAsync(1, 1))
            .ReturnsAsync((WorkShift?)null);

        // Act
        await service.RefreshCurrentShiftAsync();

        // Assert
        service.HasActiveShift.Should().BeFalse();
        service.CurrentShift.Should().BeNull();
    }

    [Fact]
    public async Task CloseSpecificShiftAsync_ClosesTargetShift()
    {
        // Arrange
        var service = CreateService();
        var targetShiftId = Guid.NewGuid();
        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = targetShiftId,
                BranchId = 1,
                CompanyId = 5,
                UserId = 15,
                OperatorName = "Operador a Cerrar",
                CashRegisterName = "Caja Norte",
                Status = 0,
                BaseAmount = 30000m,
                StartTimeUtc = DateTime.UtcNow.AddHours(-3)
            });
            await db.SaveChangesAsync();
        }

        // Act
        var closedShift = await service.CloseSpecificShiftAsync(targetShiftId, 30000m, "Cierre administrativo");

        // Assert
        closedShift.Should().NotBeNull();
        closedShift.Status.Should().Be(1); // Cerrado
        closedShift.ActualCashCounted.Should().Be(30000m);

        using (var db = _connectionManager.CreateDbContext())
        {
            var dbShift = await db.WorkShifts.FindAsync(targetShiftId);
            dbShift.Should().NotBeNull();
            dbShift!.Status.Should().Be(1);
        }
    }

    [Fact]
    public async Task HandoverAndOpenNextShiftAsync_ClosesOutgoingShiftAndOpensIncomingShiftWithVerifiedCashBase()
    {
        // Arrange
        var service = CreateService();
        var outgoingShift = await service.OpenShiftAsync(50000m, "Turno Mañana", "Caja 1");
        var incomingUserId = Guid.NewGuid();
        var incomingUserName = "Carlos Relevo";
        var verifiedCashAmount = 85000m;

        // Act
        var nextShift = await service.HandoverAndOpenNextShiftAsync(
            actualCashCounted: verifiedCashAmount,
            notes: "Relevo entregado con balance cuadrado",
            handoverToUserId: incomingUserId,
            handoverToUserName: incomingUserName,
            newShiftBaseAmount: verifiedCashAmount);

        // Assert
        nextShift.Should().NotBeNull();
        nextShift.Status.Should().Be(0); // Nuevo turno activo
        nextShift.BaseAmount.Should().Be(verifiedCashAmount);
        nextShift.OperatorName.Should().Be(incomingUserName);
        nextShift.CashRegisterName.Should().Be("Caja 1");
        service.CurrentShift.Should().Be(nextShift);

        // Verificar en base de datos que el turno saliente quedó cerrado con handover
        using var db = _connectionManager.CreateDbContext();
        var closedShift = await db.WorkShifts.FindAsync(outgoingShift.ShiftId);
        closedShift.Should().NotBeNull();
        closedShift!.Status.Should().Be(1); // Cerrado
        closedShift.ActualCashCounted.Should().Be(verifiedCashAmount);
        closedShift.HandoverToUserId.Should().Be(incomingUserId);
        closedShift.HandoverToUserName.Should().Be(incomingUserName);
    }

    [Fact]
    public async Task HandoverAndOpenNextShiftAsync_SpecificShiftId_ClosesTargetShiftAndOpensNewShiftForIncomingOperator()
    {
        // Arrange
        var service = CreateService();
        var targetShiftId = Guid.NewGuid();
        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = targetShiftId,
                BranchId = 1,
                CompanyId = 5,
                UserId = 12,
                OperatorName = "Operador Antiguo",
                CashRegisterName = "Caja Secundaria",
                Status = 0,
                BaseAmount = 40000m,
                StartTimeUtc = DateTime.UtcNow.AddHours(-4)
            });
            await db.SaveChangesAsync();
        }

        var incomingUserId = Guid.NewGuid();
        var incomingUserName = "María Auxiliar";
        var verifiedCash = 62000m;

        // Act
        var newShift = await service.HandoverAndOpenNextShiftAsync(
            actualCashCounted: verifiedCash,
            notes: "Relevo asumido por María",
            handoverToUserId: incomingUserId,
            handoverToUserName: incomingUserName,
            newShiftBaseAmount: verifiedCash,
            shiftIdToClose: targetShiftId,
            newCashRegisterName: "Caja Secundaria");

        // Assert
        newShift.Should().NotBeNull();
        newShift.Status.Should().Be(0);
        newShift.BaseAmount.Should().Be(verifiedCash);
        newShift.OperatorName.Should().Be(incomingUserName);
        newShift.CashRegisterName.Should().Be("Caja Secundaria");

        using (var db = _connectionManager.CreateDbContext())
        {
            var dbTarget = await db.WorkShifts.FindAsync(targetShiftId);
            dbTarget.Should().NotBeNull();
            dbTarget!.Status.Should().Be(1);
            dbTarget.HandoverToUserId.Should().Be(incomingUserId);
            dbTarget.HandoverToUserName.Should().Be(incomingUserName);
            dbTarget.ActualCashCounted.Should().Be(verifiedCash);
        }
    }

    [Fact]
    public async Task HandoverAndOpen_EmitsShiftStateChanged_OnlyOnce()
    {
        // Arrange
        var service = CreateService();
        await service.OpenShiftAsync(50000m, "Turno Principal", "Caja 1");
        int eventCount = 0;
        service.ShiftStateChanged += () => eventCount++;

        // Act
        await service.HandoverAndOpenNextShiftAsync(
            actualCashCounted: 75000m,
            notes: "Relevo sin parpadeo",
            handoverToUserId: Guid.NewGuid(),
            handoverToUserName: "Operador Relevo",
            newShiftBaseAmount: 75000m);

        // Assert: solo debe emitirse una única vez al final del proceso completo
        eventCount.Should().Be(1);
    }

    [Fact]
    public async Task HandoverAndOpen_ThrowsWhenUserIdCannotBeResolved()
    {
        // Arrange
        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            ServerUserId = null,
            Username = "usuario_sin_id",
            FullName = "Usuario Sin Id"
        });
        var service = CreateService();

        // Act & Assert
        Func<Task> act = async () =>
        {
            await service.HandoverAndOpenNextShiftAsync(
                actualCashCounted: 50000m,
                notes: "Prueba sin user id",
                handoverToUserId: Guid.NewGuid(),
                handoverToUserName: "Desconocido Total",
                newShiftBaseAmount: 50000m);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No se pudo resolver el identificador de usuario*");
    }

    [Fact]
    public async Task HandoverAndOpen_ResolvesUserIdFromUserEntity_WhenAvailableInSQLite()
    {
        // Arrange
        var incomingUserGuid = Guid.NewGuid();
        var incomingName = "Carlos Relevo";
        const int expectedServerUserId = 42;

        using (var db = _connectionManager.CreateDbContext())
        {
            var role = new Role { RoleId = Guid.NewGuid(), Name = "Cajero", Description = "Cajero" };
            db.Roles.Add(role);
            db.Users.Add(new User
            {
                UserId = incomingUserGuid,
                Username = "carlos.relevo",
                FullName = incomingName,
                ServerUserId = expectedServerUserId,
                RoleId = role.RoleId,
                IsActive = true
            });
            await db.SaveChangesAsync();
        }

        // Simular que el usuario saliente es diferente y tiene otro id
        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            ServerUserId = 999,
            Username = "operador.saliente",
            FullName = "Operador Saliente"
        });

        var service = CreateService();

        // Act
        var nextShift = await service.HandoverAndOpenNextShiftAsync(
            actualCashCounted: 50000m,
            notes: "Relevo a Carlos",
            handoverToUserId: incomingUserGuid,
            handoverToUserName: incomingName,
            newShiftBaseAmount: 50000m);

        // Assert
        nextShift.Should().NotBeNull();
        nextShift.UserId.Should().Be(expectedServerUserId);
        nextShift.OperatorName.Should().Be(incomingName);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
