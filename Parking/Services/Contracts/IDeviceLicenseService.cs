using System.Threading.Tasks;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IDeviceLicenseService
{
    bool HasValidLicense();
    LocalLicenseData? GetCurrentLicense();
    Task<LicenseActivationResult> ActivateLicenseAsync(string licenseKey);
    void InvalidateLicense();
}
