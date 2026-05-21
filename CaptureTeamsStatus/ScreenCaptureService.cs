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
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var iconFileName = $"teams_icon_sample_{timestamp}.png";
            bmp.Save(iconFileName, ImageFormat.Png);
            Log.Information("Saved debug image {FileName}", iconFileName);

            using var markedBmp = new Bitmap(bmp);
            using (var graphics = Graphics.FromImage(markedBmp))
            using (var pen = new Pen(Color.Lime, 1))
            {
                graphics.DrawRectangle(pen, circleArea);
            }

            var markedFileName = $"teams_icon_marked_sample_{timestamp}.png";
            markedBmp.Save(markedFileName, ImageFormat.Png);
            Log.Information("Saved debug marked image {FileName}", markedFileName);

            using var circleBmp = bmp.Clone(circleArea, bmp.PixelFormat);
            using var enlargedCircleBmp = new Bitmap(circleBmp.Width * 8, circleBmp.Height * 8);
            using (var graphics = Graphics.FromImage(enlargedCircleBmp))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(circleBmp, new Rectangle(0, 0, enlargedCircleBmp.Width, enlargedCircleBmp.Height));
            }

            var circleFileName = $"teams_circle_sample_{timestamp}.png";
            enlargedCircleBmp.Save(circleFileName, ImageFormat.Png);
            Log.Information("Saved debug circle image {FileName}", circleFileName);
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
