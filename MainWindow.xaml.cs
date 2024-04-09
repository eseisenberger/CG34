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
    private ShapeType? _selectedShapeTypeType;
    private Mode Mode = Mode.Select;
    private Shape? CurrentShape;
    public ObservableCollection<Shape> Queue = [];

    public WriteableBitmap Source
    {
        get => _source;
        set => SetField(ref _source, value);
    }

    private const int DefaultWidth = 600;
    private const int DefaultHeight = 400;
    private int Stride;
    private double _scale;
    private double Thickness = 1;

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
        _source = new WriteableBitmap(
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

        Stride = Source.Stride();
        Scale = 1;

        PreviewMouseWheel += OnMouseWheel;

        InitializeComponent();
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
                Queue.Insert(0, new Shape(CurrentShape));
                await DrawLatest();
                CurrentShape = null;
                Mode = Mode.Selected;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task DrawLatest()
    {
        var latest = Queue.FirstOrDefault();
        if (latest == default)
            return;

        var pixels = Source.GetPixels();

        pixels = latest.Type switch
        {
            ShapeType.Line => await DrawLine(latest, pixels),
            ShapeType.ThickLine => await DrawThickLine(latest, pixels, (int)Math.Round(Thickness, 0)),
            ShapeType.Circle => await DrawCircle(latest, pixels),
            _ => throw new ArgumentOutOfRangeException()
        };

        Source.WritePixels(pixels);
    }

    private async Task<byte[]> DrawLine(Shape shape, byte[] pixels)
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
                    ref pixels
                );
                return ValueTask.CompletedTask;
            });
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
            // Plot the symmetrical points for the current (x, y)
            PlotCirclePoints(cx, cy, x, y, value, channel, ref pixels);
        }
    }

    private void PlotCirclePoints(int cx, int cy, int x, int y, byte value, int channel, ref byte[] pixels)
    {
        // Octant 1
        PutPixel(cx + x, cy - y, value, channel, ref pixels);
        // Octant 2
        PutPixel(cx + y, cy - x, value, channel, ref pixels);
        // Octant 3
        PutPixel(cx - y, cy - x, value, channel, ref pixels);
        // Octant 4
        PutPixel(cx - x, cy - y, value, channel, ref pixels);
        // Octant 5
        PutPixel(cx - x, cy + y, value, channel, ref pixels);
        // Octant 6
        PutPixel(cx - y, cy + x, value, channel, ref pixels);
        // Octant 7
        PutPixel(cx + y, cy + x, value, channel, ref pixels);
        // Octant 8
        PutPixel(cx + x, cy + y, value, channel, ref pixels);
    }


    private void PutPixel(int x, int y, byte value, int channel, ref byte[] pixels)
    {
        x = Math.Clamp(x, 0, DefaultWidth - 1);
        y = Math.Clamp(y, 0, DefaultHeight - 1);
        var index = y * Stride + x * 4 + channel;
        pixels[index] = value;
    }
}