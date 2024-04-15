using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace CG34.Classes;

public class Shape(Point start, Point end, ShapeType type, Color color, object? data = null) : INotifyPropertyChanged
{
    public Shape() : this(default!, default!, default!, default!)
    {
    }

    public Shape(Shape other) : this(other.Start, other.End, other.Type, other.Color, other.Data)
    {
    }

    public Point Start
    {
        get => _start;
        set => SetField(ref _start, value);
    }

    public Point End
    {
        get => _end;
        set => SetField(ref _end, value);
    }

    public ShapeType Type = type;
    public Color Color = color;
    public object? Data = data;

    [JsonIgnore] public bool Done = false;
    [JsonIgnore] public bool Removed = false;
    private Point _start = start;
    private Point _end = end;
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