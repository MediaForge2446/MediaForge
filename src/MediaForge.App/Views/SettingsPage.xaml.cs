using System.Windows;

namespace MediaForge.App.Views;

public partial class SettingsPage : System.Windows.Controls.UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private void OpenTerms_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var termsWindow = new TermsWindow
        {
            Owner = owner,
            ShowInTaskbar = false
        };

        termsWindow.ShowDialog();
    }
}
