namespace CG34.Extensions;

public static class PointExtensions
{
    public static (int x, int y) GetCoordinates(this Point point)
    {
        return ((int)point.X, (int)point.Y);
    }

    public static (int x, int y) GetCoordinates(this Point point, int maxX, int maxY)
    {
        var coordinates = point.GetCoordinates();

        coordinates.x = Math.Clamp(coordinates.x, 0, maxX);
        coordinates.y = Math.Clamp(coordinates.y, 0, maxY);

        return coordinates;
    }

    public static double DistanceTo(this Point a, Point b)
    {
        return Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
    }

    public static Point Add(this Point a, Point b)
    {
        return new Point(a.X + b.X, a.Y + b.Y);
    }

    public static Point Midpoint(this Point a, Point b)
    {
        return new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
    }

    public static Point Average(this IEnumerable<Point> enumerable)
    {
        var array = enumerable as Point[] ?? enumerable.ToArray();
        return new Point(array.Average(p => p.X), array.Average(p => p.Y));
    }
}