using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Parking.Data.Factories;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class AppUpdateService : IAppUpdateService
{
    private readonly HttpClient _httpClient;
    private readonly ISyncEngineService _syncEngine;
    private readonly IDbConnectionManager _dbManager;
    private readonly IHardwareFingerprintService _fingerprintService;
    private readonly IDeviceLicenseService _licenseService;
    private readonly ISessionService _sessionService;

    public AppUpdateService(
        HttpClient httpClient,
        ISyncEngineService syncEngine,
        IDbConnectionManager dbManager,
        IHardwareFingerprintService fingerprintService,
        IDeviceLicenseService licenseService,
        ISessionService sessionService)
    {
        _httpClient = httpClient;
        _syncEngine = syncEngine;
        _dbManager = dbManager;
        _fingerprintService = fingerprintService;
        _licenseService = licenseService;
        _sessionService = sessionService;
    }

    public async Task<AppReleaseInfoDto?> CheckForUpdateAsync()
    {
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        var fingerprint = _fingerprintService.GetMachineFingerprint();
        var branchId = _sessionService.CurrentBranch?.Id;

        var url = $"api/v1/app-update/check?currentVersion={currentVersion}&machineFingerprint={fingerprint}&branchId={branchId}";

        try
        {
            var license = _licenseService.GetCurrentLicense();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (license != null && !string.IsNullOrWhiteSpace(license.DeviceToken))
            {
                req.Headers.Add("X-Device-Token", license.DeviceToken);
            }
            req.Headers.Add("X-Machine-Fingerprint", fingerprint);

            var response = await _httpClient.SendAsync(req);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var hasUpdate = root.TryGetProperty("hasUpdate", out var hProp) && hProp.GetBoolean();
            if (!hasUpdate)
            {
                return null;
            }

            return new AppReleaseInfoDto
            {
                HasUpdate = true,
                LatestVersion = root.TryGetProperty("latestVersion", out var lvProp) ? lvProp.GetString() ?? string.Empty : string.Empty,
                IsMandatory = root.TryGetProperty("isMandatory", out var mProp) && mProp.GetBoolean(),
                ReleaseNotes = root.TryGetProperty("releaseNotes", out var rnProp) ? rnProp.GetString() : null,
                PackageSha256 = root.TryGetProperty("packageSha256", out var shaProp) ? shaProp.GetString() ?? string.Empty : string.Empty,
                PackageSizeBytes = root.TryGetProperty("packageSizeBytes", out var szProp) && szProp.ValueKind == JsonValueKind.Number ? szProp.GetInt64() : 0,
                DownloadEndpoint = root.TryGetProperty("downloadEndpoint", out var deProp) ? deProp.GetString() ?? string.Empty : string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> PrepareAndApplyUpdateAsync(AppReleaseInfoDto release, IProgress<UpdateProgressReport>? progress = null)
    {
        // 1. REGLA DE ORO DE SEGURIDAD: VERIFICACIÓN Y SINCRONIZACIÓN PREVIA TOTAL DE DATOS LOCALES
        progress?.Report(new UpdateProgressReport
        {
            StepDescription = "Comprobando transacciones locales pendientes antes de actualizar...",
            Percentage = 10
        });

        if (_syncEngine.PendingItemsCount > 0)
        {
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = $"Sincronizando {_syncEngine.PendingItemsCount} transacciones locales pendientes con el servidor central...",
                Percentage = 20
            });

            var syncSuccess = await _syncEngine.PerformFullSyncAsync();
            if (!syncSuccess || _syncEngine.PendingItemsCount > 0)
            {
                progress?.Report(new UpdateProgressReport
                {
                    StepDescription = "Sincronización incompleta.",
                    Percentage = 20,
                    IsError = true,
                    ErrorMessage = "Existen registros en cola local sin sincronizar y no se pudo asegurar la conexión con el servidor. Para proteger la información de ventas y turnos, la actualización se ha pospuesto."
                });
                return false;
            }
        }

        // 2. COPIA DE SEGURIDAD PREVENTIVA DE LA BASE DE DATOS LOCAL SQLITE
        progress?.Report(new UpdateProgressReport
        {
            StepDescription = "Generando copia de seguridad preventiva de la base de datos local...",
            Percentage = 35
        });

        try
        {
            var backupPath = await _dbManager.BackupDatabaseAsync();
            if (!string.IsNullOrEmpty(backupPath))
            {
                Debug.WriteLine($"[BACKUP LOCAL] Base de datos respaldada con éxito en: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BACKUP LOCAL WARNING] No se pudo generar backup automático preventivo: {ex.Message}");
        }

        // 3. DESCARGA AUTENTICADA DEL PAQUETE ZIP
        progress?.Report(new UpdateProgressReport
        {
            StepDescription = "Descargando paquete de actualización firmado...",
            Percentage = 50
        });

        var tempDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ParkFlow", "Temp");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var tempZipPath = Path.Combine(tempDir, $"update_v{release.LatestVersion}.zip");

        try
        {
            var downloadUrl = string.IsNullOrWhiteSpace(release.DownloadEndpoint)
                ? $"api/v1/app-update/download/{release.LatestVersion}"
                : release.DownloadEndpoint.TrimStart('/');

            var license = _licenseService.GetCurrentLicense();
            using var downloadReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (license != null && !string.IsNullOrWhiteSpace(license.DeviceToken))
            {
                downloadReq.Headers.Add("X-Device-Token", license.DeviceToken);
            }
            downloadReq.Headers.Add("X-Machine-Fingerprint", _fingerprintService.GetMachineFingerprint());

            var downloadResp = await _httpClient.SendAsync(downloadReq, HttpCompletionOption.ResponseHeadersRead);
            if (!downloadResp.IsSuccessStatusCode)
            {
                progress?.Report(new UpdateProgressReport
                {
                    StepDescription = "Error en la descarga del paquete.",
                    Percentage = 50,
                    IsError = true,
                    ErrorMessage = $"El servidor respondió con código {downloadResp.StatusCode} al descargar la versión."
                });
                return false;
            }

            await using (var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await downloadResp.Content.CopyToAsync(fs);
            }

            // 4. VERIFICACIÓN CRIPTOGRÁFICA DEL HASH SHA-256
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = "Validando firma e integridad criptográfica SHA-256...",
                Percentage = 80
            });

            if (!string.IsNullOrWhiteSpace(release.PackageSha256))
            {
                await using var checkFs = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var sha = SHA256.Create();
                var hashBytes = await sha.ComputeHashAsync(checkFs);
                var computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                if (!string.Equals(computedHash, release.PackageSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    checkFs.Close();
                    File.Delete(tempZipPath);

                    progress?.Report(new UpdateProgressReport
                    {
                        StepDescription = "Falla de integridad criptográfica.",
                        Percentage = 80,
                        IsError = true,
                        ErrorMessage = "El paquete descargado no coincide con la firma digital oficial del servidor. El archivo ha sido eliminado por seguridad."
                    });
                    return false;
                }
            }

            // 5. INVOCACIÓN DEL MICRO-UPDATER Y CIERRE ORDENADO DE LA APLICACIÓN
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = "Iniciando proceso de actualización en caliente...",
                Percentage = 95
            });

            var targetDir = AppDomain.CurrentDomain.BaseDirectory;
            var updaterExe = Path.Combine(targetDir, "ParkFlow.Updater.exe");

            if (!File.Exists(updaterExe))
            {
                // Fallback para pruebas en desarrollo: buscar en carpeta contigua de binarios
                var altUpdater = Path.Combine(targetDir, "..", "..", "..", "..", "ParkFlow.Updater", "bin", "Debug", "net10.0-windows", "ParkFlow.Updater.exe");
                if (File.Exists(altUpdater))
                {
                    updaterExe = Path.GetFullPath(altUpdater);
                }
            }

            if (!File.Exists(updaterExe))
            {
                progress?.Report(new UpdateProgressReport
                {
                    StepDescription = "No se encontró el actualizador auxiliar.",
                    Percentage = 95,
                    IsError = true,
                    ErrorMessage = "No se localizó el componente ParkFlow.Updater.exe en el directorio de la aplicación."
                });
                return false;
            }

            var currentPid = Environment.ProcessId;
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = $"--pid {currentPid} --zip \"{tempZipPath}\" --target \"{targetDir}\" --exe \"Parking.exe\" --sha256 \"{release.PackageSha256}\"",
                UseShellExecute = true
            };

            Process.Start(startInfo);

            // Cierre limpio
            var app = Application.Current;
            if (app != null)
            {
                if (app.Dispatcher.CheckAccess())
                {
                    app.Shutdown();
                }
                else
                {
                    app.Dispatcher.Invoke(() => app.Shutdown());
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = "Excepción durante la actualización.",
                Percentage = 0,
                IsError = true,
                ErrorMessage = $"Error no esperado: {ex.Message}"
            });
            return false;
        }
    }
}
