using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class BillingResolutionService : IBillingResolutionService
{
    private readonly IDbConnectionManager _connectionManager;

    public BillingResolutionService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<IReadOnlyList<BillingResolution>> GetAllResolutionsAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.BillingResolutions
            .OrderBy(r => r.Prefix)
            .ThenBy(r => r.ResolutionNumber)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<BillingResolution>> GetActiveResolutionsByBranchAsync(int? branchId)
    {
        using var db = _connectionManager.CreateDbContext();
        var query = db.BillingResolutions.Where(r => r.IsActive);
        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == null || r.BranchId == branchId.Value);
        }

        return await query
            .OrderBy(r => r.DocumentType)
            .ThenBy(r => r.Prefix)
            .ToListAsync();
    }

    public async Task<BillingResolution?> GetResolutionByIdAsync(Guid resolutionId)
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.BillingResolutions.FirstOrDefaultAsync(r => r.ResolutionId == resolutionId);
    }
}
