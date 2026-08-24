using System;

namespace Parking.Core.Helpers;

public class NavigationMessage
{
    public Type TargetViewModelType { get; }
    public object? Parameter { get; }

    public NavigationMessage(Type targetViewModelType, object? parameter = null)
    {
        TargetViewModelType = targetViewModelType;
        Parameter = parameter;
    }
}
