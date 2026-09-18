using System;
using System.Threading.Tasks;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface IAppUpdateService
{
    Task<AppReleaseInfoDto?> CheckForUpdateAsync();
    Task<bool> PrepareAndApplyUpdateAsync(AppReleaseInfoDto release, IProgress<UpdateProgressReport>? progress = null);
}
