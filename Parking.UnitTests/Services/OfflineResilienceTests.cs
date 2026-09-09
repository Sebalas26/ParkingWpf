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
        syncEngine.SyncStatusDescription.Should().Contain("API Central Online");
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
        syncEngine.SyncStatusDescription.Should().Contain("API Central Online");
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
        syncEngine.SyncStatusDescription.Should().Contain("API Central Online");
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
    public async Task SyncEngineService_ProcessPendingQueueAsync_DispatchesAndDeletesFromLocalDb()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);

        // Insertar item offline pendiente
        using (var db = _connectionManager.CreateDbContext())
        {
            db.PendingSyncItems.Add(new PendingSyncItem
            {
                PendingSyncItemId = Guid.NewGuid(),
                OperationType = "CheckIn",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new CheckInApiRequest
                {
                    TicketId = Guid.NewGuid(),
                    BranchId = 1,
                    CompanyId = 1,
                    TicketNumber = "T-001",
                    PlateNumber = "ABC123",
                    VehicleType = VehicleType.Car,
                    HourlyRate = 2000m,
                    EntryTimeUtc = DateTime.UtcNow
                }),
                CreatedAtUtc = DateTime.UtcNow,
                IsProcessed = false
            });
            await db.SaveChangesAsync();
        }

        _mockApiClient.Setup(a => a.CheckInAsync(It.IsAny<CheckInApiRequest>()))
            .ReturnsAsync(new ParkingTicket { TicketId = Guid.NewGuid(), PlateNumber = "ABC123" });

        // Act - Conectar y despachar
        syncEngine.SetOnlineStatus(true);
        await syncEngine.ProcessPendingQueueAsync();

        // Assert - El item procesado debe ser eliminado de SQLite
        using (var db = _connectionManager.CreateDbContext())
        {
            var remaining = await db.PendingSyncItems.CountAsync();
            remaining.Should().Be(0);
        }

        _mockApiClient.Verify(a => a.CheckInAsync(It.IsAny<CheckInApiRequest>()), Times.Once);
    }

    [Fact]
    public async Task SyncEngineService_ProcessPendingQueueAsync_WhenOffline_DoesNotDispatch()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);

        using (var db = _connectionManager.CreateDbContext())
        {
            db.PendingSyncItems.Add(new PendingSyncItem
            {
                PendingSyncItemId = Guid.NewGuid(),
                OperationType = "CheckIn",
                PayloadJson = "{}",
                CreatedAtUtc = DateTime.UtcNow,
                IsProcessed = false
            });
            await db.SaveChangesAsync();
        }

        // Act
        await syncEngine.ProcessPendingQueueAsync();

        // Assert
        using (var db = _connectionManager.CreateDbContext())
        {
            var remaining = await db.PendingSyncItems.CountAsync();
            remaining.Should().Be(1);
        }

        _mockApiClient.Verify(a => a.CheckInAsync(It.IsAny<CheckInApiRequest>()), Times.Never);
    }

    [Fact]
    public async Task ProcessPendingQueueAsync_WhenCheckOutReturnedFromCloud_RemovesFromQueueAndReconcilesCanonicalDataToSqlite()
    {
        // Arrange
        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(true);

        var ticketId = Guid.NewGuid();
        var localTicket = new ParkingTicket
        {
            TicketId = ticketId,
            PlateNumber = "OFF123",
            Status = TicketStatus.Active,
            GrossAmount = 2000,
            NetAmount = 2000,
            EntryTimeUtc = DateTime.UtcNow.AddHours(-2)
        };

        var pendingItem = new PendingSyncItem
        {
            PendingSyncItemId = Guid.NewGuid(),
            OperationType = "CheckOut",
            PayloadJson = System.Text.Json.JsonSerializer.Serialize(new CheckOutApiRequest
            {
                TicketId = ticketId,
                GrossAmount = 2000,
                NetAmount = 2000
            }, ParkingApiClient.JsonOptions),
            CreatedAtUtc = DateTime.UtcNow,
            IsProcessed = false
        };

        using (var db = _connectionManager.CreateDbContext())
        {
            db.ParkingTickets.Add(localTicket);
            db.PendingSyncItems.Add(pendingItem);
            await db.SaveChangesAsync();
        }

        var cloudCanonicalTicket = new ParkingTicket
        {
            TicketId = ticketId,
            PlateNumber = "OFF123",
            Status = TicketStatus.Completed,
            GrossAmount = 8500, // Data real de la nube
            NetAmount = 8500,
            ExitTimeUtc = DateTime.UtcNow.AddMinutes(-30),
            PaymentMethod = PaymentMethod.CreditCard
        };

        _mockApiClient.Setup(a => a.CheckOutAsync(It.IsAny<CheckOutApiRequest>()))
            .ReturnsAsync(cloudCanonicalTicket);

        // Act
        await syncEngine.ProcessPendingQueueAsync();

        // Assert: El ítem pendiente debe haber sido eliminado de la cola (0 pendientes)
        using (var db = _connectionManager.CreateDbContext())
        {
            var remainingPending = await db.PendingSyncItems.CountAsync();
            remainingPending.Should().Be(0);

            // La data de la nube debe haber bajado a tierra en SQLite
            var updatedLocal = await db.ParkingTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            updatedLocal.Should().NotBeNull();
            updatedLocal!.Status.Should().Be(TicketStatus.Completed);
            updatedLocal.GrossAmount.Should().Be(8500);
            updatedLocal.NetAmount.Should().Be(8500);
            updatedLocal.PaymentMethod.Should().Be(PaymentMethod.CreditCard);
            updatedLocal.IsSynchronized.Should().BeTrue();
        }
    }

    [Fact]
    public async Task SyncEngineService_WhenSetOnlineStatusTrue_InvokesEnsureConnectedAsyncOnSignalR()
    {
        // Arrange
        _mockSessionService.Setup(s => s.CurrentBranch).Returns(new BranchModel { Id = 5 });
        _mockSessionService.Setup(s => s.CurrentUser).Returns(new UserSessionModel { CompanyId = 2 });

        var syncEngine = new SyncEngineService(
            _mockApiClient.Object,
            _connectionManager,
            _mockSessionService.Object,
            _mockShiftService.Object,
            _mockSignalRClient.Object);

        syncEngine.SetOnlineStatus(false);

        // Act
        syncEngine.SetOnlineStatus(true);
        await Task.Delay(100); // Pequeña pausa para que Task.Run ejecute

        // Assert
        _mockSignalRClient.Verify(s => s.EnsureConnectedAsync(5, 2), Times.Once);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
    }
}
