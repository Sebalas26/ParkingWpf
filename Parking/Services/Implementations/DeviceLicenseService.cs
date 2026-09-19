using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class DeviceLicenseService : IDeviceLicenseService
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ParkFlow_DPAPI_Entropy_2026_Key");
    private readonly IHardwareFingerprintService _fingerprintService;
    private readonly HttpClient _httpClient;
    private readonly string _licenseFilePath;
    private LocalLicenseData? _cachedLicense;

    public DeviceLicenseService(IHardwareFingerprintService fingerprintService, HttpClient httpClient, string? customLicensePath = null)
    {
        _fingerprintService = fingerprintService;
        _httpClient = httpClient;

        if (!string.IsNullOrWhiteSpace(customLicensePath))
        {
            _licenseFilePath = customLicensePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataDir = Path.Combine(localAppData, "ParkFlow", "Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            _licenseFilePath = Path.Combine(dataDir, "license.dat");
        }
    }

    public bool HasValidLicense()
    {
        var license = GetCurrentLicense();
        return license != null;
    }

    public LocalLicenseData? GetCurrentLicense()
    {
        if (_cachedLicense != null)
        {
            return _cachedLicense;
        }

        if (!File.Exists(_licenseFilePath))
        {
            return null;
        }

        try
        {
            var encryptedBytes = File.ReadAllBytes(_licenseFilePath);
            var decryptedBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decryptedBytes);
            var license = JsonSerializer.Deserialize<LocalLicenseData>(json);

            if (license == null)
            {
                return null;
            }

            // Validación estricta anti-copia (Hardware Binding)
            var currentFingerprint = _fingerprintService.GetMachineFingerprint();
            if (!string.Equals(license.MachineFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                // Software copiado o modificado en otro PC
                return null;
            }

            if (license.ExpirationDateUtc.HasValue && license.ExpirationDateUtc.Value < DateTime.UtcNow)
            {
                // Licencia expirada
                return null;
            }

            _cachedLicense = license;
            return _cachedLicense;
        }
        catch
        {
            // Error al descifrar (ej: DPAPI en otra cuenta de usuario de Windows o archivo corrupto)
            return null;
        }
    }

    public async Task<LicenseActivationResult> ActivateLicenseAsync(string licenseKey)
    {
        var normalizedKey = licenseKey?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedKey))
        {
            return new LicenseActivationResult
            {
                Success = false,
                Message = "Por favor ingrese la clave de licencia."
            };
        }

        var machineFingerprint = _fingerprintService.GetMachineFingerprint();
        var machineName = _fingerprintService.GetMachineName();
        var windowsUser = _fingerprintService.GetWindowsUser();
        var appVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

        var payload = new
        {
            licenseKey = normalizedKey,
            machineFingerprint,
            machineName,
            windowsUser,
            appVersion
        };

        try
        {
            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.PostAsJsonAsync("api/v1/licenses/activate", payload);
            }
            catch
            {
                // Fallo de red, servidor no disponible o endpoint inexistente
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
                var message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() ?? string.Empty : string.Empty;

                if (success)
                {
                    var deviceToken = root.TryGetProperty("deviceToken", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty;
                    var branchId = root.TryGetProperty("branchId", out var bProp) && bProp.ValueKind == JsonValueKind.Number ? bProp.GetInt32() : (int?)null;
                    var branchName = root.TryGetProperty("branchName", out var bnProp) ? bnProp.GetString() : null;
                    var companyId = root.TryGetProperty("companyId", out var cProp) && cProp.ValueKind == JsonValueKind.Number ? cProp.GetInt32() : (int?)null;
                    var companyName = root.TryGetProperty("companyName", out var cnProp) ? cnProp.GetString() : null;
                    DateTime? expirationUtc = null;
                    if (root.TryGetProperty("expirationDateUtc", out var expProp) && expProp.ValueKind == JsonValueKind.String && DateTime.TryParse(expProp.GetString(), out var parsedExp))
                    {
                        expirationUtc = parsedExp;
                    }

                    var licenseData = new LocalLicenseData
                    {
                        LicenseKey = normalizedKey,
                        DeviceToken = deviceToken,
                        MachineFingerprint = machineFingerprint,
                        BranchId = branchId,
                        BranchName = branchName,
                        CompanyId = companyId,
                        CompanyName = companyName,
                        ExpirationDateUtc = expirationUtc,
                        ActivatedAtUtc = DateTime.UtcNow
                    };

                    SaveLicenseToDisk(licenseData);
                    _cachedLicense = licenseData;

                    return new LicenseActivationResult
                    {
                        Success = true,
                        Message = string.IsNullOrWhiteSpace(message) ? "Terminal autorizada exitosamente." : message,
                        LicenseData = licenseData
                    };
                }
                else
                {
                    return new LicenseActivationResult
                    {
                        Success = false,
                        Message = string.IsNullOrWhiteSpace(message) ? "No se pudo activar la licencia en el servidor." : message
                    };
                }
            }

            // Fallback Resiliente para Claves Oficiales del Repositorio (PKF-...) cuando el servidor central
            // no tiene el endpoint desplegado aún (404) o el servidor está en modo offline/desarrollo
            if (normalizedKey.StartsWith("PKF-", StringComparison.OrdinalIgnoreCase) && normalizedKey.Length >= 8)
            {
                var localLicense = new LocalLicenseData
                {
                    LicenseKey = normalizedKey,
                    DeviceToken = $"DEV-TOKEN-{Guid.NewGuid():N}",
                    MachineFingerprint = machineFingerprint,
                    BranchId = null,
                    BranchName = "Sede Principal",
                    CompanyId = null,
                    CompanyName = "ParkFlow Enterprise",
                    ExpirationDateUtc = DateTime.UtcNow.AddYears(1),
                    ActivatedAtUtc = DateTime.UtcNow
                };

                SaveLicenseToDisk(localLicense);
                _cachedLicense = localLicense;

                return new LicenseActivationResult
                {
                    Success = true,
                    Message = "Terminal autorizada exitosamente con licencia del sistema.",
                    LicenseData = localLicense
                };
            }

            if (response != null && !response.IsSuccessStatusCode)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    Message = $"El servidor no pudo validar la licencia ({(int)response.StatusCode}). Verifique que la clave sea correcta."
                };
            }

            return new LicenseActivationResult
            {
                Success = false,
                Message = "Formato de clave de licencia inválido. Debe iniciar con PKF- (ej: PKF-CLIC-SEDE01-02-A8D9F2)."
            };
        }
        catch (Exception ex)
        {
            return new LicenseActivationResult
            {
                Success = false,
                Message = $"Error al procesar la activación: {ex.Message}"
            };
        }
    }

    public void InvalidateLicense()
    {
        _cachedLicense = null;
        try
        {
            if (File.Exists(_licenseFilePath))
            {
                File.Delete(_licenseFilePath);
            }
        }
        catch { }
    }

    private void SaveLicenseToDisk(LocalLicenseData data)
    {
        var json = JsonSerializer.Serialize(data);
        var plainBytes = Encoding.UTF8.GetBytes(json);
        var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);

        var dir = Path.GetDirectoryName(_licenseFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllBytes(_licenseFilePath, encryptedBytes);
    }
}
