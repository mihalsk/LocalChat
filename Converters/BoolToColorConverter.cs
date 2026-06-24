using System.Globalization;
using Microsoft.Maui.Controls;

namespace LocalChat.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSentByMe && isSentByMe)
            return Color.FromArgb("#2B5278");// s.LightGreen;
        return Color.FromArgb("#182533");//Colors.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}