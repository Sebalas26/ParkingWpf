using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(Guid userId, string moduleKey, string actionKey);
    Task<IReadOnlyList<AppModule>> GetAccessibleModulesAsync(Guid userId);
    Task<IReadOnlyList<PermissionMatrixItem>> GetRolePermissionsMatrixAsync(Guid roleId);
    Task SaveRolePermissionsAsync(Guid roleId, IEnumerable<Guid> grantedPermissionIds);
}
