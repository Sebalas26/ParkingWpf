using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Shifts;

public class ShiftFlowSimulationTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly BranchModel _testBranchModel;

    public ShiftFlowSimulationTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockApiClient = new Mock<IApiClientService>();
        _mockAuthService = new Mock<IAuthService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockSyncEngine = new Mock<ISyncEngineService>();
        _mockServiceProvider = new Mock<IServiceProvider>();

        _mockSyncEngine.Setup(s => s.IsOnline).Returns(false);
        _mockServiceProvider.Setup(sp => sp.GetService(typeof(ISyncEngineService))).Returns(_mockSyncEngine.Object);

        _testBranchModel = new BranchModel
        {
            Id = 1,
            CompanyId = 5,
            Name = "Sede Norte",
            TotalCapacity = 50
        };

        _mockSessionService.Setup(s => s.CurrentBranch).Returns(_testBranchModel);
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentCompanyId).Returns(5);

        using var db = _connectionManager.CreateDbContext();
        db.Branches.Add(new Branch
        {
            Id = 1,
            CompanyId = 5,
            Name = "Sede Norte",
            TotalCapacity = 50
        });
        db.SaveChanges();
    }

    private EfShiftService CreateService()
    {
        return new EfShiftService(
            _connectionManager,
            _mockApiClient.Object,
            _mockAuthService.Object,
            _mockSessionService.Object,
            _mockServiceProvider.Object);
    }

    [Fact]
    public async Task Simulation_OfflineLogin_WithExistingShift_IdentifiesOwnShiftAndExcludesFromOtherShifts()
    {
        // Arrange: En SQLite existe un turno abierto del usuario "Miguel Gutierrez Caja" con ServerUserId = 5
        var shiftId = Guid.NewGuid();
        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = shiftId,
                BranchId = 1,
                CompanyId = 5,
                UserId = 5,
                OperatorName = "Miguel Gutierrez Caja",
                CashRegisterName = "Caja Principal",
                BaseAmount = 100000m,
                Status = 0,
                StartTimeUtc = DateTime.UtcNow.AddHours(-2)
            });
            await db.SaveChangesAsync();
        }

        // Usuario autenticado en modo offline con ServerUserId = 5
        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            ServerUserId = 5,
            Username = "mgutierrez",
            FullName = "Miguel Gutierrez Caja"
        });

        var service = CreateService();

        // Act: Obtener el turno activo del usuario y los turnos de la sede para relevo
        var myShift = await service.GetActiveShiftAsync();
        var otherShifts = await service.GetActiveShiftsByBranchAsync(1);

        // Assert:
        // 1. Debe reconocer SU turno propio
        myShift.Should().NotBeNull();
        myShift!.ShiftId.Should().Be(shiftId);
        myShift.OperatorName.Should().Be("Miguel Gutierrez Caja");

        // 2. La lista de otras cajas NO debe contener su propio turno (evita el bug de relevarse a sí mismo)
        otherShifts.Should().BeEmpty();
    }

    [Fact]
    public async Task Simulation_CleanBranch_ZeroShifts_ReturnsEmptyOtherShifts()
    {
        // Arrange: Sede sin ningún turno abierto
        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            ServerUserId = 10,
            Username = "nuevo_operador",
            FullName = "Nuevo Operador"
        });

        var service = CreateService();

        // Act
        var activeShift = await service.GetActiveShiftAsync();
        var branchShifts = await service.GetActiveShiftsByBranchAsync(1);

        // Assert
        activeShift.Should().BeNull();
        branchShifts.Should().BeEmpty();
    }

    [Fact]
    public async Task Simulation_BranchWithOtherOperatorShift_ExcludesOwnAndReturnsOnlyOtherOperatorShift()
    {
        // Arrange: En la sede hay 2 turnos abiertos:
        // 1. Turno de Carlos Operador (UserId = 20)
        // 2. Turno del usuario actual Miguel Gutierrez (UserId = 5)
        var carlosShiftId = Guid.NewGuid();
        var miguelShiftId = Guid.NewGuid();

        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = carlosShiftId,
                BranchId = 1,
                CompanyId = 5,
                UserId = 20,
                OperatorName = "Carlos Operador",
                CashRegisterName = "Caja 1",
                BaseAmount = 80000m,
                Status = 0,
                StartTimeUtc = DateTime.UtcNow.AddHours(-4)
            });

            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = miguelShiftId,
                BranchId = 1,
                CompanyId = 5,
                UserId = 5,
                OperatorName = "Miguel Gutierrez",
                CashRegisterName = "Caja 2",
                BaseAmount = 120000m,
                Status = 0,
                StartTimeUtc = DateTime.UtcNow.AddHours(-1)
            });

            await db.SaveChangesAsync();
        }

        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            ServerUserId = 5,
            Username = "mgutierrez",
            FullName = "Miguel Gutierrez"
        });

        var service = CreateService();

        // Act
        var ownShift = await service.GetActiveShiftAsync();
        var otherShifts = await service.GetActiveShiftsByBranchAsync(1);

        // Assert:
        // Turno propio es el de Miguel
        ownShift.Should().NotBeNull();
        ownShift!.ShiftId.Should().Be(miguelShiftId);

        // La lista de otras cajas solo debe incluir la de Carlos, NO la de Miguel
        otherShifts.Should().HaveCount(1);
        otherShifts[0].ShiftId.Should().Be(carlosShiftId);
        otherShifts[0].OperatorName.Should().Be("Carlos Operador");
    }

    [Fact]
    public async Task Simulation_OfflineOpenShift_WithoutServerUserId_AssignsNegativeUniqueIdAndPreventsCollision()
    {
        // Arrange: Dos usuarios locales distintos que nunca se han conectado online (ServerUserId = null)
        var userGuid1 = Guid.NewGuid();
        var userGuid2 = Guid.NewGuid();

        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            UserId = userGuid1,
            ServerUserId = null,
            Username = "operador1",
            FullName = "Operador Local Uno"
        });

        var service1 = CreateService();
        var shift1 = await service1.OpenShiftAsync(baseAmount: 50000m, cashRegisterName: "Caja Offline 1");

        _mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel
        {
            UserId = userGuid2,
            ServerUserId = null,
            Username = "operador2",
            FullName = "Operador Local Dos"
        });

        var service2 = CreateService();
        var shift2 = await service2.OpenShiftAsync(baseAmount: 50000m, cashRegisterName: "Caja Offline 2");

        // Assert:
        // Ambos turnos deben tener UserId negativo (no 1) y distintos entre sí
        shift1.UserId.Should().BeNegative();
        shift2.UserId.Should().BeNegative();
        shift1.UserId.Should().NotBe(1);
        shift2.UserId.Should().NotBe(1);
        shift1.UserId.Should().NotBe(shift2.UserId);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
    }
}
