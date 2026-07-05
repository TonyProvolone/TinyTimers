using System.Windows;
using System.Windows.Media;

namespace TinyTimers;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string message, string confirmText = "OK", bool isDestructive = false, bool showCancel = true)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;
        CancelButton.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;

        if (isDestructive)
        {
            ConfirmButton.Background = (System.Windows.Media.Brush)FindResource("DangerBackgroundBrush");
            ConfirmButton.Foreground = (System.Windows.Media.Brush)FindResource("DangerForegroundBrush");
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
