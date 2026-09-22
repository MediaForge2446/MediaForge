using System.Windows;
using MediaForge.App.Localization;

namespace MediaForge.App.Views;

public partial class TermsWindow : Window
{
    public TermsWindow()
    {
        InitializeComponent();
        LocalizationService.Instance.ApplyToWindow(this);
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AgreementCheckBox.IsChecked.GetValueOrDefault())
            return;

        DialogResult = true;
    }
}
