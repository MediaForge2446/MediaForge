using MediaForge.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MediaForge;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    public MainWindow(){InitializeComponent();SystemBackdrop=new MicaBackdrop{Kind=MicaKind.BaseAlt};DataContext=ViewModel;ContentFrame.Navigate(typeof(LibraryPage));Nav.SelectedItem=Nav.MenuItems[0];Closed+=(_,_)=>ViewModel.Dispose();}
    public void NavigateToExplorer(){Nav.SelectedItem=Nav.MenuItems.OfType<NavigationViewItem>().First(x=>string.Equals(x.Tag?.ToString(),"Explorer",StringComparison.Ordinal));}
    private void ClearError(object sender,RoutedEventArgs e)=>ViewModel.ClearError();
    private void OnSelectionChanged(NavigationView sender,NavigationViewSelectionChangedEventArgs e)
    {
        try{var page=e.IsSettingsSelected?typeof(SettingsPage):e.SelectedItemContainer?.Tag?.ToString()=="Explorer"?typeof(ExplorerPage):typeof(LibraryPage);ContentFrame.Navigate(page);}catch(Exception ex){ViewModel.ReportError(ex);}
    }
}
