using System;
using Microsoft.Extensions.DependencyInjection;
using Parking.Services.Contracts;
using Parking.ViewModels;

namespace Parking.Services.Implementations;

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ViewModelBase? _currentViewModel;

    public ViewModelBase? CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel != value)
            {
                _currentViewModel = value;
                CurrentViewModelChanged?.Invoke(this, _currentViewModel!);
            }
        }
    }

    public event EventHandler<ViewModelBase>? CurrentViewModelChanged;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        if (_serviceProvider.GetRequiredService(viewModelType) is ViewModelBase viewModel)
        {
            CurrentViewModel = viewModel;
            _ = viewModel.InitializeAsync();
        }
    }
}
