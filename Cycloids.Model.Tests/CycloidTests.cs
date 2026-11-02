using Cycloids.Model.Calculations;
using Cycloids.Model.Enums;
using Cycloids.Model.Parameters;
using Cycloids.Model.Services;

namespace Cycloids.Model.Tests;

public class CycloidCalculatorTests
{
    private static CycloidCalculator Rim(double r = 2, double w = 3) => new(new CycloidParameters(r, w, r));

    [Fact]
    public void Rim_AtTimeZero_PointIsAtOrigin()
    {
        var s = Rim().GetPointState(0);
        Assert.Equal(0, s.X, 9);
        Assert.Equal(0, s.Y, 9);
        Assert.Equal(0, s.Speed, 9);
    }

    [Fact]
    public void Rim_AfterOneRevolution_XEquals2PiR()
    {
        var calc = Rim(r: 2, w: 3);
        var s = calc.GetPointState(calc.Parameters.Period);
        Assert.Equal(2 * Math.PI * 2, s.X, 9);
        Assert.Equal(0, s.Y, 9);
        Assert.Equal(0, s.Speed, 9); // cusp
    }

    [Fact]
    public void Rim_AtTopOfArch_HeightIs2R_AndSpeedIs2RW()
    {
        var calc = Rim(r: 2, w: 3);
        var s = calc.GetPointState(calc.Parameters.Period / 2);
        Assert.Equal(4, s.Y, 9);
        Assert.Equal(2 * 2 * 3, s.Speed, 9);
        Assert.Equal(0, s.Vy, 9);
    }

    [Fact]
    public void AccelerationMagnitude_IsConstant_W2D()
    {
        var calc = new CycloidCalculator(new CycloidParameters(1.5, 4, 1.0, 0.7));
        foreach (var t in new[] { 0.0, 0.13, 0.5, 1.1, 2.9 })
            Assert.Equal(16 * 1.0, calc.GetPointState(t).AccelerationMagnitude, 9);
    }

    [Fact]
    public void WheelCenter_MovesWithoutSlipping()
    {
        var calc = Rim(r: 2, w: 3);
        var w = calc.GetWheelState(1.5);
        Assert.Equal(2 * 3 * 1.5, w.CenterX, 9);
        Assert.Equal(2, w.CenterY, 9);
        Assert.Equal(3 * 1.5, w.RotationAngle, 9);
    }

    [Fact]
    public void VelocityMatchesNumericalDerivativeOfPosition()
    {
        var calc = new CycloidCalculator(new CycloidParameters(1.2, -2.5, 2.0, 0.4));
        const double t = 0.8, h = 1e-6;
        var a = calc.GetPointState(t - h);
        var b = calc.GetPointState(t + h);
        var s = calc.GetPointState(t);
        Assert.Equal((b.X - a.X) / (2 * h), s.Vx, 6);
        Assert.Equal((b.Y - a.Y) / (2 * h), s.Vy, 6);
    }

    [Theory]
    [InlineData(1.0, 0.5, CycloidType.Curtate)]
    [InlineData(1.0, 1.0, CycloidType.Common)]
    [InlineData(1.0, 1.5, CycloidType.Prolate)]
    public void Kind_IsDerivedFromOffset(double r, double d, CycloidType expected)
        => Assert.Equal(expected, new CycloidParameters(r, 1, d).Kind);

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, -0.1)]
    [InlineData(double.NaN, 1, 1)]
    public void InvalidParameters_Throw(double r, double w, double d)
    {
        var p = new CycloidParameters(r, w, d);
        Assert.False(p.IsValid);
        Assert.Throws<ArgumentException>(() => new CycloidCalculator(p));
    }
}

public class CycloidAnalyticsAndSamplerTests
{
    private static readonly CycloidParameters Rim = new(2, 3, 2);

    [Fact]
    public void Sampler_ReturnsStepsPlusOnePoints_WithExactEndpoints()
    {
        var pts = new TrajectorySampler().Sample(Rim, 0, 1, 100);
        Assert.Equal(101, pts.Count);
        Assert.Equal(0, pts[0].Time, 12);
        Assert.Equal(1, pts[^1].Time, 12);
    }

    [Fact]
    public void ArcLength_Numeric_MatchesEightR()
    {
        var pts = new TrajectorySampler().SamplePeriods(Rim, 1, 20_000);
        Assert.Equal(8 * Rim.Radius, TrajectorySampler.PolylineLength(pts), 3);
    }

    [Fact]
    public void ArcLength_Analytic_Common_IsEightR()
        => Assert.Equal(16, new CycloidAnalytics(Rim).ArcLengthPerPeriod, 12);

    [Fact]
    public void ArcLength_Analytic_ForCenterPoint_IsStraightLine()
    {
        // d = 0: the point is the wheel center, it travels 2πR per revolution.
        var a = new CycloidAnalytics(new CycloidParameters(2, 3, 0));
        Assert.Equal(2 * Math.PI * 2, a.ArcLengthPerPeriod, 6);
    }

    [Fact]
    public void ArcLength_Analytic_Curtate_MatchesPolyline()
    {
        var p = new CycloidParameters(1, 2, 0.6);
        var numeric = TrajectorySampler.PolylineLength(new TrajectorySampler().SamplePeriods(p, 1, 20_000));
        Assert.Equal(numeric, new CycloidAnalytics(p).ArcLengthPerPeriod, 3);
    }

    [Theory]
    [InlineData(2, 3, 2)]    // common
    [InlineData(1, 2, 0.6)]  // curtate
    [InlineData(1, 2, 1.7)]  // prolate
    public void Area_Analytic_MatchesNumericIntegral(double r, double w, double d)
    {
        var p = new CycloidParameters(r, w, d);
        var pts = new TrajectorySampler().SamplePeriods(p, 1, 20_000);
        double area = 0;
        for (int i = 1; i < pts.Count; i++)
            area += 0.5 * (pts[i].Y + pts[i - 1].Y) * (pts[i].X - pts[i - 1].X);

        Assert.Equal(new CycloidAnalytics(p).AreaPerPeriod, area, 3);
    }

    [Fact]
    public void Common_Area_Is3PiRSquared()
        => Assert.Equal(3 * Math.PI * 4, new CycloidAnalytics(Rim).AreaPerPeriod, 9);

    [Fact]
    public void Heights_AndSpeeds()
    {
        var a = new CycloidAnalytics(new CycloidParameters(1, 2, 0.5));
        Assert.Equal(1.5, a.MaxHeight, 12);
        Assert.Equal(0.5, a.MinHeight, 12);
        Assert.Equal(1.0, a.MinSpeed, 12);
        Assert.Equal(3.0, a.MaxSpeed, 12);
    }

    [Fact]
    public void RadiusOfCurvature_Common_IsFourRAtTop_AndZeroAtCusp()
    {
        var a = new CycloidAnalytics(Rim);
        Assert.Equal(4 * Rim.Radius, a.RadiusOfCurvature(Rim.Period / 2), 9);
        Assert.Equal(0, a.RadiusOfCurvature(0), 9);
    }

    [Fact]
    public void RadiusOfCurvature_General_MatchesNumericFormula()
    {
        var p = new CycloidParameters(1, 2, 0.6, 0.3);
        var calc = new CycloidCalculator(p);
        const double t = 0.4;
        var s = calc.GetPointState(t);
        double expected = Math.Pow(s.Speed, 3) / Math.Abs(s.Vx * s.Ay - s.Vy * s.Ax);
        Assert.Equal(expected, new CycloidAnalytics(p).RadiusOfCurvature(t), 9);
    }

    [Fact]
    public void CuspTimes_Common_AreOncePerPeriod()
    {
        var p = new CycloidParameters(1, 2, 1);
        var cusps = new CycloidAnalytics(p).GetCuspTimes(0, 2 * p.Period);
        Assert.Equal(3, cusps.Count);
        Assert.Equal(p.Period, cusps[1], 9);
        var calc = new CycloidCalculator(p);
        foreach (var t in cusps)
        {
            Assert.Equal(0, calc.GetPointState(t).Y, 9);
            Assert.Equal(0, calc.GetPointState(t).Speed, 9);
        }
    }

    [Fact]
    public void CuspTimes_NonCommon_AreEmpty()
        => Assert.Empty(new CycloidAnalytics(new CycloidParameters(1, 2, 0.5)).GetCuspTimes(0, 10));
}
