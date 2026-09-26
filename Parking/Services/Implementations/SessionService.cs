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
    public int? CurrentCompanyId => CurrentUser?.CompanyId ?? CurrentBranch?.CompanyId;
    public IReadOnlyList<BranchModel> UserBranches => _userBranches.AsReadOnly();
    public bool HasMultipleBranches => _userBranches.Count > 1;
    public bool IsAuthenticated => CurrentUser != null;

    public void SetSession(UserSessionModel user, IEnumerable<BranchModel> branches, BranchModel? selectedBranch = null)
    {
        CurrentUser = user;
        _userBranches.Clear();
        _userBranches.AddRange(branches);

        // Resolver datos corporativos consolidados de la compañía
        var companyName = !string.IsNullOrWhiteSpace(CurrentUser?.CompanyName)
            ? CurrentUser.CompanyName
            : _userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.CompanyName))?.CompanyName;

        var companyNit = !string.IsNullOrWhiteSpace(CurrentUser?.CompanyNit)
            ? CurrentUser.CompanyNit
            : _userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.CompanyNit))?.CompanyNit;

        var companyEmail = !string.IsNullOrWhiteSpace(CurrentUser?.CompanyEmail)
            ? CurrentUser.CompanyEmail
            : _userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.CompanyEmail))?.CompanyEmail;

        var companyPhone = !string.IsNullOrWhiteSpace(CurrentUser?.CompanyPhone)
            ? CurrentUser.CompanyPhone
            : _userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.CompanyPhone))?.CompanyPhone;

        var companyLogo = !string.IsNullOrWhiteSpace(CurrentUser?.CompanyLogo)
            ? CurrentUser.CompanyLogo
            : (_userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.CompanyLogo))?.CompanyLogo
               ?? _userBranches.FirstOrDefault(b => !string.IsNullOrWhiteSpace(b.LogoBase64))?.LogoBase64);

        if (CurrentUser != null)
        {
            if (string.IsNullOrWhiteSpace(CurrentUser.CompanyName) && !string.IsNullOrWhiteSpace(companyName))
                CurrentUser.CompanyName = companyName;
            if (string.IsNullOrWhiteSpace(CurrentUser.CompanyNit) && !string.IsNullOrWhiteSpace(companyNit))
                CurrentUser.CompanyNit = companyNit;
            if (string.IsNullOrWhiteSpace(CurrentUser.CompanyEmail) && !string.IsNullOrWhiteSpace(companyEmail))
                CurrentUser.CompanyEmail = companyEmail;
            if (string.IsNullOrWhiteSpace(CurrentUser.CompanyPhone) && !string.IsNullOrWhiteSpace(companyPhone))
                CurrentUser.CompanyPhone = companyPhone;
            if (string.IsNullOrWhiteSpace(CurrentUser.CompanyLogo) && !string.IsNullOrWhiteSpace(companyLogo))
                CurrentUser.CompanyLogo = companyLogo;
        }

        // Propagar a todas las sedes de la colección
        foreach (var b in _userBranches)
        {
            if (string.IsNullOrWhiteSpace(b.CompanyName) && !string.IsNullOrWhiteSpace(companyName))
                b.CompanyName = companyName;
            if (string.IsNullOrWhiteSpace(b.CompanyNit) && !string.IsNullOrWhiteSpace(companyNit))
                b.CompanyNit = companyNit;
            if (string.IsNullOrWhiteSpace(b.CompanyEmail) && !string.IsNullOrWhiteSpace(companyEmail))
                b.CompanyEmail = companyEmail;
            if (string.IsNullOrWhiteSpace(b.CompanyPhone) && !string.IsNullOrWhiteSpace(companyPhone))
                b.CompanyPhone = companyPhone;
            if (string.IsNullOrWhiteSpace(b.CompanyLogo) && !string.IsNullOrWhiteSpace(companyLogo))
                b.CompanyLogo = companyLogo;
        }

        if (selectedBranch != null)
        {
            CurrentBranch = selectedBranch;
        }
        else if (CurrentBranch != null && _userBranches.Any(b => b.Id == CurrentBranch.Id))
        {
            CurrentBranch = _userBranches.First(b => b.Id == CurrentBranch.Id);
        }
        else
        {
            CurrentBranch = _userBranches.FirstOrDefault(b => b.IsDefault) ?? _userBranches.FirstOrDefault();
        }

        if (CurrentBranch != null)
        {
            if (string.IsNullOrWhiteSpace(CurrentBranch.CompanyName) && !string.IsNullOrWhiteSpace(companyName))
                CurrentBranch.CompanyName = companyName;
            if (string.IsNullOrWhiteSpace(CurrentBranch.CompanyNit) && !string.IsNullOrWhiteSpace(companyNit))
                CurrentBranch.CompanyNit = companyNit;
            if (string.IsNullOrWhiteSpace(CurrentBranch.CompanyEmail) && !string.IsNullOrWhiteSpace(companyEmail))
                CurrentBranch.CompanyEmail = companyEmail;
            if (string.IsNullOrWhiteSpace(CurrentBranch.CompanyPhone) && !string.IsNullOrWhiteSpace(companyPhone))
                CurrentBranch.CompanyPhone = companyPhone;
            if (string.IsNullOrWhiteSpace(CurrentBranch.CompanyLogo) && !string.IsNullOrWhiteSpace(companyLogo))
                CurrentBranch.CompanyLogo = companyLogo;
        }

        UserSessionChanged?.Invoke(CurrentUser);
        ActiveBranchChanged?.Invoke(CurrentBranch);
    }

    public void SetActiveBranch(BranchModel branch)
    {
        if (CurrentUser != null)
        {
            if (string.IsNullOrWhiteSpace(branch.CompanyName) && !string.IsNullOrWhiteSpace(CurrentUser.CompanyName))
                branch.CompanyName = CurrentUser.CompanyName;
            if (string.IsNullOrWhiteSpace(branch.CompanyNit) && !string.IsNullOrWhiteSpace(CurrentUser.CompanyNit))
                branch.CompanyNit = CurrentUser.CompanyNit;
            if (string.IsNullOrWhiteSpace(branch.CompanyEmail) && !string.IsNullOrWhiteSpace(CurrentUser.CompanyEmail))
                branch.CompanyEmail = CurrentUser.CompanyEmail;
            if (string.IsNullOrWhiteSpace(branch.CompanyPhone) && !string.IsNullOrWhiteSpace(CurrentUser.CompanyPhone))
                branch.CompanyPhone = CurrentUser.CompanyPhone;
            if (string.IsNullOrWhiteSpace(branch.CompanyLogo) && !string.IsNullOrWhiteSpace(CurrentUser.CompanyLogo))
                branch.CompanyLogo = CurrentUser.CompanyLogo;
        }

        CurrentBranch = branch;
        ActiveBranchChanged?.Invoke(CurrentBranch);
    }

    public void UpdateCurrentBranch(Action<BranchModel> updateAction)
    {
        if (CurrentBranch != null)
        {
            updateAction(CurrentBranch);
            ActiveBranchChanged?.Invoke(CurrentBranch);
        }
    }

    public void UpdateCurrentUser(Action<UserSessionModel> updateAction)
    {
        if (CurrentUser != null)
        {
            updateAction(CurrentUser);
            UserSessionChanged?.Invoke(CurrentUser);
        }
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
