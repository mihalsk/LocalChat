using System.Globalization;
using Microsoft.Maui.Controls;

namespace LanChat.Converters;

public class BoolToAlignmentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSentByMe && isSentByMe)
            return LayoutOptions.End;
        return LayoutOptions.Start;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}