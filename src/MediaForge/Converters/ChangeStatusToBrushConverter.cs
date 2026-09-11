using MediaForge.ViewModels;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
namespace MediaForge.Converters;
public sealed class ChangeStatusToBrushConverter:IValueConverter
{
    private static readonly SolidColorBrush Green=new(Microsoft.UI.Colors.MediumSeaGreen);
    private static readonly SolidColorBrush Yellow=new(Microsoft.UI.Colors.Goldenrod);
    private static readonly SolidColorBrush Red=new(Microsoft.UI.Colors.IndianRed);
    public object Convert(object value,Type targetType,object parameter,string language)=>value is ChangeStatus s?s switch{ChangeStatus.Synced=>Green,ChangeStatus.Pending=>Yellow,ChangeStatus.Failed=>Red,_=>Yellow}:Yellow;
    public object ConvertBack(object value,Type targetType,object parameter,string language)=>throw new NotSupportedException();
}
