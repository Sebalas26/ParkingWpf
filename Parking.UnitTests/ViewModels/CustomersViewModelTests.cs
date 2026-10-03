using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;
using Parking.ViewModels;
using Xunit;

namespace Parking.UnitTests.ViewModels;

public class CustomersViewModelTests
{
    private readonly Mock<IDbConnectionManager> _mockConnectionManager;
    private readonly Mock<IApiClientService> _mockApiClient;
    private readonly Mock<ISessionService> _mockSessionService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IPermissionService> _mockPermissionService;

    public CustomersViewModelTests()
    {
        _mockConnectionManager = new Mock<IDbConnectionManager>();
        _mockApiClient = new Mock<IApiClientService>();
        _mockSessionService = new Mock<ISessionService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockPermissionService = new Mock<IPermissionService>();

        _mockPermissionService.Setup(p => p.HasPermission(It.IsAny<string>())).Returns(true);
    }

    [Theory]
    [InlineData("901234567", "7")]
    [InlineData("800197268", "4")]
    [InlineData("900336004", "7")]
    [InlineData("890900608", "9")]
    public void CalculateNitCheckDigit_ReturnsCorrectDianCheckDigit(string nit, string expectedDv)
    {
        var dv = CustomersViewModel.CalculateNitCheckDigit(nit);
        dv.Should().Be(expectedDv);
    }

    [Fact]
    public void CalculateNitCheckDigit_WithEmptyOrNull_ReturnsEmpty()
    {
        CustomersViewModel.CalculateNitCheckDigit(string.Empty).Should().BeEmpty();
        CustomersViewModel.CalculateNitCheckDigit("   ").Should().BeEmpty();
    }

    [Fact]
    public void OpenCreateCustomer_InitializesWithEmptyCleanFields()
    {
        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object);

        vm.OpenCreateCustomerCommand.Execute(null);

        vm.IsFormOpen.Should().BeTrue();
        vm.IsEditing.Should().BeFalse();
        vm.FormCustomerId.Should().BeNull();
        vm.FormDocumentNumber.Should().BeEmpty();
        vm.FormFullName.Should().BeEmpty();
        vm.FormEmail.Should().BeEmpty();
        vm.FormPhone.Should().BeEmpty();
        vm.FormAddress.Should().BeEmpty();
        vm.FormCityCode.Should().BeEmpty();
        vm.FormCheckDigit.Should().BeNull();
        vm.FormDocumentError.Should().BeNull();
        vm.FormFullNameError.Should().BeNull();
        vm.FormEmailError.Should().BeNull();
        vm.FormGeneralError.Should().BeNull();
    }

    [Fact]
    public void OpenEditCustomer_PopulatesFormWithCustomerData()
    {
        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            IdentificationTypeId = 31,
            DocumentNumber = "901234567",
            CheckDigit = "1",
            PersonType = "Company",
            FullName = "Empresa ABC S.A.S.",
            TradeName = "ABC",
            Email = "facturacion@abc.com",
            Phone = "3001234567",
            Address = "Calle 10 # 20-30",
            CityCode = "11001"
        };

        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object);

        vm.OpenEditCustomerCommand.Execute(customer);

        vm.IsFormOpen.Should().BeTrue();
        vm.IsEditing.Should().BeTrue();
        vm.FormCustomerId.Should().Be(customer.CustomerId);
        vm.FormIdentificationTypeId.Should().Be(3);
        vm.FormDocumentNumber.Should().Be("901234567");
        vm.FormCheckDigit.Should().Be("1");
        vm.FormPersonType.Should().Be("Company");
        vm.FormFullName.Should().Be("Empresa ABC S.A.S.");
        vm.FormTradeName.Should().Be("ABC");
        vm.FormEmail.Should().Be("facturacion@abc.com");
        vm.FormPhone.Should().Be("3001234567");
        vm.FormAddress.Should().Be("Calle 10 # 20-30");
        vm.FormCityCode.Should().Be("11001");
    }

    [Fact]
    public async Task SaveCustomerCommand_WithInvalidForm_SetsValidationErrors()
    {
        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object);

        vm.OpenCreateCustomerCommand.Execute(null);
        vm.FormDocumentNumber = "";
        vm.FormFullName = "";
        vm.FormEmail = "not-an-email";

        await vm.SaveCustomerCommand.ExecuteAsync(null);

        vm.FormDocumentError.Should().NotBeNullOrEmpty();
        vm.FormFullNameError.Should().NotBeNullOrEmpty();
        vm.FormEmailError.Should().NotBeNullOrEmpty();
        vm.FormAddressError.Should().NotBeNullOrEmpty();
        vm.FormCityCodeError.Should().NotBeNullOrEmpty();
        vm.FormGeneralError.Should().NotBeNullOrEmpty();
        vm.IsFormOpen.Should().BeTrue();
    }

    [Fact]
    public void ClearSearchCommand_ResetsSearchText()
    {
        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object)
        {
            SearchText = "Empresa"
        };

        vm.HasSearchText.Should().BeTrue();
        vm.ClearSearchCommand.Execute(null);

        vm.SearchText.Should().BeEmpty();
        vm.HasSearchText.Should().BeFalse();
    }

    [Fact]
    public void IsNitSelected_WhenDocumentTypeChanges_TogglesCorrectlyAndClearsDvWhenNotNit()
    {
        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object);

        // Inicial por defecto es CC (1)
        vm.FormIdentificationTypeId.Should().Be(1);
        vm.IsNitSelected.Should().BeFalse();
        vm.FormCheckDigit.Should().BeNull();

        // Cambiar a NIT (3) con número de documento
        vm.FormDocumentNumber = "900336004";
        vm.FormIdentificationTypeId = 3;

        vm.IsNitSelected.Should().BeTrue();
        vm.FormPersonType.Should().Be("Company");
        vm.FormCheckDigit.Should().Be("7"); // Dígito DIAN calculado para 900336004

        // Cambiar de nuevo a CC (1)
        vm.FormIdentificationTypeId = 1;

        vm.IsNitSelected.Should().BeFalse();
        vm.FormPersonType.Should().Be("Person");
        vm.FormCheckDigit.Should().BeNull(); // Debe limpiarse al no ser NIT
    }

    [Theory]
    [InlineData("usuario@dominio.c", false)]
    [InlineData("usuario@dominio", false)]
    [InlineData("usuario@.com", false)]
    [InlineData("usuario@dominio.co", true)]
    [InlineData("usuario@dominio.com", true)]
    public async Task ValidateForm_EmailValidation_StrictFormat(string email, bool shouldBeValid)
    {
        var vm = new CustomersViewModel(
            _mockConnectionManager.Object,
            _mockApiClient.Object,
            _mockSessionService.Object,
            _mockDialogService.Object,
            _mockPermissionService.Object);

        vm.OpenCreateCustomerCommand.Execute(null);
        vm.FormDocumentNumber = "12345678";
        vm.FormFullName = "Test User";
        vm.FormAddress = "Calle 123";
        vm.FormCityCode = "11001";
        vm.FormEmail = email;

        await vm.SaveCustomerCommand.ExecuteAsync(null);

        if (shouldBeValid)
        {
            vm.FormEmailError.Should().BeNull();
        }
        else
        {
            vm.FormEmailError.Should().NotBeNullOrEmpty();
        }
    }
}

