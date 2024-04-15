using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using CG34.Classes;
using CG34.Extensions;

namespace CG34;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private WriteableBitmap _source;
    private WriteableBitmap _previousSource;
    private ShapeType? _selectedShapeTypeType;
    private Mode Mode = Mode.Select;

    public Shape? CurrentShape
    {
        get => _currentShape;
        set => SetField(ref _currentShape, value);
    }

    public ObservableCollection<Shape> Queue = [];
    public ObservableCollection<Point> CurrentShapePoints = [];

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

    public int DefaultWidth { get; set; } = 600;
    public int DefaultHeight { get; set; } = 400;
    private int Stride;
    private double _scale;
    private double Thickness = 1;
    private int _x;
    private int _y;
    private Shape? _currentShape;

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
        KeyDown += OnKey;

        InitializeComponent();
    }

    private async void OnMouse(object sender, MouseEventArgs e)
    {
        if (sender is not Image image)
            return;

        var pos = e.GetPosition(image);
        X = (int)(pos.X / Scale);
        Y = (int)(pos.Y / Scale);
        if (CurrentShape is not null)
        {
            Source = new WriteableBitmap(_previousSource);
            var pixels = Source.GetPixels();

            var shape = new Shape(CurrentShape)
            {
                End = new Point(X, Y)
            };
            pixels = CurrentShape.Type switch
            {
                ShapeType.Line => await DrawLine(shape, pixels),
                ShapeType.ThickLine => await DrawThickLine(shape, pixels, (int)Math.Round(Thickness, 0)),
                ShapeType.Circle => await DrawCircle(shape, pixels),
                _ => throw new ArgumentOutOfRangeException()
            };
            shape.Done = true;

            Source.WritePixels(pixels);
        }
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

    private async void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
                await Undo();
            else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                await Redo();
        }
    }

    private async Task Undo()
    {
        if (Queue.Count(s => s.Removed == false) == 0)
            return;

        Queue.Last(s => s.Removed == false).Removed = true;
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

        if (Keyboard.Modifiers == ModifierKeys.Alt)
        {
            Thickness = Math.Clamp(Thickness + e.Delta / (30 * 120.0), 1, 25);
        }
    }

    private void Open(object sender, RoutedEventArgs e)
    {
    }

    private void Save(object sender, RoutedEventArgs e)
    {
    }

    private void Reset(object sender, RoutedEventArgs e)
    {
        ClearCanvas();
        Queue.Clear();
    }


    private void ShapeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
            return;

        if (button.Tag is not ShapeType shape)
            return;

        if (SelectedShapeType != shape)
        {
            SelectedShapeType = shape;
            Mode = Mode.Selected;
        }
        else
        {
            SelectedShapeType = null;
            Mode = Mode.Select;
        }
    }

    private void AntialiasingToggle(object sender, RoutedEventArgs e)
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private async void ImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        var image = sender as Image;

        var position = e.GetPosition(image);
        position.X /= Scale;
        position.Y /= Scale;

        Console.WriteLine(position);
        switch (Mode)
        {
            case Mode.Select:
                //move
                break;
            case Mode.Selected:
                if (SelectedShapeType is null)
                    throw new Exception("Selected shape type is null in selected mode");
                CurrentShape = new Shape
                {
                    Start = position,
                    Type = SelectedShapeType.Value
                };
                Mode = Mode.Draw;
                break;
            case Mode.Draw:
                if (CurrentShape is null)
                    throw new Exception("Shape is null in draw mode");
                CurrentShape.End = position;
                Queue.Add(new Shape(CurrentShape));
                await DrawAll();
                CurrentShape = null;
                Mode = Mode.Selected;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        Queue = new ObservableCollection<Shape>(Queue.Where(s => s.Removed == false));
    }

    private async Task Redo()
    {
        if (Queue.Count(s => s.Removed) == 0)
            return;

        var shape = Queue.First(s => s.Removed);
        shape.Removed = false;
        await DrawAll();
    }

    private async Task DrawAll()
    {
        var pixels = _previousSource.GetPixels();

        foreach (var shape in Queue.Where(s => s.Done == false && s.Removed == false))
        {
            pixels = shape.Type switch
            {
                ShapeType.Line => await DrawLine(shape, pixels),
                ShapeType.ThickLine => await DrawThickLine(shape, pixels, (int)Math.Round(Thickness, 0)),
                ShapeType.Circle => await DrawCircle(shape, pixels),
                _ => throw new ArgumentOutOfRangeException()
            };
            shape.Done = true;
        }

        Source.WritePixels(pixels);
        _previousSource = Source;
    }

    private async Task<byte[]> DrawLine(Shape shape, byte[] pixels)
    {
        DrawLineAA(
            (int)shape.Start.X,
            (int)shape.Start.Y,
            (int)shape.End.X,
            (int)shape.End.Y,
            0,
            ref pixels
        );
        return pixels;
    }

    private void SymmetricLine(int x0, int y0, int x1, int y1, byte value, int channel, ref byte[] pixels,
        int thickness = 1)
    {
        x0 = Math.Clamp(x0, 0, DefaultWidth);
        x1 = Math.Clamp(x1, 0, DefaultWidth);

        y0 = Math.Clamp(y0, 0, DefaultHeight);
        y1 = Math.Clamp(y1, 0, DefaultHeight);

        var steep = Math.Abs(y1 - y0) > Math.Abs(x1 - x0);

        if (steep)
        {
            (x0, y0) = (y0, x0);
            (x1, y1) = (y1, x1);
        }

        if (x0 > x1)
        {
            (x0, x1) = (x1, x0);
            (y0, y1) = (y1, y0);
        }

        var dx = x1 - x0;
        var dy = Math.Abs(y1 - y0);

        var d = dx / 2;
        var yStep = (y0 < y1) ? 1 : -1;
        var y = y0;
        var yEnd = y1;

        var midPointX = x0 + (x1 - x0) / 2;

        for (int x = x0, xEnd = x1; x <= midPointX; x++, xEnd--)
        {
            for (var i = -(thickness - 1) / 2; i <= thickness / 2; i++)
            {
                if (steep)
                {
                    PutPixel(y + i, x, value, channel, ref pixels);
                    PutPixel(yEnd + i, xEnd, value, channel, ref pixels);
                }
                else
                {
                    PutPixel(x, y + i, value, channel, ref pixels);
                    PutPixel(xEnd, yEnd + i, value, channel, ref pixels);
                }
            }

            d -= dy;
            if (d >= 0)
                continue;

            y += yStep;
            yEnd -= yStep;
            d += dx;
        }
    }

    private async Task<byte[]> DrawThickLine(Shape shape, byte[] pixels, int n = 3)
    {
        await Parallel.ForAsync(0, 3,
            (channel, _) =>
            {
                SymmetricLine(
                    (int)shape.Start.X,
                    (int)shape.Start.Y,
                    (int)shape.End.X,
                    (int)shape.End.Y,
                    0,
                    channel,
                    ref pixels,
                    n
                );
                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    private async Task<byte[]> DrawCircle(Shape shape, byte[] pixels)
    {
        var radius =
            (int)Math.Sqrt(Math.Pow(shape.Start.X - shape.End.X, 2) + Math.Pow(shape.Start.Y - shape.End.Y, 2));

        await Parallel.ForAsync(0, 3,
            (channel, _) =>
            {
                MidpointCircle(
                    radius,
                    (int)shape.Start.X,
                    (int)shape.Start.Y,
                    0,
                    channel,
                    ref pixels
                );
                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    void MidpointCircle(int R, int cx, int cy, byte value, int channel, ref byte[] pixels)
    {
        var dE = 3;
        var dSE = 5 - 2 * R;
        var d = 1 - R;
        var x = 0;
        var y = R;

        PlotCirclePoints(cx, cy, x, y, value, channel, ref pixels);

        while (y >= x)
        {
            if (d < 0)
            {
                d += dE;
                dE += 2;
                dSE += 2;
            }
            else
            {
                d += dSE;
                dE += 2;
                dSE += 4;
                --y;
            }

            ++x;
            PlotCirclePoints(cx, cy, x, y, value, channel, ref pixels);
        }
    }

    private void PlotCirclePoints(int cx, int cy, int x, int y, byte value, int channel, ref byte[] pixels)
    {
        PutPixel(cx + x, cy - y, value, channel, ref pixels);
        PutPixel(cx + y, cy - x, value, channel, ref pixels);
        PutPixel(cx - y, cy - x, value, channel, ref pixels);
        PutPixel(cx - x, cy - y, value, channel, ref pixels);
        PutPixel(cx - x, cy + y, value, channel, ref pixels);
        PutPixel(cx - y, cy + x, value, channel, ref pixels);
        PutPixel(cx + y, cy + x, value, channel, ref pixels);
        PutPixel(cx + x, cy + y, value, channel, ref pixels);
    }


    private void PutPixel(int x, int y, byte value, int channel, ref byte[] pixels)
    {
        if (x < 0 || x >= DefaultWidth || y < 0 || y >= DefaultHeight)
            return;

        var index = y * Stride + x * 4 + channel;
        pixels[index] = value;
    }

    private byte GetPixel(int x, int y, int channel, byte[] pixels)
    {
        x = Math.Clamp(x, 0, DefaultWidth - 1);
        y = Math.Clamp(y, 0, DefaultHeight - 1);
        var index = y * Stride + x * 4 + channel;
        return pixels[index];
    }

    private void DrawLineAA(int x1, int y1, int x2, int y2, byte value, ref byte[] pixels,
        int thickness = 1)
    {
        x1 = Math.Clamp(x1, 0, DefaultWidth);
        x2 = Math.Clamp(x2, 0, DefaultWidth);

        y1 = Math.Clamp(y1, 0, DefaultHeight);
        y2 = Math.Clamp(y2, 0, DefaultHeight);

        var steep = Math.Abs(y2 - y1) > Math.Abs(x2 - x1);

        if (steep)
        {
            Console.WriteLine("Steep");
            (x1, y1) = (y1, x1);
            (x2, y2) = (y2, x2);
        }

        if (x1 > x2)
        {
            Console.WriteLine("Flip");
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var dx = x2 - x1;
        var dy = Math.Abs(y2 - y1);
        var yStep = (y1 < y2) ? 1 : -1;
        var d = 2 * (dy - dx);

        int incrE = 2 * dy;
        int incrNE = 2 * (dy - dx);

        var x = x1;
        var y = y1;

        var length2 = 2 * Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
        double D;
        if (d <= 0)
            D = (d + dx) / length2;
        else
            D = (d - dx) / length2;

        double Du = (2 * dx - (d - dx)) / length2;
        double Dl = (2 * dx + (d - dx)) / length2;
        //Console.WriteLine("\n\n-------------------------\n\n");

        Write(ref pixels);
        while (x < x2)
        {
            if (d <= 0)
            {
                x++;
                D = (d + dx) / length2;
                Du = (2 * dx - (d + dx)) / length2;
                Dl = (2 * dx + (d + dx)) / length2;
                d += incrE;
            }
            else
            {
                y += yStep;
                x++;
                D = (d - dx) / length2;
                Du = (2 * dx - (d - dx)) / length2;
                Dl = (2 * dx + (d - dx)) / length2;
                d += incrNE;
            }

            Write(ref pixels);
        }

        void Write(ref byte[] pixels)
        {
            if (steep)
            {
                IntensifyPixel(y + 1, x, Du, value, ref pixels);
                IntensifyPixel(y, x, D, value, ref pixels, true);
                IntensifyPixel(y - 1, x, Dl, value, ref pixels);
            }
            else
            {
                IntensifyPixel(x + 1, y, Dl, value, ref pixels);
                IntensifyPixel(x, y, D, value, ref pixels, true);
                IntensifyPixel(x - 1, y, Du, value, ref pixels);
            }
        }
    }

    void IntensifyPixel(int x, int y, double distance, byte value, ref byte[] pixels, bool main = false)
    {
        var intensity = GetIntensityFactor(x, y, distance);
        //if (main)
        //Console.WriteLine($"x:{x}, y:{y}, D:{distance:0.##}, int:{intensity:0.##}");
        for (var channel = 0; channel < 3; channel++)
        {
            var scaledIntensity = (byte)(intensity * value + (1 - intensity) * GetPixel(x, y, channel, pixels));
            PutPixel(x, y, scaledIntensity, channel, ref pixels);
        }
    }

    double GetIntensityFactor(int x, int y, double distance)
    {
        var maxIntensity = 1.0;
        var intensityFactor = maxIntensity - Math.Abs(distance);

        intensityFactor = Math.Clamp(intensityFactor, 0, maxIntensity);
        return intensityFactor;
    }
}