using ModernWpf;
using ModernWpf.Markup;

namespace CG34.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool selected)
            return new SolidColorBrush(Colors.Red);

        return selected ? new SolidColorBrush(Colors.Orange) : new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}