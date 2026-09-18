namespace Parking.Services.Contracts;

public interface IHardwareFingerprintService
{
    string GetMachineFingerprint();
    string GetMachineName();
    string GetWindowsUser();
}
