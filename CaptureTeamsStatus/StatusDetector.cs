using System.Drawing;

public sealed class StatusDetector
{
    private const int Tolerance = 0;
    
    private static readonly (int R, int G, int B) ExcludedPresenting = (167, 47, 60);

    public string GetTeamsStatus(Color avg)
    {
        if (IsExcludedPresenting(avg))
            return TeamStatusColors.Unknown;
        
        if (avg is { R: < 100, G: > 90, B: < 100 })
            return TeamStatusColors.Available;
        if (avg is { R: > 100, G: > 80, B: < 60 })
            return TeamStatusColors.Away;
        if (avg is { R: > 90, G: < 60, B: > 40 } && avg.B < avg.R - 30)
            return TeamStatusColors.DoNotDisturb;
        if (avg is { R: > 100, G: < 60, B: < 50 })
            return TeamStatusColors.Busy;

        return TeamStatusColors.Unknown;
    }

    private static bool IsExcludedPresenting(Color c)
    {
        if (Tolerance == 0)
            return c.R == ExcludedPresenting.R && c.G == ExcludedPresenting.G && c.B == ExcludedPresenting.B;

        return
            Math.Abs(c.R - ExcludedPresenting.R) <= Tolerance &&
            Math.Abs(c.G - ExcludedPresenting.G) <= Tolerance &&
            Math.Abs(c.B - ExcludedPresenting.B) <= Tolerance;
    }
}