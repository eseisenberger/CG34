using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CG34.Classes;
using CG34.Extensions;
using Microsoft.Win32;
using Newtonsoft.Json;

namespace CG34;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : INotifyPropertyChanged
{
    private WriteableBitmap _source = default!;
    private WriteableBitmap _previousSource;
    private ShapeType? _selectedShapeTypeType;
    private Mode Mode = Mode.Select;
    private const string SaveDirectory = @"./Saves";


    private Shape? SelectedShape
    {
        get => _selectedShape;
        set
        {
            if (value is null)
            {
                if (_selectedShape is null)
                    return;

                _selectedShape.Selected = false;
                _selectedShape = value;
            }
            else if (_selectedShape is null)
            {
                _selectedShape = value;
                _selectedShape.Selected = true;
            }
            else
            {
                _selectedShape.Selected = false;
                _selectedShape = value;
                _selectedShape.Selected = true;
            }

            _ = HandleShapeChanged();
        }
    }

    private Point SelectedPoint = default!;

    private async Task HandleShapeChanged()
    {
        await RedrawAll();
        if (_selectedShape is not null)
        {
            var pixels = Source.GetPixels();
            pixels = await DrawPoints(_selectedShape, pixels);
            Source.WritePixels(pixels);
        }
    }

    private async Task<byte[]> DrawPoints(Shape shape, byte[] pixels)
    {
        pixels = await DrawPoint(shape.Center, pixels, Colors.DarkOrange);

        for (var i = 0; i < shape.Vertices.Count; i++)
            pixels = await DrawPoint(shape.Vertices[i], pixels, Colors.DarkRed);

        var keys = shape.Midpoints.Keys.ToList();
        for (var i = 0; i < shape.Midpoints.Count; i++)
            pixels = await DrawPoint(keys[i], pixels, Colors.CadetBlue);

        return pixels;
    }

    public bool Antialiasing
    {
        get => _antialiasing;
        set
        {
            SetField(ref _antialiasing, value);
            _ = RedrawAll();
        }
    }

    public Color SelectedColor
    {
        get => _selectedColor;
        set => SetField(ref _selectedColor, value);
    }

    public Shape? CurrentShape
    {
        get => _currentShape;
        set => SetField(ref _currentShape, value);
    }

    public ObservableCollection<Shape> Queue { get; set; } = [];

    public int X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public int Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    public WriteableBitmap Source
    {
        get => _source;
        set => SetField(ref _source, value);
    }

    public int DefaultWidth { get; } = 600;
    public int DefaultHeight { get; } = 400;
    private readonly int Stride;
    private double _scale;
    private int _x;
    private int _y;
    private Shape? _currentShape;
    private Color _selectedColor = Colors.Black;
    private bool _antialiasing;
    private Shape? _selectedShape = null;

    public double Scale
    {
        get => _scale;
        set => SetField(ref _scale, value);
    }

    public ShapeType? SelectedShapeType
    {
        get => _selectedShapeTypeType;
        set => SetField(ref _selectedShapeTypeType, value);
    }

    public MainWindow()
    {
        ClearCanvas();
        Stride = Source.Stride();
        Scale = 1;
        _previousSource = Source;
        PreviewMouseWheel += OnMouseWheel;
        KeyDown += OnKeyPress;

        InitializeComponent();
    }

    private async void OnMouseMovement(object sender, MouseEventArgs e)
    {
        if (sender is not Image image)
            return;

        var pos = e.GetPosition(image);
        X = (int)(pos.X / Scale);
        Y = (int)(pos.Y / Scale);

        var pixels = _previousSource.GetPixels();


        if (SelectedShape is not null && SelectedPoint != default && Mode == Mode.Drag)
        {
            var point = new Point(X, Y);
            Point p;
            p = SelectedShape.Vertices.FirstOrDefault(v => v == SelectedPoint);
            if (p != default)
            {
                var i = SelectedShape.Vertices.IndexOf(p);
                SelectedShape.Vertices[i] = new Point(X, Y);
                if (SelectedShape.Type is ShapeType.Line or ShapeType.ThickLine)
                    SelectedShape.Center = SelectedShape.Vertices[0].Midpoint(SelectedShape.Vertices[1]);
                if (SelectedShape.Type is ShapeType.Polygon)
                {
                    SelectedShape.Center = SelectedShape.Vertices.Average();
                    var midpoints = new Dictionary<Point, (int, int)>(SelectedShape.Midpoints);
                    foreach (var midpoint in midpoints)
                    {
                        SelectedShape.Midpoints.Remove(midpoint.Key);
                        var (s, m) = midpoint.Value;
                        SelectedShape.Midpoints.Add(SelectedShape.Vertices[s].Midpoint(SelectedShape.Vertices[m]),
                            (s, m));
                    }
                }
            }

            p = SelectedShape.Midpoints.Keys.FirstOrDefault(v => v.DistanceTo(SelectedPoint) < 10);
            if (p != default)
            {
                var vec = point - p;
                var (start, end) = SelectedShape.Midpoints[p];
                var (x, y) = (SelectedShape.Vertices[start] + vec).GetCoordinates();
                SelectedShape.Vertices[start] = new Point(x, y);
                (x, y) = (SelectedShape.Vertices[end] + vec).GetCoordinates();
                SelectedShape.Vertices[end] = new Point(x, y);
                SelectedShape.Center = SelectedShape.Vertices.Average();
                var midpoints = new Dictionary<Point, (int, int)>(SelectedShape.Midpoints);
                foreach (var midpoint in midpoints)
                {
                    SelectedShape.Midpoints.Remove(midpoint.Key);
                    var (s, m) = midpoint.Value;
                    SelectedShape.Midpoints.Add(SelectedShape.Vertices[s].Midpoint(SelectedShape.Vertices[m]), (s, m));
                }
            }
            else if (SelectedPoint == SelectedShape.Center)
            {
                var vec = point - SelectedShape.Center;
                SelectedShape.Center = point;

                for (var i = 0; i < SelectedShape.Vertices.Count; i++)
                {
                    var (x, y) = (SelectedShape.Vertices[i] + vec).GetCoordinates();
                    SelectedShape.Vertices[i] = new Point(x, y);
                }

                var midpoints = new Dictionary<Point, (int, int)>(SelectedShape.Midpoints);
                foreach (var midpoint in midpoints)
                {
                    SelectedShape.Midpoints.Remove(midpoint.Key);
                    var (s, m) = midpoint.Value;
                    SelectedShape.Midpoints.Add(SelectedShape.Vertices[s].Midpoint(SelectedShape.Vertices[m]),
                        (s, m));
                }
            }


            SelectedPoint = point;

            pixels = await Draw(SelectedShape, pixels);
            pixels = await DrawPoints(SelectedShape, pixels);

            Source.WritePixels(pixels);
            return;
        }

        if (CurrentShape is null)
            return;

        Source = _previousSource.Clone();

        var shape = new Shape(CurrentShape);
        shape.Vertices.Add(new Point(X, Y));
        pixels = await Draw(shape, pixels);

        Source.WritePixels(pixels);
    }

    private async Task<byte[]> Draw(Shape shape, byte[] pixels)
    {
        return shape.Type switch
        {
            ShapeType.Line => await DrawLine(shape, pixels),
            ShapeType.ThickLine => await DrawThickLine(shape, pixels),
            ShapeType.Circle => await DrawCircle(shape, pixels),
            ShapeType.Select => pixels,
            ShapeType.Polygon => await DrawPolygon(shape, pixels),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void ClearCanvas()
    {
        Source = new WriteableBitmap(
            DefaultWidth,
            DefaultHeight,
            96,
            96,
            PixelFormats.Pbgra32,
            null
        );
        var pixels = Source.GetPixels();
        pixels = pixels.Select(_ => (byte)255).ToArray();
        Source.WritePixels(pixels);
        _previousSource = new WriteableBitmap(Source);
    }

    private async void OnKeyPress(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Z)
            return;

        switch (Keyboard.Modifiers)
        {
            case ModifierKeys.Control:
                await Undo();
                break;
            case ModifierKeys.Control | ModifierKeys.Shift:
                await Redo();
                break;
        }
    }

    private async Task Undo()
    {
        if (Queue.All(s => s.Removed))
            return;

        Queue.Last(s => s.Removed == false).Removed = true;

        await RedrawAll();
    }

    public async Task RedrawAll()
    {
        ClearCanvas();

        foreach (var shape in Queue)
        {
            shape.Done = false;
        }

        await DrawAll();
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            Scale = Math.Clamp(Scale + e.Delta / (10 * 120.0), 0.05, 25.0);
        }
    }

    private async void Open(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        Queue.Clear();

        var file = await File.ReadAllTextAsync(dialog.FileName);
        var queue = JsonConvert.DeserializeObject<ObservableCollection<Shape>>(file);
        if (queue is null)
            return;

        foreach (var shape in queue)
        {
            Queue.Add(shape);
        }

        await RedrawAll();
    }

    private void Save(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(SaveDirectory);

        SaveFileDialog dialog = new()
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"save_{DateTime.Now}",
            InitialDirectory = Path.GetFullPath(SaveDirectory),
            DefaultExt = ".json"
        };
        if (dialog.ShowDialog() == false)
            return;

        var content = JsonConvert.SerializeObject(Queue);
        using var sw = new StreamWriter(dialog.FileName);
        sw.Write(content);
        sw.Close();
    }

    private void Reset(object sender, RoutedEventArgs e)
    {
        ClearCanvas();
        Queue.Clear();
    }


    private void ToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
            return;

        if (button.Content is not ShapeType shape)
            return;


        SelectedShape = null;
        SelectedPoint = default;
        Mode = Mode.Select;

        if (SelectedShapeType != shape)
        {
            if (shape == ShapeType.Select && Queue.Any(s => !s.Removed))
                SelectedShape = Queue.First(s => !s.Removed);
            SelectedShapeType = shape;
            Mode = Mode.Selected;
        }
        else
        {
            SelectedShapeType = null;
            Mode = Mode.Select;
        }
    }


    private async void ImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        var image = sender as Image;

        var position = e.GetPosition(image);
        position.X = (int)(position.X / Scale);
        position.Y = (int)(position.Y / Scale);

        Console.WriteLine(position);
        switch (Mode)
        {
            case Mode.Select:
                //move
                break;
            case Mode.Selected:
                if (SelectedShapeType is null)
                    throw new Exception("Selected shape type is null in selected mode");
                if (SelectedShapeType != ShapeType.Select)
                {
                    if (SelectedShapeType == ShapeType.Circle)
                    {
                        CurrentShape = new Shape
                        {
                            Center = position,
                            Vertices = [],
                            Type = SelectedShapeType.Value,
                            Color = SelectedColor
                        };
                    }
                    else
                    {
                        CurrentShape = new Shape
                        {
                            Vertices = [position],
                            Type = SelectedShapeType.Value,
                            Color = SelectedColor
                        };
                    }

                    Mode = Mode.Draw;
                }

                if (SelectedShape is null)
                    return;

                if (position.DistanceTo(SelectedShape.Center) <= 10)
                    SelectedPoint = SelectedShape.Center;
                else
                {
                    var v = SelectedShape.Vertices.FirstOrDefault(v => v.DistanceTo(position) <= 10);
                    if (v != default)
                        SelectedPoint = v;
                    else
                    {
                        v = SelectedShape.Midpoints.Keys.FirstOrDefault(v => v.DistanceTo(position) <= 10);
                        if (v != default)
                            SelectedPoint = v;

                        else
                        {
                            SelectedPoint = default;
                            break;
                        }
                    }
                }

                SelectedShape.Removed = true;
                await RedrawAll();
                SelectedShape.Removed = false;

                Source = _previousSource.Clone();

                var pixels = Source.GetPixels();

                pixels = await Draw(SelectedShape, pixels);
                pixels = await DrawPoints(SelectedShape, pixels);

                Source.WritePixels(pixels);

                Mode = Mode.Drag;

                break;
            case Mode.Draw:
                if (CurrentShape is null)
                    throw new Exception("Shape is null in draw mode");
                CurrentShape.Vertices.Add(position);
                if (CurrentShape.Type is ShapeType.Line or ShapeType.ThickLine)
                    CurrentShape.Center = CurrentShape.Vertices[0].Midpoint(CurrentShape.Vertices[1]);
                if (CurrentShape.Type is ShapeType.Polygon)
                {
                    if (position.DistanceTo(CurrentShape.Vertices.First()) > 10)
                        break;
                    var v = CurrentShape.Vertices.Last();
                    CurrentShape.Vertices.Remove(v);
                    CurrentShape.Center = CurrentShape.Vertices.Average();
                    for (var i = 0; i < CurrentShape.Vertices.Count - 1; i++)
                    {
                        CurrentShape.Midpoints.Add(
                            CurrentShape.Vertices[i].Midpoint(CurrentShape.Vertices[i + 1]),
                            (i, i + 1));
                    }

                    CurrentShape.Midpoints.Add(
                        CurrentShape.Vertices.Last().Midpoint(CurrentShape.Vertices.First()),
                        (CurrentShape.Vertices.Count - 1, 0));
                }

                Queue.Add(new Shape(CurrentShape));
                await DrawAll();
                CurrentShape = null;
                Mode = Mode.Selected;
                _previousSource = Source.Clone();
                break;
            case Mode.Drag:
                SelectedPoint = default;
                Mode = Mode.Selected;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        var removed = Queue.Where(s => s.Removed);
        foreach (var r in removed)
            Queue.Remove(r);
    }

    private async Task Redo()
    {
        if (!Queue.Any(s => s.Removed))
            return;

        var shape = Queue.First(s => s.Removed);
        shape.Removed = false;
        await DrawAll();
    }

    private async Task DrawAll()
    {
        var pixels = _previousSource.GetPixels();

        foreach (var shape in Queue.Where(s => s is { Done: false, Removed: false }))
        {
            pixels = shape.Type switch
            {
                ShapeType.Line => await DrawLine(shape, pixels),
                ShapeType.ThickLine => await DrawThickLine(shape, pixels),
                ShapeType.Circle => await DrawCircle(shape, pixels),
                ShapeType.Polygon => await DrawPolygon(shape, pixels),
                _ => throw new ArgumentOutOfRangeException()
            };
            shape.Done = true;
        }

        Source.WritePixels(pixels);
        _previousSource = Source;
    }

    private void QueueItemMousedown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: Shape shape })
            return;

        Console.WriteLine(shape.Color);

        SelectedShape = SelectedShape == shape ? null : shape;
    }

    private async void HandleColorChanged(object? sender, EventArgs e)
    {
        await RedrawAll();
    }
}