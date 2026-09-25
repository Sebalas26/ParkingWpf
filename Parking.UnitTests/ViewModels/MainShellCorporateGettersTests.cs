using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class MainShellCorporateGettersTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<ISessionService> _sessionServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<IParkingTicketService> _ticketServiceMock = new();
    private readonly Mock<INavigationService> _navigationServiceMock = new();
    private readonly Mock<IApiClientService> _apiClientMock = new();
    private readonly Mock<ISyncEngineService> _syncEngineMock = new();
    private readonly Mock<IBackgroundSyncScheduler> _backgroundSyncMock = new();
    private readonly Mock<IDialogService> _dialogServiceMock = new();
    private readonly Mock<IShiftService> _shiftServiceMock = new();
    private readonly Mock<ISignalRClientService> _signalRClientMock = new();

    private MainShellViewModel CreateViewModel()
    {
        return new MainShellViewModel(
            _authServiceMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object,
            _ticketServiceMock.Object,
            _navigationServiceMock.Object,
            _apiClientMock.Object,
            _syncEngineMock.Object,
            _backgroundSyncMock.Object,
            _dialogServiceMock.Object,
            _shiftServiceMock.Object,
            _signalRClientMock.Object);
    }

    [Fact]
    public void CorporateGetters_WhenCurrentUserHasCorporateData_MaintainsConsistencyAcrossBranches()
    {
        // Arrange
        var branch1 = new BranchModel
        {
            Id = 1,
            Name = "Pepe sierra",
            Phone = "3102207910",
            CompanyName = "Parkgo",
            CompanyNit = "9088777777"
        };

        var branch2 = new BranchModel
        {
            Id = 2,
            Name = "Sede 136",
            Phone = "3188088885",
            CompanyName = "",
            CompanyNit = ""
        };

        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero1",
            FullName = "Cajero Uno",
            CompanyName = "Parkgo",
            CompanyNit = "9088777777",
            CompanyPhone = "3102207910",
            CompanyEmail = "admin@parkgo.com"
        };

        _sessionServiceMock.Setup(s => s.CurrentUser).Returns(user);
        _sessionServiceMock.Setup(s => s.UserBranches).Returns(new List<BranchModel> { branch1, branch2 });

        var vm = CreateViewModel();

        // 1. Simular sesión con Pepe sierra como sede activa
        vm.CurrentUser = user;
        vm.CurrentBranch = branch1;

        vm.CompanyDisplayName.Should().Be("Parkgo");
        vm.CompanyNit.Should().Be("9088777777");
        vm.CompanyPhone.Should().Be("3102207910");
        vm.CompanyEmail.Should().Be("admin@parkgo.com");

        // 2. Simular cambio de sede a Sede 136 (que no tiene NIT propio)
        vm.CurrentBranch = branch2;

        // Assert: El NIT no desaparece, el nombre y el teléfono corporativo siguen intactos y completos
        vm.CompanyDisplayName.Should().Be("Parkgo");
        vm.CompanyNit.Should().Be("9088777777");
        vm.CompanyPhone.Should().Be("3102207910");
        vm.CompanyEmail.Should().Be("admin@parkgo.com");
    }

    [Fact]
    public void CorporateGetters_WhenCurrentUserFieldsAreNull_FallsBackToUserBranches()
    {
        // Arrange
        var branch1 = new BranchModel
        {
            Id = 1,
            Name = "Pepe sierra",
            CompanyName = "Parkgo",
            CompanyNit = "9088777777",
            CompanyPhone = "3102207910",
            CompanyEmail = "contacto@parkgo.com"
        };

        var branch2 = new BranchModel
        {
            Id = 2,
            Name = "Sede 136",
            Phone = "3188088885"
            // All corporate fields null
        };

        var user = new UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero1",
            FullName = "Cajero Uno"
            // Corporate fields are null
        };

        _sessionServiceMock.Setup(s => s.CurrentUser).Returns(user);
        _sessionServiceMock.Setup(s => s.UserBranches).Returns(new List<BranchModel> { branch1, branch2 });

        var vm = CreateViewModel();
        vm.CurrentUser = user;
        vm.CurrentBranch = branch2; // Sede 136 activa

        // Assert: Resuelve a partir de la colección de sedes hermanas del usuario
        vm.CompanyDisplayName.Should().Be("Parkgo");
        vm.CompanyNit.Should().Be("9088777777");
        vm.CompanyPhone.Should().Be("3102207910");
        vm.CompanyEmail.Should().Be("contacto@parkgo.com");
    }
}
