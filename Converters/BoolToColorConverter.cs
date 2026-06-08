using System.Globalization;
using Microsoft.Maui.Controls;

namespace LocalChat.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSentByMe && isSentByMe)
            return Colors.LightGreen;
        return Colors.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}