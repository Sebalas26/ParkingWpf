using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IAgreementService
{
    Task<IReadOnlyList<CommercialAgreement>> GetAllAgreementsAsync();
    Task<IReadOnlyList<CommercialAgreement>> GetAgreementsByStoreAsync(Guid storeId);
    Task<CommercialAgreement> CreateAgreementAsync(Guid storeId, string name, decimal minPurchaseAmount, decimal? discountPercentage, decimal? discountFixedAmount, int? maxHoursApplicable);
    Task UpdateAgreementAsync(Guid agreementId, string name, decimal minPurchaseAmount, decimal? discountPercentage, decimal? discountFixedAmount, int? maxHoursApplicable, bool isActive);
    decimal CalculateDiscount(CommercialAgreement agreement, decimal purchaseAmount, decimal grossTicketAmount);
}
