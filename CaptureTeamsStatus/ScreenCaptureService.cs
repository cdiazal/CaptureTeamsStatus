using Serilog;
using System.Drawing;
using System.Drawing.Imaging;

public sealed class ScreenCaptureService
{
    private readonly Rectangle _captureRect;
    private readonly Rectangle _circleRelative;
    private readonly bool _debug;

    public ScreenCaptureService(Rectangle captureRect, Rectangle circleRelative, bool debugImages)
    {
        _captureRect = captureRect;
        _circleRelative = circleRelative;
        _debug = debugImages;
    }

    public Color CaptureAverageColor()
    {
        using var bmp = new Bitmap(_captureRect.Width, _captureRect.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(_captureRect.Left, _captureRect.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
        }

        var circleArea = new Rectangle(_circleRelative.X, _circleRelative.Y, _circleRelative.Width, _circleRelative.Height);

        if (_debug)
        {
            var fileName = $"teams_icon_sample_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png";
            bmp.Save(fileName, ImageFormat.Png);
            Log.Information("Saved debug image {FileName}", fileName);
        }

        return GetAverageColorUnsafe(bmp, circleArea);
    }

    private static Color GetAverageColorUnsafe(Bitmap bmp, Rectangle rect)
    {
        BitmapData? data = null;
        try
        {
            data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            long r = 0, g = 0, b = 0;
            int pixelCount = rect.Width * rect.Height;

            unsafe
            {
                byte* scan0 = (byte*)data.Scan0;
                int stride = data.Stride;

                for (int y = 0; y < rect.Height; y++)
                {
                    byte* row = scan0 + (y * stride);
                    for (int x = 0; x < rect.Width; x++)
                    {
                        int offset = x * 4;
                        b += row[offset + 0];
                        g += row[offset + 1];
                        r += row[offset + 2];
                    }
                }
            }

            return Color.FromArgb(
                (int)(r / pixelCount),
                (int)(g / pixelCount),
                (int)(b / pixelCount));
        }
        finally
        {
            if (data != null)
                bmp.UnlockBits(data);
        }
    }
}