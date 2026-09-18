using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class HardwareFingerprintService : IHardwareFingerprintService
{
    private const string InternalSalt = "ParkFlow_AntiPiracy_Salt_2026_Secured_HW";
    private string? _cachedFingerprint;

    public string GetMachineName() => Environment.MachineName;
    public string GetWindowsUser() => Environment.UserName;

    public string GetMachineFingerprint()
    {
        if (!string.IsNullOrEmpty(_cachedFingerprint))
        {
            return _cachedFingerprint;
        }

        var motherboardSerial = GetWmiValue("Win32_BaseBoard", "SerialNumber");
        var cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
        var biosUuid = GetWmiValue("Win32_ComputerSystemProduct", "UUID");

        // Fallbacks defensivos si WMI está restringido en el entorno
        if (string.IsNullOrWhiteSpace(motherboardSerial) || motherboardSerial.Equals("To be filled by O.E.M.", StringComparison.OrdinalIgnoreCase))
        {
            motherboardSerial = Environment.MachineName;
        }

        if (string.IsNullOrWhiteSpace(cpuId))
        {
            cpuId = Environment.ProcessorCount.ToString();
        }

        if (string.IsNullOrWhiteSpace(biosUuid) || biosUuid.Equals("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", StringComparison.OrdinalIgnoreCase))
        {
            biosUuid = Environment.OSVersion.VersionString;
        }

        var combined = $"{motherboardSerial}|{cpuId}|{biosUuid}|{InternalSalt}";

        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(combined));
        _cachedFingerprint = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return _cachedFingerprint;
    }

    private static string GetWmiValue(string wmiClass, string property)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return string.Empty;
            }

            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                var val = obj[property]?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    return val;
                }
            }
        }
        catch
        {
            // Fallback defensivo si el servicio WMI no responde
        }
        return string.Empty;
    }
}
