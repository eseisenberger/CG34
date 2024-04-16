using CG34.Classes;
using CG34.Extensions;

namespace CG34;

public partial class MainWindow
{
    private async Task<byte[]> DrawLine(Shape shape, byte[] pixels)
    {
        if (shape.Vertices.Count < 2)
            return pixels;

        var values = shape.Values;
        var start = shape.Vertices[0];
        var end = shape.Vertices[1];
        await Parallel.ForAsync(0, 4,
            (channel, _) =>
            {
                if (Antialiasing)
                    AntialiasedLine(
                        start,
                        end,
                        values[channel],
                        channel,
                        ref pixels
                    );
                else
                    SymmetricLine(
                        start,
                        end,
                        values[channel],
                        channel,
                        ref pixels
                    );
                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    private async Task<byte[]> DrawPolygon(Shape shape, byte[] pixels)
    {
        if (shape.Vertices.Count < 2)
            return pixels;

        var values = shape.Values;
        await Parallel.ForAsync(0, 4,
            (channel, _) =>
            {
                for (var i = 0; i < shape.Vertices.Count - 1; i++)
                {
                    if (Antialiasing)
                        AntialiasedLine(
                            shape.Vertices[i],
                            shape.Vertices[i + 1],
                            values[channel],
                            channel,
                            ref pixels
                        );
                    else
                        SymmetricLine(
                            shape.Vertices[i],
                            shape.Vertices[i + 1],
                            values[channel],
                            channel,
                            ref pixels
                        );
                }

                return ValueTask.CompletedTask;
            });
        await Parallel.ForAsync(0, 4,
            (channel, _) =>
            {
                if (Antialiasing)
                    AntialiasedLine(
                        shape.Vertices.Last(),
                        shape.Vertices.First(),
                        values[channel],
                        channel,
                        ref pixels
                    );
                else
                    SymmetricLine(
                        shape.Vertices.Last(),
                        shape.Vertices.First(),
                        values[channel],
                        channel,
                        ref pixels
                    );

                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    private async Task<byte[]> DrawThickLine(Shape shape, byte[] pixels)
    {
        if (shape.Vertices.Count < 2)
            return pixels;

        var values = shape.Values;
        var start = shape.Vertices[0];
        var end = shape.Vertices[1];
        var thickness = shape.Thickness;
        await Parallel.ForAsync(0, 4,
            (channel, _) =>
            {
                SymmetricLine(
                    start,
                    end,
                    values[channel],
                    channel,
                    ref pixels,
                    thickness
                );
                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    private async Task<byte[]> DrawCircle(Shape shape, byte[] pixels)
    {
        var radius =
            (int)Math.Sqrt(Math.Pow(shape.Center.X - shape.Vertices.First().X, 2) +
                           Math.Pow(shape.Center.Y - shape.Vertices.First().Y, 2));
        var values = shape.Values;
        await Parallel.ForAsync(0, 4,
            (channel, _) =>
            {
                MidpointCircle(
                    radius,
                    (int)shape.Center.X,
                    (int)shape.Center.Y,
                    values[channel],
                    channel,
                    ref pixels
                );
                return ValueTask.CompletedTask;
            });
        return pixels;
    }

    private async Task<byte[]> DrawPoint(Point point, byte[] pixels, Color color)
    {
        var (x, y) = point.GetCoordinates();
        return await DrawPoint(x, y, pixels, color);
    }

    private async Task<byte[]> DrawPoint(int x, int y, byte[] pixels, Color color)
    {
        byte[] values = [color.B, color.G, color.R, color.A];
        for (var i = 0; i < 7; i++)
        {
            await Parallel.ForAsync(0, 4,
                (channel, _) =>
                {
                    MidpointCircle(
                        i,
                        x,
                        y,
                        values[channel],
                        channel,
                        ref pixels
                    );
                    return ValueTask.CompletedTask;
                });
        }

        return pixels;
    }

    private void SymmetricLine(Point start, Point end, byte value, int channel, ref byte[] pixels,
        int thickness = 1)
    {
        var (x1, y1) = start.GetCoordinates(DefaultWidth, DefaultHeight);
        var (x2, y2) = end.GetCoordinates(DefaultWidth, DefaultHeight);

        var steep = Math.Abs(y2 - y1) > Math.Abs(x2 - x1);

        if (steep)
        {
            (x1, y1) = (y1, x1);
            (x2, y2) = (y2, x2);
        }

        if (x1 > x2)
        {
            (x1, x2) = (x2, x1);
            (y1, y2) = (y2, y1);
        }

        var dx = x2 - x1;
        var dy = Math.Abs(y2 - y1);

        var d = dx / 2;
        var yStep = y1 < y2 ? 1 : -1;
        var y = y1;
        var yEnd = y2;

        var midPointX = x1 + (x2 - x1) / 2;

        for (int x = x1, xEnd = x2; x <= midPointX; x++, xEnd--)
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

    private void MidpointCircle(int R, int cx, int cy, byte value, int channel, ref byte[] pixels)
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

    private void AntialiasedLine(Point start, Point end, byte value, int channel, ref byte[] pixels)
    {
        var (x1, y1) = start.GetCoordinates(DefaultWidth, DefaultHeight);
        var (x2, y2) = end.GetCoordinates(DefaultWidth, DefaultHeight);

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

        var incrE = 2 * dy;
        var incrNE = 2 * (dy - dx);

        var x = x1;
        var y = y1;

        var length2 = 2 * Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));
        var D = (d <= 0) ? (d + dx) / length2 : (d - dx) / length2;

        var Du = (2 * dx - (d - dx)) / length2;
        var Dl = (2 * dx + (d - dx)) / length2;

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
                x++;
                y += yStep;
                D = (d - dx) / length2;
                Du = (2 * dx - (d - dx)) / length2;
                Dl = (2 * dx + (d - dx)) / length2;
                d += incrNE;
            }

            Write(ref pixels);
        }

        return;

        bool IsVertical()
        {
            return dx == 0;
        }

        void Write(ref byte[] pixels)
        {
            var mirrorY = y1 > y2;

            if (IsVertical())
            {
                IntensifyPixel(x, y, 1.0, value, ref pixels, channel, true);
            }
            else if (steep)
            {
                if (mirrorY)
                {
                    IntensifyPixel(y - 1, x, Du, value, ref pixels, channel);
                    IntensifyPixel(y, x, D, value, ref pixels, channel);
                    IntensifyPixel(y + 1, x, Dl, value, ref pixels, channel);
                }
                else
                {
                    IntensifyPixel(y + 1, x, Du, value, ref pixels, channel);
                    IntensifyPixel(y, x, D, value, ref pixels, channel);
                    IntensifyPixel(y - 1, x, Dl, value, ref pixels, channel);
                }
            }
            else
            {
                if (mirrorY)
                {
                    IntensifyPixel(x, y - 1, Du, value, ref pixels, channel);
                    IntensifyPixel(x, y, D, value, ref pixels, channel);
                    IntensifyPixel(x, y + 1, Dl, value, ref pixels, channel);
                }
                else
                {
                    IntensifyPixel(x + 1, y, Dl, value, ref pixels, channel);
                    IntensifyPixel(x, y, D, value, ref pixels, channel);
                    IntensifyPixel(x - 1, y, Du, value, ref pixels, channel);
                }
            }
        }
    }

    private void IntensifyPixel(int x, int y, double distance, byte value, ref byte[] pixels, int channel,
        bool isVertical = false)
    {
        var intensity = 1.0;
        if (!isVertical)
            intensity = GetIntensityFactor(distance);

        var scaledIntensity = (byte)(intensity * value + (1 - intensity) * GetPixel(x, y, channel, pixels));
        PutPixel(x, y, scaledIntensity, channel, ref pixels);
    }

    private static double GetIntensityFactor(double distance)
    {
        const double MaxIntensity = 1.0;
        var intensityFactor = MaxIntensity - Math.Abs(distance);

        intensityFactor = Math.Clamp(intensityFactor, 0, MaxIntensity);
        return intensityFactor;
    }
}