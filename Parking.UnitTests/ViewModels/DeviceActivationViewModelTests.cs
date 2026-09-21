using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Models;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class DeviceActivationViewModelTests
{
    private readonly Mock<IDeviceLicenseService> _mockLicenseService;
    private readonly Mock<IHardwareFingerprintService> _mockFingerprintService;

    public DeviceActivationViewModelTests()
    {
        _mockLicenseService = new Mock<IDeviceLicenseService>();
        _mockFingerprintService = new Mock<IHardwareFingerprintService>();

        _mockFingerprintService.Setup(f => f.GetMachineFingerprint()).Returns("FP-SHA256-TEST");
        _mockFingerprintService.Setup(f => f.GetMachineName()).Returns("TEST-PC");
        _mockFingerprintService.Setup(f => f.GetWindowsUser()).Returns("Operator1");
    }

    [Fact]
    public void Constructor_InitializesHardwareInformationCorrectly()
    {
        var vm = new DeviceActivationViewModel(_mockLicenseService.Object, _mockFingerprintService.Object);

        vm.MachineFingerprint.Should().Be("FP-SHA256-TEST");
        vm.MachineName.Should().Be("TEST-PC");
        vm.WindowsUser.Should().Be("Operator1");
        vm.LicenseKey.Should().BeEmpty();
        vm.ErrorMessage.Should().BeNull();
        vm.SuccessMessage.Should().BeNull();
        vm.IsBusy.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ActivateCommand_WhenLicenseKeyIsEmpty_SetsErrorMessageWithoutCallingService(string? key)
    {
        var vm = new DeviceActivationViewModel(_mockLicenseService.Object, _mockFingerprintService.Object)
        {
            LicenseKey = key!
        };

        var completedFired = false;
        vm.ActivationCompleted += () => completedFired = true;

        await vm.ActivateCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Debe ingresar la clave de licencia asignada a esta sede.");
        completedFired.Should().BeFalse();
        _mockLicenseService.Verify(s => s.ActivateLicenseAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ActivateCommand_WhenActivationFails_SetsErrorMessageAndDoesNotFireCompleted()
    {
        _mockLicenseService.Setup(s => s.ActivateLicenseAsync("PKF-INVALID-KEY"))
            .ReturnsAsync(new LicenseActivationResult
            {
                Success = false,
                Message = "Licencia no encontrada o inactiva."
            });

        var vm = new DeviceActivationViewModel(_mockLicenseService.Object, _mockFingerprintService.Object)
        {
            LicenseKey = "PKF-INVALID-KEY"
        };

        var completedFired = false;
        vm.ActivationCompleted += () => completedFired = true;

        await vm.ActivateCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Licencia no encontrada o inactiva.");
        vm.SuccessMessage.Should().BeNull();
        completedFired.Should().BeFalse();
    }

    [Fact]
    public async Task ActivateCommand_WhenActivationSucceeds_FiresActivationCompletedEvent()
    {
        _mockLicenseService.Setup(s => s.ActivateLicenseAsync("PKF-VALID-KEY-123"))
            .ReturnsAsync(new LicenseActivationResult
            {
                Success = true,
                Message = "Terminal autorizada exitosamente."
            });

        var vm = new DeviceActivationViewModel(_mockLicenseService.Object, _mockFingerprintService.Object)
        {
            LicenseKey = "PKF-VALID-KEY-123"
        };

        var completedFired = false;
        vm.ActivationCompleted += () => completedFired = true;

        await vm.ActivateCommand.ExecuteAsync(null);

        vm.SuccessMessage.Should().Be("¡Terminal autorizada exitosamente!");
        vm.ErrorMessage.Should().BeNull();
        completedFired.Should().BeTrue();
    }

    [Fact]
    public void CancelCommand_InvokesCancelRequestedEvent()
    {
        var vm = new DeviceActivationViewModel(_mockLicenseService.Object, _mockFingerprintService.Object);

        var cancelFired = false;
        vm.CancelRequested += () => cancelFired = true;

        vm.CancelCommand.Execute(null);

        cancelFired.Should().BeTrue();
    }
}
