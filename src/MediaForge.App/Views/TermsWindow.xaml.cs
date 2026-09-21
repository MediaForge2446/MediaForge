using System.Windows;

namespace MediaForge.App.Views;

public partial class TermsWindow : Window
{
    public TermsWindow()
    {
        InitializeComponent();
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AgreementCheckBox.IsChecked.GetValueOrDefault())
            return;

        DialogResult = true;
    }
}
