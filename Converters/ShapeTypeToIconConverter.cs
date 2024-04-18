using Material.Icons;
using Material.Icons.WPF;

namespace CG34.Converters;

public class ShapeTypeToIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ShapeType shape)
            return MaterialIconKind.QuestionMark;
        return shape switch
        {
            ShapeType.Line => MaterialIconKind.Minus,
            ShapeType.ThickLine => MaterialIconKind.MinusThick,
            ShapeType.Circle => MaterialIconKind.CircleOutline,
            ShapeType.Select => MaterialIconKind.CursorDefault,
            ShapeType.Polygon => MaterialIconKind.VectorPolyline,
            ShapeType.Bezier => MaterialIconKind.VectorCurve,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}