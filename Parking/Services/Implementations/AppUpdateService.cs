using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
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
    private readonly System.Windows.Threading.DispatcherTimer _periodicTimer = new();

    public event Action<AppReleaseInfoDto>? UpdateDetected;

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
        // 1. COPIA DE SEGURIDAD PREVENTIVA DE LA BASE DE DATOS LOCAL SQLITE
        progress?.Report(new UpdateProgressReport
        {
            StepDescription = "Generando copia de seguridad preventiva de la base de datos local...",
            Percentage = 15
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

        // 2. REGLA DE ORO DE SEGURIDAD: GARANTÍA DE SINCRONIZACIÓN AL 100% DE DATOS LOCALES
        if (_syncEngine.PendingItemsCount > 0)
        {
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = $"Sincronizando {_syncEngine.PendingItemsCount} transacciones locales pendientes con el servidor central...",
                Percentage = 30
            });

            try
            {
                await _syncEngine.PerformFullSyncAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SYNC PRE-UPDATE ERROR] Sincronización previa no completada: {ex.Message}");
            }

            if (_syncEngine.PendingItemsCount > 0)
            {
                // REGLA DE ORO: ABORTAR ACTUALIZACIÓN SI NO SE PUDO SUBIR TODO A LA NUBE
                progress?.Report(new UpdateProgressReport
                {
                    StepDescription = "No se puede aplicar la actualización: existen registros locales pendientes que no pudieron subirse a la nube.",
                    Percentage = 30,
                    IsError = true,
                    ErrorMessage = $"Imposible actualizar: Existen {_syncEngine.PendingItemsCount} transacciones locales pendientes por subir al servidor central. Por seguridad e integridad de datos, la actualización solo se ejecutará cuando todas las transacciones estén sincronizadas en la nube."
                });
                return false;
            }
        }

        // 3. DESCARGA AUTENTICADA DEL PAQUETE ZIP
        progress?.Report(new UpdateProgressReport
        {
            StepDescription = "Estableciendo conexión y preparando descarga...",
            Percentage = 25
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

            // CRÍTICO: Crear un HttpClient DEDICADO y AISLADO para la descarga.
            // El HttpClient singleton es compartido con PingAsync/SyncEngine que usan
            // CancellationTokens de 8s. Cuando un health-check se cancela, el SocketsHttpHandler
            // puede matar la conexión TCP activa de descarga (comparten pool de sockets).
            var downloadHandler = new HttpClientHandler();
            var baseUri = _httpClient.BaseAddress;
            if (baseUri != null && (baseUri.Host.Contains("localhost") || baseUri.Host.Contains("127.0.0.1")))
            {
                downloadHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }

            using var downloadClient = new HttpClient(downloadHandler)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromMinutes(10) // 10 minutos exclusivos para la descarga
            };

            var license = _licenseService.GetCurrentLicense();
            using var downloadReq = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (license != null && !string.IsNullOrWhiteSpace(license.DeviceToken))
            {
                downloadReq.Headers.Add("X-Device-Token", license.DeviceToken);
            }
            downloadReq.Headers.Add("X-Machine-Fingerprint", _fingerprintService.GetMachineFingerprint());

            using var downloadResp = await downloadClient.SendAsync(downloadReq, HttpCompletionOption.ResponseHeadersRead);
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

            var totalBytes = downloadResp.Content.Headers.ContentLength ?? (release.PackageSizeBytes > 0 ? release.PackageSizeBytes : 65L * 1024 * 1024);
            var downloadedBytes = 0L;
            var buffer = new byte[81920]; // 80 KB: Tamaño óptimo para evitar LOH y alineado con TCP Window

            await using var responseStream = await downloadResp.Content.ReadAsStreamAsync();
            await using var fs = new FileStream(tempZipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: false);

            int bytesRead;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                // Timeout individual por lectura: 60 segundos de inactividad máxima por chunk
                using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                bytesRead = await responseStream.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token);

                if (bytesRead == 0)
                    break;

                fs.Write(buffer, 0, bytesRead); // Escritura síncrona directa: evita overhead async en FileStream no-async
                downloadedBytes += bytesRead;

                if (stopwatch.ElapsedMilliseconds >= 250 || downloadedBytes >= totalBytes)
                {
                    var progressFraction = totalBytes > 0 ? Math.Min(1.0, (double)downloadedBytes / totalBytes) : 0.5;
                    var percent = 30 + (int)(progressFraction * 55.0);
                    var mbDownloaded = downloadedBytes / (1024.0 * 1024.0);
                    var mbTotal = totalBytes / (1024.0 * 1024.0);

                    progress?.Report(new UpdateProgressReport
                    {
                        StepDescription = $"Descargando actualización: {mbDownloaded:F1} MB de {mbTotal:F1} MB ({percent}%)...",
                        Percentage = Math.Min(85, percent)
                    });
                    stopwatch.Restart();
                }
            }

            fs.Flush();

            // Cerrar explícitamente los streams antes de la verificación SHA-256
            // porque fs tiene FileShare.None y bloquearía la apertura para lectura
            await fs.DisposeAsync();
            await responseStream.DisposeAsync();

            // 4. VERIFICACIÓN CRIPTOGRÁFICA DEL HASH SHA-256
            progress?.Report(new UpdateProgressReport
            {
                StepDescription = "Validando firma e integridad criptográfica SHA-256...",
                Percentage = 90
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

            var cleanTargetDir = Path.GetFullPath(targetDir).TrimEnd('\\', '/');
            var cleanZipPath = Path.GetFullPath(tempZipPath).TrimEnd('\\', '/');
            var cleanSha256 = (release.PackageSha256 ?? string.Empty).Trim();

            var currentPid = Environment.ProcessId;
            var startInfo = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = $"--pid {currentPid} --zip \"{cleanZipPath}\" --target \"{cleanTargetDir}\" --exe \"Parking.exe\" --sha256 \"{cleanSha256}\"",
                UseShellExecute = true
            };

            // Cierre formal de la sesión activa para garantizar que la nueva versión arranque en Login
            try
            {
                _sessionService.Clear();
            }
            catch { }

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

    public void StartHourlyUpdateCheck()
    {
        _periodicTimer.Interval = TimeSpan.FromHours(1);
        _periodicTimer.Tick -= PeriodicTimer_Tick;
        _periodicTimer.Tick += PeriodicTimer_Tick;
        _periodicTimer.Start();
    }

    public void StopHourlyUpdateCheck()
    {
        _periodicTimer.Stop();
    }

    private async void PeriodicTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            var release = await CheckForUpdateAsync();
            if (release != null && release.HasUpdate)
            {
                UpdateDetected?.Invoke(release);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[HOURLY UPDATE CHECK ERROR] {ex.Message}");
        }
    }
}
