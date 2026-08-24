using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Parking.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }
}
