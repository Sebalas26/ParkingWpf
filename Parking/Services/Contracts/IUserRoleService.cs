using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Parking.Entities;

namespace Parking.Services.Contracts;

public interface IUserRoleService
{
    Task<IReadOnlyList<User>> GetAllUsersAsync();
    Task<IReadOnlyList<Role>> GetAllRolesAsync();
    Task<User> CreateUserAsync(string username, string password, string fullName, string? email, Guid roleId);
    Task UpdateUserAsync(Guid userId, string fullName, string? email, Guid roleId, bool isActive);
    Task ResetPasswordAsync(Guid userId, string newPassword);
    Task<Role> CreateRoleAsync(string name, string? description);
    Task UpdateRoleAsync(Guid roleId, string name, string? description, bool isActive);
}
