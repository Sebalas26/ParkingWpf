using System.Threading.Tasks;
using System.Windows;
using Parking.Core.Enums;

namespace Parking.Views;

public static class ModernMessageDialog
{
    public static void ShowAlert(Window? owner, string title, string message, DialogNotificationType type = DialogNotificationType.Information)
    {
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, ToImage(type));
    }

    public static bool ShowConfirmation(Window? owner, string title, string message, DialogNotificationType type = DialogNotificationType.Question, string confirmText = "Confirmar", string cancelText = "Cancelar")
    {
        var result = MessageBox.Show(
            owner,
            message,
            title,
            MessageBoxButton.YesNo,
            ToImage(type),
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    private static MessageBoxImage ToImage(DialogNotificationType type)
    {
        return type switch
        {
            DialogNotificationType.Success => MessageBoxImage.Information,
            DialogNotificationType.Warning => MessageBoxImage.Warning,
            DialogNotificationType.Error => MessageBoxImage.Error,
            DialogNotificationType.Question => MessageBoxImage.Question,
            _ => MessageBoxImage.Information
        };
    }
}
