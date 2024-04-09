namespace CG34.Extensions;

public static class BitmapExtensions
{
    public static byte[] GetPixels(this BitmapSource source)
    {
        var height = source.PixelHeight;
        var stride = source.Stride();
        var pixels = new byte[height * stride];
        source.CopyPixels(
            pixels: pixels,
            stride: stride,
            offset: 0);
        return pixels;
    }

    public static int BytesPerPixel(this BitmapSource source) => source.Format.BitsPerPixel / 8;
    public static int Stride(this BitmapSource source) => source.BytesPerPixel() * source.PixelWidth;
    public static Int32Rect Rect(this BitmapSource source) => new(0, 0, source.PixelWidth, source.PixelHeight);

    public static void WritePixels(this WriteableBitmap source, byte[] pixels)
    {
        source.WritePixels(source.Rect(), pixels, source.Stride(), 0);
    }
}