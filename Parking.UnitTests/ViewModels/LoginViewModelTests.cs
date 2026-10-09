using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Core.Enums;
using Parking.Data.Factories;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class LoginViewModelTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<ISyncEngineService> _mockSyncEngine;
    private readonly Mock<IPermissionService> _mockPermissionService;
    private readonly Mock<IAppUpdateService> _mockUpdateService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IDbConnectionManager> _mockDbManager;

    public LoginViewModelTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockApiClient = new Mock<IApiClientService>();
        _mockSyncEngine = new Mock<ISyncEngineService>();
        _mockPermissionService = new Mock<IPermissionService>();
        _mockUpdateService = new Mock<IAppUpdateService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockDbManager = new Mock<IDbConnectionManager>();
    }

    [Fact]
    public async Task LoginAsync_CuandoExisteActualizacionYHayPendientes_EjecutaSyncAntesDeActualizar()
    {
        // Arrange
        var branch = new BranchModel { Id = 1, Name = "Sede Principal" };
        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "operador",
            FullName = "Operador Test",
            IsSuperAdmin = false,
            RoleId = Guid.NewGuid(),
            RoleName = "Administrador"
        };

        _mockAuthService.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new LoginResultModel
            {
                Success = true,
                User = user,
                Branches = new List<BranchModel> { branch },
                HasDesktopAccess = true
            });

        _mockPermissionService.SetupGet(p => p.IsAdmin).Returns(true);
        _mockSyncEngine.Setup(s => s.HasLocalBranchDataAsync(1)).ReturnsAsync(true);
        _mockSessionService.SetupGet(s => s.CurrentBranch).Returns(branch);

        var releaseInfo = new AppReleaseInfoDto
        {
            HasUpdate = true,
            LatestVersion = "1.0.16",
            IsMandatory = true
        };
        _mockUpdateService.Setup(u => u.CheckForUpdateAsync()).ReturnsAsync(releaseInfo);

        // Al inicio hay 2 pendientes, luego de sincronizar queda en 0
        int pendingCount = 2;
        _mockSyncEngine.SetupGet(s => s.PendingItemsCount).Returns(() => pendingCount);
        _mockSyncEngine.Setup(s => s.PerformFullSyncAsync()).ReturnsAsync(() =>
        {
            pendingCount = 0;
            return true;
        });

        var vm = new LoginViewModel(
            _mockAuthService.Object,
            _mockSessionService.Object,
            _mockApiClient.Object,
            _mockSyncEngine.Object,
            _mockPermissionService.Object,
            _mockUpdateService.Object,
            _mockDialogService.Object,
            _mockDbManager.Object);

        vm.Username = "operador";
        vm.Password = "password123";

        // Act
        await vm.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockDbManager.Verify(d => d.BackupDatabaseAsync(), Times.Once);
        _mockSyncEngine.Verify(s => s.PerformFullSyncAsync(), Times.Once);
        _mockDialogService.Verify(d => d.ShowAppUpdateDialogAsync(releaseInfo), Times.Once);
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_CuandoActualizacionObligatoriaYColaNoLograSubir_BloqueaConMensajeInformativo()
    {
        // Arrange
        var branch = new BranchModel { Id = 1, Name = "Sede Principal" };
        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "operador",
            FullName = "Operador Test",
            IsSuperAdmin = false,
            RoleId = Guid.NewGuid(),
            RoleName = "Administrador"
        };

        _mockAuthService.Setup(a => a.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new LoginResultModel
            {
                Success = true,
                User = user,
                Branches = new List<BranchModel> { branch },
                HasDesktopAccess = true
            });

        _mockPermissionService.SetupGet(p => p.IsAdmin).Returns(true);
        _mockSyncEngine.Setup(s => s.HasLocalBranchDataAsync(1)).ReturnsAsync(true);
        _mockSessionService.SetupGet(s => s.CurrentBranch).Returns(branch);

        var releaseInfo = new AppReleaseInfoDto
        {
            HasUpdate = true,
            LatestVersion = "1.0.16",
            IsMandatory = true
        };
        _mockUpdateService.Setup(u => u.CheckForUpdateAsync()).ReturnsAsync(releaseInfo);

        // La cola se mantiene en 1 pendiente (sin conexión por ejemplo)
        _mockSyncEngine.SetupGet(s => s.PendingItemsCount).Returns(1);
        _mockSyncEngine.Setup(s => s.PerformFullSyncAsync()).ReturnsAsync(false);

        var vm = new LoginViewModel(
            _mockAuthService.Object,
            _mockSessionService.Object,
            _mockApiClient.Object,
            _mockSyncEngine.Object,
            _mockPermissionService.Object,
            _mockUpdateService.Object,
            _mockDialogService.Object,
            _mockDbManager.Object);

        vm.Username = "operador";
        vm.Password = "password123";

        // Act
        await vm.LoginCommand.ExecuteAsync(null);

        // Assert
        _mockSyncEngine.Verify(s => s.PerformFullSyncAsync(), Times.Once);
        _mockDialogService.Verify(d => d.ShowAppUpdateDialogAsync(It.IsAny<AppReleaseInfoDto>()), Times.Never);
        vm.HasError.Should().BeTrue();
        vm.ErrorMessage.Should().Contain("Existe una actualización obligatoria (1.0.16)");
        vm.ErrorMessage.Should().Contain("1 transacciones locales");
    }
}
