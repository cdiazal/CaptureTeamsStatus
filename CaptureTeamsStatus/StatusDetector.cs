using System.Drawing;

public sealed class StatusDetector
{
    private const int Tolerance = 0;
    private const int SampleTolerance = 14;

    private static readonly (int R, int G, int B) ExcludedPresenting = (167, 47, 60);
    private static readonly (int R, int G, int B) AvailableSample = (56, 173, 51);
    private static readonly (int R, int G, int B) AwaySample = (232, 168, 22);
    private static readonly (int R, int G, int B) BusySample = (193, 15, 30);
    private static readonly (int R, int G, int B) DoNotDisturbSample = (200, 40, 54);

    public string GetTeamsStatus(Color avg)
    {
        if (IsExcludedPresenting(avg))
            return TeamStatusColors.Unknown;

        if (IsNear(avg, AvailableSample))
            return TeamStatusColors.Available;
        if (IsNear(avg, AwaySample))
            return TeamStatusColors.Away;
        if (IsNear(avg, DoNotDisturbSample))
            return TeamStatusColors.DoNotDisturb;
        if (IsNear(avg, BusySample))
            return TeamStatusColors.Busy;

        if (avg.G > avg.R && avg.G > avg.B)
            return TeamStatusColors.Available;
        if (avg.R > 180 && avg.G > 120 && avg.B < 80)
            return TeamStatusColors.Away;
        if (avg.R > 170 && avg.B > 40)
            return TeamStatusColors.DoNotDisturb;
        if (avg.R > 150 && avg.G < 40 && avg.B < 50)
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

    private static bool IsNear(Color c, (int R, int G, int B) sample)
    {
        return
            Math.Abs(c.R - sample.R) <= SampleTolerance &&
            Math.Abs(c.G - sample.G) <= SampleTolerance &&
            Math.Abs(c.B - sample.B) <= SampleTolerance;
    }
}
