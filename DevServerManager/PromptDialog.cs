using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace DevServerManager;
public static class PromptDialog
{
    public static string? Show(string title, string message, string initialValue)
    {
        var input = new TextBox { Text = initialValue, MinWidth = 320, Margin = new Thickness(0, 8, 0, 12) };
        var window = new Window { Title = title, Width = 420, Height = 175, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }); panel.Children.Add(input);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var ok = new Button { Content = "Apply", IsDefault = true, MinWidth = 75, Margin = new Thickness(0, 0, 8, 0) }; ok.Click += (_, _) => window.DialogResult = true;
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75 }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); window.Content = panel;
        return window.ShowDialog() == true ? input.Text : null;
    }
}
