using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class AgreementService : IAgreementService
{
    private readonly IDbConnectionManager _connectionManager;

    public AgreementService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<IReadOnlyList<CommercialAgreement>> GetAllAgreementsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.CommercialAgreements
            .Include(a => a.Store)
            .OrderBy(a => a.Store.Name)
            .ThenBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CommercialAgreement>> GetAgreementsByStoreAsync(Guid storeId)
    {
        using var db = _connectionManager.CreateDbContext();
        var items = await db.CommercialAgreements
            .Where(a => a.StoreId == storeId && a.IsActive)
            .Include(a => a.Store)
            .ToListAsync();

        return items.OrderBy(a => a.MinPurchaseAmount).ToList();
    }

    public async Task<CommercialAgreement> CreateAgreementAsync(Guid storeId, string name, decimal minPurchaseAmount, decimal? discountPercentage, decimal? discountFixedAmount, int? maxHoursApplicable)
    {
        using var db = _connectionManager.CreateDbContext();
        var agreement = new CommercialAgreement
        {
            AgreementId = Guid.NewGuid(),
            StoreId = storeId,
            Name = name.Trim(),
            MinPurchaseAmount = minPurchaseAmount,
            DiscountPercentage = discountPercentage,
            DiscountFixedAmount = discountFixedAmount,
            MaxHoursApplicable = maxHoursApplicable,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.CommercialAgreements.Add(agreement);
        await db.SaveChangesAsync();

        agreement.Store = (await db.Stores.FindAsync(storeId))!;
        return agreement;
    }

    public async Task UpdateAgreementAsync(Guid agreementId, string name, decimal minPurchaseAmount, decimal? discountPercentage, decimal? discountFixedAmount, int? maxHoursApplicable, bool isActive)
    {
        using var db = _connectionManager.CreateDbContext();
        var agreement = await db.CommercialAgreements.FindAsync(agreementId);
        if (agreement == null)
        {
            throw new KeyNotFoundException("Convenio no encontrado.");
        }

        agreement.Name = name.Trim();
        agreement.MinPurchaseAmount = minPurchaseAmount;
        agreement.DiscountPercentage = discountPercentage;
        agreement.DiscountFixedAmount = discountFixedAmount;
        agreement.MaxHoursApplicable = maxHoursApplicable;
        agreement.IsActive = isActive;

        await db.SaveChangesAsync();
    }

    public decimal CalculateDiscount(CommercialAgreement agreement, decimal purchaseAmount, decimal grossTicketAmount)
    {
        if (agreement == null || !agreement.IsActive || purchaseAmount < agreement.MinPurchaseAmount || grossTicketAmount <= 0)
        {
            return 0m;
        }

        decimal discount = 0m;
        if (agreement.DiscountPercentage.HasValue && agreement.DiscountPercentage.Value > 0)
        {
            discount = grossTicketAmount * (agreement.DiscountPercentage.Value / 100m);
        }
        else if (agreement.DiscountFixedAmount.HasValue && agreement.DiscountFixedAmount.Value > 0)
        {
            discount = agreement.DiscountFixedAmount.Value;
        }

        return Math.Min(grossTicketAmount, Math.Max(0m, discount));
    }
}
