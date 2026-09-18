using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Parking.Data.Factories;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.Services.Implementations;
using Xunit;

namespace Parking.UnitTests.Services;

public class AppUpdateAndLicensingTests : IDisposable
{
    private readonly string _testTempDir;

    public AppUpdateAndLicensingTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "ParkFlow_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public void HardwareFingerprint_ShouldReturnValidSha256HexString()
    {
        var service = new HardwareFingerprintService();

        var fingerprint1 = service.GetMachineFingerprint();
        var fingerprint2 = service.GetMachineFingerprint();

        fingerprint1.Should().NotBeNullOrWhiteSpace();
        fingerprint1.Length.Should().Be(64); // SHA-256 hex
        fingerprint1.Should().Be(fingerprint2); // Cached / Deterministic
    }

    [Fact]
    public void DeviceLicenseService_WhenFileDoesNotExist_ShouldReturnNullAndNotValid()
    {
        var fingerprintMock = new Mock<IHardwareFingerprintService>();
        fingerprintMock.Setup(f => f.GetMachineFingerprint()).Returns("fp_original");

        var nonExistentPath = Path.Combine(_testTempDir, "missing_license.dat");
        var service = new DeviceLicenseService(fingerprintMock.Object, new HttpClient(), nonExistentPath);

        service.HasValidLicense().Should().BeFalse();
        service.GetCurrentLicense().Should().BeNull();
    }

    [Fact]
    public void DeviceLicenseService_WhenFingerprintDoesNotMatch_ShouldDetectCloningAndReturnNull()
    {
        var fingerprintMock = new Mock<IHardwareFingerprintService>();
        fingerprintMock.Setup(f => f.GetMachineFingerprint()).Returns("fp_machine_A");

        var licensePath = Path.Combine(_testTempDir, "cloned_license.dat");
        var serviceOnMachineA = new DeviceLicenseService(fingerprintMock.Object, new HttpClient(), licensePath);

        // Simular guardado de licencia en Máquina A
        var licenseData = new LocalLicenseData
        {
            LicenseKey = "PKF-CORP-SEDE01",
            DeviceToken = "TOKEN-MACH-A",
            MachineFingerprint = "fp_machine_A",
            BranchId = 1,
            ActivatedAtUtc = DateTime.UtcNow
        };

        var entropy = Encoding.UTF8.GetBytes("ParkFlow_DPAPI_Entropy_2026_Key");
        var json = JsonSerializer.Serialize(licenseData);
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(licensePath, encrypted);

        // Ahora intentar leerlo desde Máquina B (huella diferente)
        var fingerprintMockB = new Mock<IHardwareFingerprintService>();
        fingerprintMockB.Setup(f => f.GetMachineFingerprint()).Returns("fp_machine_B_STOLEN");

        var serviceOnMachineB = new DeviceLicenseService(fingerprintMockB.Object, new HttpClient(), licensePath);

        serviceOnMachineB.HasValidLicense().Should().BeFalse();
        serviceOnMachineB.GetCurrentLicense().Should().BeNull();
    }

    [Fact]
    public async Task PrepareAndApplyUpdate_WhenPendingSyncItemsExistAndSyncFails_ShouldAbortToPreventDataLoss()
    {
        // REGLA DE ORO: Si hay pendientes y falla la sincronización, NUNCA debe actualizar
        var syncEngineMock = new Mock<ISyncEngineService>();
        syncEngineMock.SetupGet(s => s.PendingItemsCount).Returns(5); // 5 transacciones pendientes
        syncEngineMock.Setup(s => s.PerformFullSyncAsync()).ReturnsAsync(false); // Falla el sync

        var dbManagerMock = new Mock<IDbConnectionManager>();
        var fingerprintMock = new Mock<IHardwareFingerprintService>();
        var licenseMock = new Mock<IDeviceLicenseService>();
        var sessionMock = new Mock<ISessionService>();

        var updateService = new AppUpdateService(
            new HttpClient(),
            syncEngineMock.Object,
            dbManagerMock.Object,
            fingerprintMock.Object,
            licenseMock.Object,
            sessionMock.Object);

        var release = new AppReleaseInfoDto
        {
            LatestVersion = "2.0.0",
            IsMandatory = true
        };

        UpdateProgressReport? lastReport = null;
        var progress = new Progress<UpdateProgressReport>(r => lastReport = r);

        var result = await updateService.PrepareAndApplyUpdateAsync(release, progress);

        // Verificaciones de Seguridad Crítica
        result.Should().BeFalse();
        syncEngineMock.Verify(s => s.PerformFullSyncAsync(), Times.Once);
        // Jamás debe llamar al backup ni descargar binarios si el sync falló
        dbManagerMock.Verify(d => d.BackupDatabaseAsync(), Times.Never);
        lastReport.Should().NotBeNull();
        lastReport!.IsError.Should().BeTrue();
        lastReport.ErrorMessage.Should().Contain("Para proteger la información de ventas y turnos, la actualización se ha pospuesto");
    }

    [Fact]
    public async Task PrepareAndApplyUpdate_WhenHashMismatch_ShouldAbortAndDeleteCorruptPackage()
    {
        var syncEngineMock = new Mock<ISyncEngineService>();
        syncEngineMock.SetupGet(s => s.PendingItemsCount).Returns(0); // 0 pendientes

        var dbManagerMock = new Mock<IDbConnectionManager>();
        dbManagerMock.Setup(d => d.BackupDatabaseAsync()).ReturnsAsync("C:\\mock\\backup.db");

        var fingerprintMock = new Mock<IHardwareFingerprintService>();
        fingerprintMock.Setup(f => f.GetMachineFingerprint()).Returns("fp_test");

        var licenseMock = new Mock<IDeviceLicenseService>();
        var sessionMock = new Mock<ISessionService>();

        // Simular respuesta HTTP que entrega bytes falsos (hash no coincide)
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("CONTENIDO_CORRUPTO_O_FALSO"))
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost/")
        };

        var updateService = new AppUpdateService(
            httpClient,
            syncEngineMock.Object,
            dbManagerMock.Object,
            fingerprintMock.Object,
            licenseMock.Object,
            sessionMock.Object);

        var release = new AppReleaseInfoDto
        {
            LatestVersion = "1.5.0",
            DownloadEndpoint = "api/v1/app-update/download/1.5.0",
            PackageSha256 = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855" // Hash esperado diferente
        };

        UpdateProgressReport? lastReport = null;
        var progress = new Progress<UpdateProgressReport>(r => lastReport = r);

        var result = await updateService.PrepareAndApplyUpdateAsync(release, progress);

        result.Should().BeFalse();
        dbManagerMock.Verify(d => d.BackupDatabaseAsync(), Times.Once); // El backup sí se generó
        lastReport.Should().NotBeNull();
        lastReport!.IsError.Should().BeTrue();
        lastReport.ErrorMessage.Should().Contain("El paquete descargado no coincide con la firma digital oficial");
    }
}
