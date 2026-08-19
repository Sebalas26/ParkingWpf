using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parking.Entities;
using Parking.Models;
using Parking.Services.Contracts;

namespace Parking.ViewModels;

public partial class SecuritySettingsViewModel : ViewModelBase
{
    private readonly IUserRoleService _userRoleService;
    private readonly IPermissionService _permissionService;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string? _email;

    [ObservableProperty]
    private Role? _selectedRole;

    [ObservableProperty]
    private bool _isUserActive = true;

    [ObservableProperty]
    private User? _selectedUser;

    [ObservableProperty]
    private bool _isEditingUser;

    [ObservableProperty]
    private string _newRoleName = string.Empty;

    [ObservableProperty]
    private string? _newRoleDescription;

    [ObservableProperty]
    private Role? _selectedRoleForMatrix;

    [ObservableProperty]
    private string? _feedbackMessage;

    [ObservableProperty]
    private bool _hasFeedback;

    [ObservableProperty]
    private bool _isSuccessFeedback;

    public ObservableCollection<User> Users { get; } = new();
    public ObservableCollection<Role> Roles { get; } = new();
    public ObservableCollection<PermissionMatrixItem> PermissionsMatrix { get; } = new();

    public SecuritySettingsViewModel(
        IUserRoleService userRoleService,
        IPermissionService permissionService,
        IDialogService dialogService)
    {
        _userRoleService = userRoleService;
        _permissionService = permissionService;
        _dialogService = dialogService;
    }

    public override async Task InitializeAsync()
    {
        await LoadRolesAsync();
        await LoadUsersAsync();

        if (Roles.Count > 0 && SelectedRoleForMatrix == null)
        {
            SelectedRoleForMatrix = Roles[0];
        }
    }

    private async Task LoadRolesAsync()
    {
        var roles = await _userRoleService.GetAllRolesAsync();
        Roles.Clear();
        foreach (var r in roles)
        {
            Roles.Add(r);
        }

        if (Roles.Count > 0 && SelectedRole == null)
        {
            SelectedRole = Roles[0];
        }
    }

    private async Task LoadUsersAsync()
    {
        var users = await _userRoleService.GetAllUsersAsync();
        Users.Clear();
        foreach (var u in users)
        {
            Users.Add(u);
        }
    }

    async partial void OnSelectedRoleForMatrixChanged(Role? value)
    {
        if (value != null)
        {
            await LoadMatrixForRoleAsync(value.RoleId);
        }
        else
        {
            PermissionsMatrix.Clear();
        }
    }

    private async Task LoadMatrixForRoleAsync(Guid roleId)
    {
        var matrix = await _permissionService.GetRolePermissionsMatrixAsync(roleId);
        PermissionsMatrix.Clear();
        foreach (var item in matrix)
        {
            PermissionsMatrix.Add(item);
        }
    }

    partial void OnSelectedUserChanged(User? value)
    {
        if (value != null)
        {
            IsEditingUser = true;
            Username = value.Username;
            FullName = value.FullName;
            Email = value.Email;
            IsUserActive = value.IsActive;
            SelectedRole = Roles.FirstOrDefault(r => r.RoleId == value.RoleId);
            Password = string.Empty;
        }
        else
        {
            ClearUserForm();
        }
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(FullName))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "El nombre completo es obligatorio.";
            return;
        }

        if (SelectedRole == null)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Debe asignar un rol al usuario.";
            return;
        }

        if (!IsEditingUser && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "Usuario y contraseña son obligatorios para nuevos usuarios.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Guardando información de usuario...";

        try
        {
            if (IsEditingUser && SelectedUser != null)
            {
                await _userRoleService.UpdateUserAsync(SelectedUser.UserId, FullName, Email, SelectedRole.RoleId, IsUserActive);

                if (!string.IsNullOrWhiteSpace(Password))
                {
                    await _userRoleService.ResetPasswordAsync(SelectedUser.UserId, Password);
                }

                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Usuario '{SelectedUser.Username}' actualizado exitosamente.";
            }
            else
            {
                var newUser = await _userRoleService.CreateUserAsync(Username, Password, FullName, Email, SelectedRole.RoleId);
                HasFeedback = true;
                IsSuccessFeedback = true;
                FeedbackMessage = $"Usuario '{newUser.Username}' creado exitosamente.";
            }

            ClearUserForm();
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al guardar usuario: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void ClearUserForm()
    {
        SelectedUser = null;
        IsEditingUser = false;
        Username = string.Empty;
        Password = string.Empty;
        FullName = string.Empty;
        Email = null;
        IsUserActive = true;
    }

    [RelayCommand]
    private async Task CreateRoleAsync()
    {
        HasFeedback = false;
        FeedbackMessage = null;

        if (string.IsNullOrWhiteSpace(NewRoleName))
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = "El nombre del rol es obligatorio.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Creando rol...";

        try
        {
            var role = await _userRoleService.CreateRoleAsync(NewRoleName, NewRoleDescription);
            NewRoleName = string.Empty;
            NewRoleDescription = null;

            await LoadRolesAsync();
            SelectedRoleForMatrix = Roles.FirstOrDefault(r => r.RoleId == role.RoleId);

            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = $"Rol '{role.Name}' creado exitosamente.";
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al crear rol: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task SaveRolePermissionsAsync()
    {
        if (SelectedRoleForMatrix == null)
        {
            return;
        }

        IsBusy = true;
        BusyMessage = "Guardando matriz de permisos del rol...";

        try
        {
            var grantedIds = PermissionsMatrix
                .Where(p => p.IsGranted)
                .Select(p => p.PermissionId)
                .ToList();

            await _permissionService.SaveRolePermissionsAsync(SelectedRoleForMatrix.RoleId, grantedIds);

            HasFeedback = true;
            IsSuccessFeedback = true;
            FeedbackMessage = $"Permisos actualizados para el rol '{SelectedRoleForMatrix.Name}'.";

            await _dialogService.ShowAlertAsync("Permisos Guardados", $"La matriz de permisos para '{SelectedRoleForMatrix.Name}' ha sido actualizada.");
        }
        catch (Exception ex)
        {
            HasFeedback = true;
            IsSuccessFeedback = false;
            FeedbackMessage = $"Error al guardar permisos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }
}
