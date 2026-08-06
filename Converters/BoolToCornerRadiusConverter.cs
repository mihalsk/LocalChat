using System.Globalization;
using Microsoft.Maui.Controls;

namespace LocalChat.Converters;

public class BoolToCornerRadiusConverter : IValueConverter
{
    public object Convert(object value, Type targetType,
        object parameter, CultureInfo culture)
    {
        bool mine = (bool)value;

        return mine
            ? new CornerRadius(8, 8, 8, 0)
            : new CornerRadius(8, 8, 0, 8);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}