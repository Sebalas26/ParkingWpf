using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IBillingResolutionService
{
    Task<IReadOnlyList<BillingResolution>> GetAllResolutionsAsync();
    Task<IReadOnlyList<BillingResolution>> GetActiveResolutionsByBranchAsync(int? branchId);
    Task<BillingResolution?> GetResolutionByIdAsync(Guid resolutionId);
}
