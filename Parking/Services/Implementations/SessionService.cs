using System;
using System.Collections.Generic;
using System.Linq;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.Services.Implementations;

public class SessionService : ISessionService
{
    private readonly List<BranchModel> _userBranches = new();

    public event Action<BranchModel?>? ActiveBranchChanged;
    public event Action<UserSessionModel?>? UserSessionChanged;

    public UserSessionModel? CurrentUser { get; private set; }
    public BranchModel? CurrentBranch { get; private set; }
    public int? CurrentBranchId => CurrentBranch?.Id;
    public IReadOnlyList<BranchModel> UserBranches => _userBranches.AsReadOnly();
    public bool HasMultipleBranches => _userBranches.Count > 1;
    public bool IsAuthenticated => CurrentUser != null;

    public void SetSession(UserSessionModel user, IEnumerable<BranchModel> branches, BranchModel? selectedBranch = null)
    {
        CurrentUser = user;
        _userBranches.Clear();
        _userBranches.AddRange(branches);

        if (selectedBranch != null)
        {
            CurrentBranch = selectedBranch;
        }
        else
        {
            CurrentBranch = _userBranches.FirstOrDefault(b => b.IsDefault) ?? _userBranches.FirstOrDefault();
        }

        UserSessionChanged?.Invoke(CurrentUser);
        ActiveBranchChanged?.Invoke(CurrentBranch);
    }

    public void SetActiveBranch(BranchModel branch)
    {
        CurrentBranch = branch;
        ActiveBranchChanged?.Invoke(CurrentBranch);
    }

    public void Clear()
    {
        CurrentUser = null;
        CurrentBranch = null;
        _userBranches.Clear();
        UserSessionChanged?.Invoke(null);
        ActiveBranchChanged?.Invoke(null);
    }
}
