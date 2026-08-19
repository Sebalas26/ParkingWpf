using System;
using Parking.ViewModels;

namespace Parking.Services.Contracts;

public interface INavigationService
{
    ViewModelBase? CurrentViewModel { get; }
    event EventHandler<ViewModelBase>? CurrentViewModelChanged;
    void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
    void NavigateTo(Type viewModelType);
}
