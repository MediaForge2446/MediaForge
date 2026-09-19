using System.Windows;
using System.Windows.Input;
using MediaForge.App.ViewModels;

namespace MediaForge.App.Views;

public partial class ExplorerPage : System.Windows.Controls.UserControl
{
    public ExplorerPage()
    {
        InitializeComponent();
    }

    private ExplorerViewModel? ViewModel => DataContext as ExplorerViewModel;

    private MainViewModel? MainModel => Window.GetWindow(this)?.DataContext as MainViewModel;

    private async void GoHomeButton_OnClick(object sender, RoutedEventArgs e)
    {
        MainModel?.NavigateHomeFromView();
        await Task.CompletedTask;
    }

    private async void GoBackButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.GoBackCommand.ExecuteAsync(null);
    }

    private async void GoForwardButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.GoForwardCommand.ExecuteAsync(null);
    }

    private async void GoUpButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            await ViewModel.GoUpCommand.ExecuteAsync(null);
    }

    private async void AddFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is null) return;
        var name = ShowTextInputDialog("הוסף תיקייה", "שם התיקייה", "צור תיקייה");
        if (!string.IsNullOrWhiteSpace(name))
            await ViewModel.CreateFolderWithNameAsync(name);
    }

    private async void RenameButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedEntry is null) return;
        var name = ShowTextInputDialog("שנה שם", "השם החדש", "שמור שם", ViewModel.SelectedEntry.Name);
        if (!string.IsNullOrWhiteSpace(name))
            await ViewModel.RenameSelectedWithNameAsync(name);
    }

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedEntry is null) return;

        var result = System.Windows.MessageBox.Show(
            $"למחוק את \"{ViewModel.SelectedEntry.Name}\"?\n\nהפריט יישאר מסומן בצהוב עד שתלחץ על \"שמור שינויים\".",
            "אישור מחיקה",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            await ViewModel.DeleteSelectedFromViewAsync();
    }

    private void AddMediaButton_OnClick(object sender, RoutedEventArgs e)
        => ViewModel?.RequestAddMediaToCurrentFolder();

    private async void FolderTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (ViewModel is null || e.NewValue is not ExplorerTreeNodeViewModel node)
            return;

        await ViewModel.NavigateToPathAsync(node.FullPath);
    }

    private async void Entry_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel is null || sender is not System.Windows.Controls.ListViewItem item || item.Content is not ExplorerEntryViewModel entry)
            return;

        await ViewModel.OpenFromDoubleClickAsync(entry);
    }

    private string? ShowTextInputDialog(string title, string label, string confirmText, string? initialValue = null)
    {
        var owner = Window.GetWindow(this);
        var dialog = new Window
        {
            Owner = owner,
            Width = 440,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Title = title
        };

        var outer = new System.Windows.Controls.Border
        {
            Background = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 229, 239)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(24),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.18
            }
        };

        var panel = new System.Windows.Controls.StackPanel { FlowDirection = System.Windows.FlowDirection.RightToLeft };
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = title,
            FontSize = 21,
            FontWeight = FontWeights.SemiBold,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(28, 39, 56))
        });
        panel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = label,
            Margin = new Thickness(0, 7, 0, 0),
            FontSize = 11,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(119, 132, 150))
        });

        var input = new System.Windows.Controls.TextBox
        {
            Text = initialValue ?? string.Empty,
            Margin = new Thickness(0, 14, 0, 0),
            MinHeight = 44,
            Padding = new Thickness(12, 0, 12, 0),
            VerticalContentAlignment = VerticalAlignment.Center,
            FontSize = 13,
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(214, 224, 235)),
            Background = System.Windows.Media.Brushes.White,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(31, 43, 60))
        };
        panel.Children.Add(input);

        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancel = new System.Windows.Controls.Button
        {
            Content = "ביטול",
            MinWidth = 90,
            MinHeight = 40,
            Margin = new Thickness(0, 0, 8, 0),
            Style = FindResource("MediaForgeSubtleButtonStyle") as Style
        };
        cancel.Click += (_, _) => dialog.DialogResult = false;

        var confirm = new System.Windows.Controls.Button
        {
            Content = confirmText,
            MinWidth = 110,
            MinHeight = 40,
            Style = FindResource("MediaForgePrimaryButtonStyle") as Style
        };
        confirm.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(input.Text))
                dialog.DialogResult = true;
        };

        buttons.Children.Add(cancel);
        buttons.Children.Add(confirm);
        panel.Children.Add(buttons);

        outer.Child = panel;
        dialog.Content = outer;
        dialog.Loaded += (_, _) =>
        {
            input.Focus();
            input.SelectAll();
        };

        return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }
}
