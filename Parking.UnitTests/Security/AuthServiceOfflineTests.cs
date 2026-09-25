using System;
using System.Net.Http;
using System.Threading.Tasks;
using Moq;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Security;

public class AuthServiceOfflineTests : IDisposable
{
    private readonly TestDbConnectionManager _dbManager;
    private readonly Mock<IApiClientService> _apiClientMock;
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<IPermissionService> _permissionServiceMock;

    public AuthServiceOfflineTests()
    {
        _dbManager = new TestDbConnectionManager();
        _apiClientMock = new Mock<IApiClientService>();
        _sessionServiceMock = new Mock<ISessionService>();
        _permissionServiceMock = new Mock<IPermissionService>();
    }

    public void Dispose()
    {
        _dbManager.Dispose();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenApiThrowsException_ShouldAuthenticateSuccessfullyWithBCryptHashFromSQLite()
    {
        // Arrange: API central caída
        _apiClientMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("No se puede conectar al servidor"));

        // Preparar usuario en SQLite con hash BCrypt (como viene de MySQL)
        var rawPassword = "passwordSeguro123";
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 11);

        using (var db = _dbManager.CreateDbContext())
        {
            var role = new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "Operador",
                Description = "Operador de Caja"
            };
            db.Roles.Add(role);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = "cajero_offline",
                FullName = "Cajero Pruebas Offline",
                PasswordHash = bcryptHash,
                RoleId = role.RoleId,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);

            var branch = new Branch
            {
                Id = 1,
                Name = "Sede Norte",
                Code = "SN01",
                IsActive = true
            };
            db.Branches.Add(branch);

            await db.SaveChangesAsync();
        }

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act: Intento de login offline con clave correcta
        var result = await authService.AuthenticateAsync("cajero_offline", rawPassword);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("cajero_offline", result.User.Username);
        Assert.Equal("Cajero Pruebas Offline", result.User.FullName);
        Assert.NotEmpty(result.Branches);
    }

    [Fact]
    public async Task AuthenticateAsync_WhenApiOffline_AndWrongPassword_ShouldFailWithErrorMessage()
    {
        // Arrange: API central caída
        _apiClientMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Servidor caído"));

        var rawPassword = "passwordCorrecto";
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 11);

        using (var db = _dbManager.CreateDbContext())
        {
            var role = new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "Operador"
            };
            db.Roles.Add(role);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = "cajero_test",
                PasswordHash = bcryptHash,
                RoleId = role.RoleId,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act: Intento con contraseña equivocada
        var result = await authService.AuthenticateAsync("cajero_test", "passwordIncorrecto");

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.Contains("incorrectos", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticateAsync_Online_AssignsLocalRoleIdToSession()
    {
        // Arrange
        _apiClientMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new Parking.Models.ApiModels.LoginApiResponse
            {
                Success = true,
                Token = "fake-jwt-token",
                UserId = 42,
                Username = "cajero_online",
                FullName = "Cajero Online Test",
                RoleName = "Cajero Central",
                RoleId = 7,
                CompanyId = 1,
                CompanyName = "Parking Flow",
                Permissions = new System.Collections.Generic.List<string> { "shift.handover", "ticket.create" }
            });

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act
        var result = await authService.AuthenticateAsync("cajero_online", "password123");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.NotEqual(Guid.Empty, result.User.RoleId);
        Assert.NotEqual(Guid.Empty, result.User.UserId);
        Assert.Equal(42, result.User.ServerUserId);
        Assert.Equal(7, result.User.ServerRoleId);
    }

    [Fact]
    public async Task SwitchCurrentUser_UsesGrantedPermissions_WhenAvailable()
    {
        // Arrange
        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        var userModel = new Parking.Models.UserSessionModel
        {
            UserId = Guid.NewGuid(),
            Username = "cajero_switch",
            FullName = "Cajero Switch Test",
            RoleName = "Operador",
            RoleId = Guid.NewGuid(),
            IsAdmin = false,
            GrantedPermissions = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "shift.handover",
                "ticket.checkout"
            }
        };

        // Act
        authService.SwitchCurrentUser(userModel);

        // Assert: _permissionServiceMock debe recibir las GrantedPermissions directamente
        _permissionServiceMock.Verify(p => p.LoadPermissions(
            It.Is<System.Collections.Generic.List<string>>(perms =>
                perms.Contains("shift.handover") && perms.Contains("ticket.checkout")),
            false),
            Moq.Times.Once);
    }

    [Fact]
    public async Task ValidateCredentials_DoesNotCreateNewApiSession()
    {
        // Arrange
        var rawPassword = "claveLocal123";
        var bcryptHash = BCrypt.Net.BCrypt.HashPassword(rawPassword, workFactor: 11);

        using (var db = _dbManager.CreateDbContext())
        {
            var role = new Role { RoleId = Guid.NewGuid(), Name = "Cajero" };
            db.Roles.Add(role);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = "cajero_val_local",
                FullName = "Cajero Local Validación",
                PasswordHash = bcryptHash,
                RoleId = role.RoleId,
                IsActive = true
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act
        var validatedSession = await authService.ValidateCredentialsAsync("cajero_val_local", rawPassword);

        // Assert: Validó localmente con éxito
        Assert.NotNull(validatedSession);
        Assert.Equal("cajero_val_local", validatedSession.Username);

        // Y NUNCA llamó al API central para crear una sesión o token
        _apiClientMock.Verify(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()), Moq.Times.Never);
    }

    [Fact]
    public async Task ValidateAdminAuthorization_OnlyAcceptsIsAdminTrue()
    {
        // Arrange: Dos usuarios, uno con rol Administrador pero IsAdmin = false (suplantación de texto),
        // y otro con IsAdmin = true legítimo.
        var passAdminFalso = "claveFalsa123";
        var passAdminReal = "claveReal123";

        using (var db = _dbManager.CreateDbContext())
        {
            var role = new Role { RoleId = Guid.NewGuid(), Name = "Administrador" };
            db.Roles.Add(role);

            // Usuario con nombre de rol "Administrador" pero IsAdmin = false
            db.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                Username = "admin_falso",
                FullName = "Usuario Trampa",
                PasswordHash = DbConnectionManager.HashPassword(passAdminFalso),
                RoleId = role.RoleId,
                IsAdmin = false,
                IsActive = true
            });

            // Usuario con IsAdmin = true legítimo
            db.Users.Add(new User
            {
                UserId = Guid.NewGuid(),
                Username = "admin_real",
                FullName = "Admin Real",
                PasswordHash = DbConnectionManager.HashPassword(passAdminReal),
                RoleId = role.RoleId,
                IsAdmin = true,
                IsActive = true
            });

            await db.SaveChangesAsync();
        }

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act & Assert
        var resultFalso = await authService.ValidateAdminAuthorizationAsync(passAdminFalso);
        Assert.Null(resultFalso); // Rechazado porque IsAdmin es false

        var resultReal = await authService.ValidateAdminAuthorizationAsync(passAdminReal);
        Assert.NotNull(resultReal); // Aceptado porque IsAdmin es true
        Assert.Equal("admin_real", resultReal.Username);
    }

    [Fact]
    public async Task AuthenticateAsync_Offline_ResolvesAndHealsCorporateDataConsistentlyAcrossBranches()
    {
        // Arrange: API central caída
        _apiClientMock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("API Offline"));

        var password = "offlinePassword123";
        var passwordHash = DbConnectionManager.HashPassword(password);

        using (var db = _dbManager.CreateDbContext())
        {
            var role = new Role
            {
                RoleId = Guid.NewGuid(),
                Name = "Operador",
                Description = "Operador Caja"
            };
            db.Roles.Add(role);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                Username = "cajero_multi_sede",
                FullName = "Operador Multi Sede",
                Email = "contacto@parkgo.com",
                PasswordHash = passwordHash,
                RoleId = role.RoleId,
                CompanyId = 1,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);

            // Sede 1 (Pepe sierra): tiene CompanyName y CompanyNit completos
            var branch1 = new Branch
            {
                Id = 1,
                CompanyId = 1,
                CompanyName = "Parkgo",
                CompanyNit = "9088777777",
                Phone = "3102207910",
                Name = "Pepe sierra",
                Code = "PS01",
                IsActive = true
            };
            // Sede 2 (Sede 136): CompanyName y CompanyNit están vacíos
            var branch2 = new Branch
            {
                Id = 2,
                CompanyId = 1,
                CompanyName = "",
                CompanyNit = "",
                Phone = "3188088885",
                Name = "Sede 136",
                Code = "S136",
                IsActive = true
            };
            db.Branches.AddRange(branch1, branch2);
            await db.SaveChangesAsync();
        }

        var authService = new AuthService(
            _dbManager,
            _apiClientMock.Object,
            _sessionServiceMock.Object,
            _permissionServiceMock.Object);

        // Act
        var result = await authService.AuthenticateAsync("cajero_multi_sede", password);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.User);
        Assert.Equal("Parkgo", result.User.CompanyName);
        Assert.Equal("9088777777", result.User.CompanyNit);
        Assert.Equal("contacto@parkgo.com", result.User.CompanyEmail);
        Assert.Equal("3102207910", result.User.CompanyPhone);

        // Ambas sedes deben tener datos corporativos completos y consistentes
        Assert.Equal(2, result.Branches.Count);
        foreach (var b in result.Branches)
        {
            Assert.Equal("Parkgo", b.CompanyName);
            Assert.Equal("9088777777", b.CompanyNit);
            Assert.Equal("contacto@parkgo.com", b.CompanyEmail);
            Assert.Equal("3102207910", b.CompanyPhone);
        }

        // Auto-curación en SQLite: Sede 2 debe haberse curado en la base de datos local
        using (var verifyDb = _dbManager.CreateDbContext())
        {
            var healedBranch2 = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(verifyDb.Branches, b => b.Id == 2);
            Assert.NotNull(healedBranch2);
            Assert.Equal("Parkgo", healedBranch2.CompanyName);
            Assert.Equal("9088777777", healedBranch2.CompanyNit);
        }
    }
}
