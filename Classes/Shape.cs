using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CG34.Classes;

public class Shape(
    Point center,
    IEnumerable<Point> vertices,
    Dictionary<Point, (int, int)> midpoints,
    ShapeType type,
    Color color,
    int thickness = 1)
    : INotifyPropertyChanged
{
    public Shape() : this(default!, [], [], default!, default!)
    {
    }

    public Shape(Shape other) : this(other.Center, other.Vertices, other.Midpoints, other.Type, other.Color,
        other.Thickness)
    {
    }

    public byte[] Values =>
    [
        Color.B,
        Color.G,
        Color.R,
        Color.A
    ];

    public ShapeType Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    public Color Color
    {
        get => _color;
        set
        {
            if (SetField(ref _color, value)) OnPropertyChanged(nameof(Values));
        }
    }

    public int Thickness
    {
        get => _thickness;
        set => SetField(ref _thickness, value);
    }

    [JsonIgnore] public bool Done = false;
    [JsonIgnore] public bool Removed = false;

    [JsonIgnore]
    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }

    public ObservableCollection<Point> Vertices { get; set; } = new(vertices);
    public Dictionary<Point, (int, int)> Midpoints { get; set; } = midpoints;

    public Point Center
    {
        get => _center;
        set => SetField(ref _center, value);
    }

    private ShapeType _type = type;
    private Color _color = color;
    private int _thickness = thickness;
    [JsonIgnore] private bool _selected = false;
    private Point _center = center;
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}