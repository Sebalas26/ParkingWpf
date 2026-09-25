using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Moq.Protected;
using Parking.Core.Enums;
using Parking.Entities;
using Parking.Models;
using Parking.Models.ApiModels;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Services;

public class OfflineResilienceTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IShiftService> _mockShiftService;
    private readonly Mock<ISignalRClientService> _mockSignalRClient;

    public OfflineResilienceTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _mockApiClient = new Mock<IApiClientService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockShiftService = new Mock<IShiftService>();
        _mockSignalRClient = new Mock<ISignalRClientService>();
    }

    [Fact]
    public void SyncEngineService_SetOnlineStatus_False_UpdatesStateAndNotifies()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        string? notifiedStatus = null;
        syncEngine.SyncStatusChanged += (s, status) => notifiedStatus = status;

        // Act
        syncEngine.SetOnlineStatus(false);

        // Assert
        syncEngine.IsOnline.Should().BeFalse();
        syncEngine.SyncStatusDescription.Should().Contain("Modo Offline");
        notifiedStatus.Should().Contain("Modo Offline");
    }

    [Fact]
    public void SyncEngineService_WhenApiClientReportsConnectionDrop_SwitchesToOfflineImmediately()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act - simular que ParkingApiClient detecta caída de red y dispara ConnectionStateChanged(false)
        _mockApiClient.Raise(a => a.ConnectionStateChanged += null, false);

        // Assert
        syncEngine.IsOnline.Should().BeFalse();
        syncEngine.SyncStatusDescription.Should().Contain("Modo Offline");
    }

    [Fact]
    public void SyncEngineService_WhenSignalRDisconnects_SwitchesToOfflineImmediately()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act - simular que SignalR pierde conexión
        _mockSignalRClient.Raise(s => s.ConnectionStatusChanged += null, false);

        // Assert
        syncEngine.IsOnline.Should().BeFalse();
    }

    [Fact]
    public void SyncEngineService_WhenConnectionRestored_SwitchesToOnline()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);
        syncEngine.IsOnline.Should().BeFalse();

        // Act - simular reconexión
        _mockApiClient.Raise(a => a.ConnectionStateChanged += null, true);

        // Assert
        syncEngine.IsOnline.Should().BeTrue();
        syncEngine.SyncStatusDescription.Should().Contain("Sincronizado");
    }

    [Fact]
    public async Task ParkingApiClient_WhenHttpRequestFails_FiresConnectionStateChangedFalse()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network unreachable"));

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var client = new ParkingApiClient(httpClient)
        {
            BaseUrl = "https://api.test.com"
        };

        bool? reportedState = null;
        client.ConnectionStateChanged += isOnline => reportedState = isOnline;

        // Act
        var result = await client.CheckInAsync(new CheckInApiRequest
        {
            PlateNumber = "XYZ999",
            BranchId = 1,
            CompanyId = 1
        });

        // Assert
        result.Should().BeNull();
        reportedState.Should().BeFalse();
    }

    [Fact]
    public async Task ParkingApiClient_PingAsync_WhenHostUnreachable_FiresConnectionStateChangedFalse()
    {
        // Arrange
        var mockHttpHandler = new Mock<HttpMessageHandler>();
        mockHttpHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        var httpClient = new HttpClient(mockHttpHandler.Object);
        var client = new ParkingApiClient(httpClient)
        {
            BaseUrl = "https://api.test.com"
        };

        bool? reportedState = null;
        client.ConnectionStateChanged += isOnline => reportedState = isOnline;

        // Act
        var isAlive = await client.PingAsync();

        // Assert
        isAlive.Should().BeFalse();
        reportedState.Should().BeFalse();
    }

    [Fact]
    public async Task EfShiftService_WhenOffline_DoesNotCallRemoteApi()
    {
        // Arrange
        var mockSyncEngine = new Mock<ISyncEngineService>();
        mockSyncEngine.Setup(s => s.IsOnline).Returns(false); // OFFLINE

        var mockServiceProvider = new Mock<IServiceProvider>();
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ISyncEngineService))).Returns(mockSyncEngine.Object);

        var mockAuthService = new Mock<IAuthService>();
        mockAuthService.Setup(a => a.CurrentUser).Returns(new UserSessionModel { ServerUserId = 1, FullName = "Cajero Local" });

        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);

        using (var db = _connectionManager.CreateDbContext())
        {
            db.WorkShifts.Add(new WorkShift
            {
                ShiftId = Guid.NewGuid(),
                BranchId = 1,
                CompanyId = 1,
                UserId = 1,
                OperatorName = "Cajero Local",
                StartTimeUtc = DateTime.UtcNow.AddHours(-1),
                BaseAmount = 50000m,
                Status = 0
            });
            await db.SaveChangesAsync();
        }

        var shiftService = new EfShiftService(
            _connectionManager,
            _mockApiClient.Object,
            mockAuthService.Object,
            _mockSessionService.Object,
            mockServiceProvider.Object);

        // Act
        await shiftService.RefreshCurrentShiftAsync();

        // Assert
        shiftService.CurrentShift.Should().NotBeNull();
        shiftService.CurrentShift!.OperatorName.Should().Be("Cajero Local");
        // Verificar que NUNCA se llamó al API remoto mientras estaba offline
        _mockApiClient.Verify(a => a.GetActiveShiftAsync(It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public void SyncEngineService_WhenSignalRReconnects_SwitchesToOnlineImmediately()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);
        syncEngine.IsOnline.Should().BeFalse();

        // Act - Simular reconexión de SignalR
        _mockSignalRClient.Raise(s => s.ConnectionStatusChanged += null, true);

        // Assert
        syncEngine.IsOnline.Should().BeTrue();
        syncEngine.SyncStatusDescription.Should().Contain("Sincronizado");
    }

    [Fact]
    public void SyncEngineService_SetOnlineStatus_True_StopsProbeAndTransitionsToOnline()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);
        syncEngine.IsOnline.Should().BeFalse();

        // Act
        syncEngine.SetOnlineStatus(true);

        // Assert
        syncEngine.IsOnline.Should().BeTrue();
        syncEngine.SyncStatusDescription.Should().Contain("Sincronizado");
    }

    [Fact]
    public void BackgroundSyncScheduler_StartAndStop_ExecutesCleanly()
    {
        // Arrange
        var mockSync = new Mock<ISyncEngineService>();
        mockSync.Setup(s => s.IsOnline).Returns(true);
        var scheduler = new BackgroundSyncScheduler(mockSync.Object, _mockApiClient.Object);

        // Act & Assert - No debe lanzar excepciones
        var actStart = () => scheduler.Start();
        actStart.Should().NotThrow();

        var actStop = () => scheduler.Stop();
        actStop.Should().NotThrow();
    }

    [Fact]
    public async Task SyncEngineService_SyncRates_SavesMultipleCategoriesWithoutCollision()
    {
        // Arrange
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 1, Name = "Sede 236" });

        var bootstrap = new BootstrapSyncResponse
        {
            Branches = new List<ApiBranchSyncDto>
            {
                new() { Id = 1, Name = "Sede 236" }
            },
            Rates = new List<ApiVehicleRateSyncDto>
            {
                new() { RawRateId = Guid.NewGuid(), BranchId = 1, DisplayName = "Carro", VehicleType = "Car", HourRate = 5000m, MinuteRate = 250m, IsActive = true },
                new() { RawRateId = Guid.NewGuid(), BranchId = 1, DisplayName = "Patineta", VehicleType = "Bicycle", HourRate = 3800m, MinuteRate = 28m, IsActive = true },
                new() { RawRateId = Guid.NewGuid(), BranchId = 1, DisplayName = "Moto", VehicleType = "Motorcycle", HourRate = 2000m, MinuteRate = 160m, IsActive = true },
                new() { RawRateId = Guid.NewGuid(), BranchId = 1, DisplayName = "Bicicleta", VehicleType = "Bicycle", HourRate = 1500m, MinuteRate = 60m, IsActive = true }
            }
        };

        _mockApiClient.Setup(a => a.PingAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(a => a.GetBootstrapAsync(1)).ReturnsAsync(bootstrap);

        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act
        var result = await syncEngine.PerformFullSyncAsync();

        // Assert
        result.Should().BeTrue();
        using var db = _connectionManager.CreateDbContext();
        var localRates = await db.VehicleRates.Where(r => r.BranchId == 1).ToListAsync();
        localRates.Should().HaveCount(4);
        localRates.Select(r => r.DisplayName).Should().Contain(new[] { "Carro", "Patineta", "Moto", "Bicicleta" });
    }

    [Fact]
    public async Task SyncEngineService_SyncRates_MultiBranch_PersistsAllBranchesAndDoesNotPurgeSisterBranches()
    {
        // Arrange: Sede 1 activa
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 1, Name = "Sede 136" });

        var rateBranch1Car = new ApiVehicleRateSyncDto
        {
            RawRateId = Guid.NewGuid(),
            BranchId = 1,
            DisplayName = "Automóvil Sede 1",
            VehicleType = "Car",
            HourRate = 4500m,
            MinuteRate = 75m,
            IsActive = true
        };
        var rateBranch1Moto = new ApiVehicleRateSyncDto
        {
            RawRateId = Guid.NewGuid(),
            BranchId = 1,
            DisplayName = "Motocicleta Sede 1",
            VehicleType = "Motorcycle",
            HourRate = 2500m,
            MinuteRate = 42m,
            IsActive = true
        };
        var rateBranch2Car = new ApiVehicleRateSyncDto
        {
            RawRateId = Guid.NewGuid(),
            BranchId = 2,
            DisplayName = "Automóvil Sede 2",
            VehicleType = "Car",
            HourRate = 5000m,
            MinuteRate = 250m,
            IsActive = true
        };
        var rateBranch2Moto = new ApiVehicleRateSyncDto
        {
            RawRateId = Guid.NewGuid(),
            BranchId = 2,
            DisplayName = "Motocicleta Sede 2",
            VehicleType = "Motorcycle",
            HourRate = 2000m,
            MinuteRate = 160m,
            IsActive = true
        };

        var bootstrap = new BootstrapSyncResponse
        {
            Branches = new List<ApiBranchSyncDto>
            {
                new() { Id = 1, Name = "Sede 136" },
                new() { Id = 2, Name = "Pepe sierra" }
            },
            Rates = new List<ApiVehicleRateSyncDto>
            {
                rateBranch1Car,
                rateBranch1Moto,
                rateBranch2Car,
                rateBranch2Moto
            }
        };

        _mockApiClient.Setup(a => a.PingAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(a => a.GetBootstrapAsync(1)).ReturnsAsync(bootstrap);

        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act 1: Sincronizar catálogo multi-sede
        var result = await syncEngine.PerformFullSyncAsync();

        // Assert 1: Ambos BranchId deben persistirse en SQLite con sus sedes originales
        result.Should().BeTrue();
        using (var db = _connectionManager.CreateDbContext())
        {
            var branch1Rates = await db.VehicleRates.Where(r => r.BranchId == 1).ToListAsync();
            branch1Rates.Should().HaveCount(2);

            var branch2Rates = await db.VehicleRates.Where(r => r.BranchId == 2).ToListAsync();
            branch2Rates.Should().HaveCount(2);
        }

        // Act 2 & Assert 2: Pricing calculator en Sede 1 resuelve tarifas de Sede 1
        var pricingService = new EfPricingCalculatorService(_connectionManager, syncEngine, _mockSessionService.Object);
        await pricingService.ReloadRatesAsync();
        var resolvedCarRateSede1 = pricingService.GetRate(VehicleType.Car);
        resolvedCarRateSede1.Should().NotBeNull();
        resolvedCarRateSede1!.HourRate.Should().Be(4500m);
        resolvedCarRateSede1.DisplayName.Should().Be("Automóvil Sede 1");

        // Act 3 & Assert 3: Al conmutar de sede a Sede 2 (modo offline), resuelve tarifas de Sede 2
        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(2);
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 2, Name = "Pepe sierra" });
        await pricingService.ReloadRatesAsync();
        var resolvedCarRateSede2 = pricingService.GetRate(VehicleType.Car);
        resolvedCarRateSede2.Should().NotBeNull();
        resolvedCarRateSede2!.HourRate.Should().Be(5000m);
        resolvedCarRateSede2.DisplayName.Should().Be("Automóvil Sede 2");

        // Act 4: Resincronizar y confirmar que las sedes hermanas NO se purgan
        _mockApiClient.Setup(a => a.GetBootstrapAsync(2)).ReturnsAsync(bootstrap);
        var resyncResult = await syncEngine.PerformFullSyncAsync();
        resyncResult.Should().BeTrue();
        using (var dbAfter = _connectionManager.CreateDbContext())
        {
            var allRates = await dbAfter.VehicleRates.ToListAsync();
            allRates.Should().HaveCount(4);
        }
    }

    [Fact]
    public async Task SyncEngineService_SyncRoles_WithCustomRole_DoesNotThrowRoleIdCollision()
    {
        // Arrange: Simular que la base de datos local ya tiene un rol 'Cajero' creado durante el login
        var cajeroRoleId = Guid.NewGuid();
        using (var dbInit = _connectionManager.CreateDbContext())
        {
            dbInit.Roles.Add(new Role
            {
                RoleId = cajeroRoleId,
                Name = "Cajero",
                Description = "Cajero de turno"
            });
            await dbInit.SaveChangesAsync();
        }

        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 1, Name = "Sede Principal" });

        var bootstrap = new BootstrapSyncResponse
        {
            UserRoles = new List<ApiUserRoleSyncDto>
            {
                new() { Id = 1, Role = "Administrador", Description = "Admin", IsActive = true },
                new() { Id = 2, Role = "Cajero", Description = "Cajero de turno actualizado", IsActive = true },
                new() { Id = 3, Role = "Supervisor", Description = "Supervisor de patio", IsActive = true }
            },
            Users = new List<ApiUserSyncDto>
            {
                new() { Id = 10, Username = "cajero1", FullName = "Carlos Cajero", UserRoleId = 2, IsActive = true },
                new() { Id = 11, Username = "admin1", FullName = "Ana Admin", UserRoleId = 1, IsActive = true }
            }
        };

        _mockApiClient.Setup(a => a.PingAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(a => a.GetBootstrapAsync(1)).ReturnsAsync(bootstrap);

        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act - No debe fallar con SQLite Error 19: UNIQUE constraint failed: Roles.RoleId
        var result = await syncEngine.PerformFullSyncAsync();

        // Assert
        result.Should().BeTrue();
        using var db = _connectionManager.CreateDbContext();
        var roles = await db.Roles.ToListAsync();
        roles.Should().HaveCount(3);
        roles.Select(r => r.Name).Should().Contain(new[] { "Cajero", "Administrador", "Supervisor" });

        var users = await db.Users.ToListAsync();
        users.Should().HaveCount(2);
        var cajeroUser = users.First(u => u.Username == "cajero1");
        cajeroUser.RoleId.Should().Be(cajeroRoleId);
    }

    [Fact]
    public async Task SyncEngineService_ReconcileActiveTickets_PreservesUnsynchronizedOfflineTickets()
    {
        // Arrange: Crear un tiquete activo generado en modo offline (IsSynchronized == false)
        var offlineTicketId = Guid.NewGuid();
        using (var dbInit = _connectionManager.CreateDbContext())
        {
            dbInit.ParkingTickets.Add(new ParkingTicket
            {
                TicketId = offlineTicketId,
                BranchId = 1,
                TicketNumber = "OFF-001",
                PlateNumber = "OFF999",
                Status = TicketStatus.Active,
                IsSynchronized = false,
                EntryTimeUtc = DateTime.UtcNow.AddMinutes(-30)
            });
            await dbInit.SaveChangesAsync();
        }

        _mockSessionService.Setup(s => s.CurrentBranchId).Returns(1);
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 1, Name = "Sede Principal" });

        // El servidor no reporta este tiquete porque aún no se ha subido
        var bootstrap = new BootstrapSyncResponse
        {
            ActiveTickets = new List<ApiParkingTicketSyncDto>()
        };

        _mockApiClient.Setup(a => a.PingAsync()).ReturnsAsync(true);
        _mockApiClient.Setup(a => a.GetBootstrapAsync(1)).ReturnsAsync(bootstrap);

        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        // Act
        var result = await syncEngine.PerformFullSyncAsync();

        // Assert: El tiquete offline no debe cerrarse prematuramente
        result.Should().BeTrue();
        using var db = _connectionManager.CreateDbContext();
        var ticket = await db.ParkingTickets.FirstOrDefaultAsync(t => t.TicketId == offlineTicketId);
        ticket.Should().NotBeNull();
        ticket!.Status.Should().Be(TicketStatus.Active);
        ticket.IsSynchronized.Should().BeFalse();
    }

    [Fact]
    public void BootstrapSyncResponse_GetGracePeriodMinutes_WhenNull_DefaultsToZero()
    {
        // Arrange
        var rateDto = new ApiVehicleRateSyncDto
        {
            GracePeriodMinutes = null,
            GracePeriodMinutesSnake = null,
            Gracia = null
        };

        // Act
        var grace = rateDto.GetGracePeriodMinutes();

        // Assert - Regla de Oro #8: Debe ser 0, nunca 15 inventado
        grace.Should().Be(0);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
    }
}
