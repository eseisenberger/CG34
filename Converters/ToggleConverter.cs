namespace CG34.Converters;

public class ToggleConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length <= 1 || values[0] is not ShapeType selectedShape || values[1] is not ShapeType shape)
            return false;

        return selectedShape == shape;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}