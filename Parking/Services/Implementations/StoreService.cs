using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Parking.Data.Factories;
using Parking.Entities;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class StoreService : IStoreService
{
    private readonly IDbConnectionManager _connectionManager;

    public StoreService(IDbConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task<IReadOnlyList<Store>> GetAllStoresAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.Stores
            .Include(s => s.Agreements)
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Store>> GetActiveStoresAsync()
    {
        using var db = _connectionManager.CreateDbContext();
        return await db.Stores
            .Where(s => s.IsActive)
            .Include(s => s.Agreements.Where(a => a.IsActive))
            .OrderBy(s => s.Name)
            .ToListAsync();
    }

    public async Task<Store> CreateStoreAsync(string name, string taxId, string? phoneNumber)
    {
        using var db = _connectionManager.CreateDbContext();
        var trimmedTaxId = taxId.Trim();

        if (await db.Stores.AnyAsync(s => s.TaxId.ToLower() == trimmedTaxId.ToLower()))
        {
            throw new InvalidOperationException($"Ya existe un almacén con el NIT/Identificación '{taxId}'.");
        }

        var store = new Store
        {
            StoreId = Guid.NewGuid(),
            Name = name.Trim(),
            TaxId = trimmedTaxId,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Stores.Add(store);
        await db.SaveChangesAsync();
        return store;
    }

    public async Task UpdateStoreAsync(Guid storeId, string name, string taxId, string? phoneNumber, bool isActive)
    {
        using var db = _connectionManager.CreateDbContext();
        var store = await db.Stores.FindAsync(storeId);
        if (store == null)
        {
            throw new KeyNotFoundException("Almacén no encontrado.");
        }

        store.Name = name.Trim();
        store.TaxId = taxId.Trim();
        store.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        store.IsActive = isActive;

        await db.SaveChangesAsync();
    }
}
