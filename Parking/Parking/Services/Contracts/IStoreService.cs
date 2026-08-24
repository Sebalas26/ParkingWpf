using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IStoreService
{
    Task<IReadOnlyList<Store>> GetAllStoresAsync();
    Task<IReadOnlyList<Store>> GetActiveStoresAsync();
    Task<Store> CreateStoreAsync(string name, string taxId, string? phoneNumber);
    Task UpdateStoreAsync(Guid storeId, string name, string taxId, string? phoneNumber, bool isActive);
}
