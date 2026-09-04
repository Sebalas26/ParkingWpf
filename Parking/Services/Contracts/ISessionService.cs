using System;
using System.Collections.Generic;
using Parking.Models;

namespace Parking.Services.Contracts;

public interface ISessionService
{
    event Action<BranchModel?>? ActiveBranchChanged;
    event Action<UserSessionModel?>? UserSessionChanged;

    UserSessionModel? CurrentUser { get; }
    BranchModel? CurrentBranch { get; }
    int? CurrentBranchId { get; }
    int? CurrentCompanyId { get; }
    IReadOnlyList<BranchModel> UserBranches { get; }
    bool HasMultipleBranches { get; }
    bool IsAuthenticated { get; }

    void SetSession(UserSessionModel user, IEnumerable<BranchModel> branches, BranchModel? selectedBranch = null);
    void SetActiveBranch(BranchModel branch);
    void UpdateCurrentBranch(Action<BranchModel> updateAction);
    void Clear();
}
