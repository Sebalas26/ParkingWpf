using System;
using System.Threading.Tasks;
using FluentAssertions;
using Parking.Entities;
using Parking.Services.Implementations;
using Parking.UnitTests.Common;
using Xunit;

namespace Parking.UnitTests.Agreements;

public class AgreementServiceTests : IDisposable
{
    private readonly TestDbConnectionManager _connectionManager;
    private readonly AgreementService _service;
    private readonly Store _testStore;

    public AgreementServiceTests()
    {
        _connectionManager = new TestDbConnectionManager();
        _service = new AgreementService(_connectionManager);

        _testStore = new Store
        {
            StoreId = Guid.NewGuid(),
            Name = "Éxito Supermercado",
            IsActive = true
        };

        using var db = _connectionManager.CreateDbContext();
        db.Stores.Add(_testStore);
        db.SaveChanges();
    }

    [Fact]
    public void CalculateDiscount_PercentageDiscount_CalculatesAccurately()
    {
        // Arrange
        var agreement = new CommercialAgreement
        {
            IsActive = true,
            MinPurchaseAmount = 20000m,
            DiscountPercentage = 50m // 50% de descuento
        };

        // Act: Compra de 50.000 (supera los 20.000), tarifa bruta 10.000
        var discount = _service.CalculateDiscount(agreement, purchaseAmount: 50000m, grossTicketAmount: 10000m);

        // Assert: 50% de 10.000 = 5.000
        discount.Should().Be(5000m);
    }

    [Fact]
    public void CalculateDiscount_PurchaseAmountBelowMinimum_ReturnsZero()
    {
        // Arrange
        var agreement = new CommercialAgreement
        {
            IsActive = true,
            MinPurchaseAmount = 50000m,
            DiscountPercentage = 50m
        };

        // Act: Compra de 30.000 (menor al mínimo de 50.000)
        var discount = _service.CalculateDiscount(agreement, purchaseAmount: 30000m, grossTicketAmount: 10000m);

        // Assert
        discount.Should().Be(0m);
    }

    [Fact]
    public void CalculateDiscount_FixedAmount_ReturnsFixedAmountCappedAtGrossTicket()
    {
        // Arrange
        var agreement = new CommercialAgreement
        {
            IsActive = true,
            MinPurchaseAmount = 10000m,
            DiscountFixedAmount = 4000m
        };

        // Act: Tarifa bruta de 10.000
        var discount = _service.CalculateDiscount(agreement, purchaseAmount: 20000m, grossTicketAmount: 10000m);

        // Assert
        discount.Should().Be(4000m);

        // Si la tarifa bruta es menor al descuento fijo (ej: 3.000): se topa a 3.000
        var cappedDiscount = _service.CalculateDiscount(agreement, purchaseAmount: 20000m, grossTicketAmount: 3000m);
        cappedDiscount.Should().Be(3000m);
    }

    [Fact]
    public void CalculateDiscount_InactiveAgreement_ReturnsZero()
    {
        // Arrange
        var agreement = new CommercialAgreement
        {
            IsActive = false,
            MinPurchaseAmount = 0m,
            DiscountPercentage = 100m
        };

        // Act
        var discount = _service.CalculateDiscount(agreement, purchaseAmount: 50000m, grossTicketAmount: 10000m);

        // Assert
        discount.Should().Be(0m);
    }

    [Fact]
    public async Task CreateAndRetrieveAgreements_PersistsInDatabase()
    {
        // Act
        var created = await _service.CreateAgreementAsync(
            _testStore.StoreId,
            "Convenio 2 Horas Gratis",
            minPurchaseAmount: 30000m,
            discountPercentage: null,
            discountFixedAmount: null,
            maxHoursApplicable: 2);

        var retrieved = await _service.GetAgreementsByStoreAsync(_testStore.StoreId);

        // Assert
        created.Should().NotBeNull();
        created.Name.Should().Be("Convenio 2 Horas Gratis");
        retrieved.Should().ContainSingle(a => a.AgreementId == created.AgreementId);
    }

    public void Dispose()
    {
        _connectionManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
