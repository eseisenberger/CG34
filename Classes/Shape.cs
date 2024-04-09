namespace CG34.Classes;

public class Shape(Point start, Point end, ShapeType type, Color color, object? data = null)
{
    public Shape() : this(default!, default!, default!, default!)
    {
    }

    public Shape(Shape other) : this(other.Start, other.End, other.Type, other.Color, other.Data)
    {
    }

    public Point Start = start;
    public Point End = end;
    public ShapeType Type = type;
    public Color Color = color;
    public object? Data = data;
}