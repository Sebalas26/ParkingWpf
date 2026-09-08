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
}
