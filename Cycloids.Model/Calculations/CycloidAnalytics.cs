using Cycloids.Model.Enums;
using Cycloids.Model.Parameters;

namespace Cycloids.Model.Calculations;

public sealed class CycloidAnalytics : ICycloidAnalytics
{
    private const int SimpsonIntervals = 20000; // максимальное конечное число
    private const double Eps = 1e-9;

    private readonly CycloidParameters _p;

    public CycloidAnalytics(CycloidParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.EnsureValid();
        _p = parameters;
    }

    public double Period => _p.Period;

    public double DistancePerPeriod => _p.DistancePerPeriod;

    public double MaxHeight => _p.Radius + _p.PointOffset;

    public double MinHeight => _p.Radius - _p.PointOffset;

    public double MinSpeed => Math.Abs(_p.AngularVelocity) * Math.Abs(_p.Radius - _p.PointOffset);

    public double MaxSpeed => Math.Abs(_p.AngularVelocity) * (_p.Radius + _p.PointOffset);

    public double AreaPerPeriod => Math.PI * (2 * _p.Radius * _p.Radius + _p.PointOffset * _p.PointOffset);

    /// <summary>
    /// |v| = |ω|*f(α), где f(α) = sqrt(R^2 + d^2 − 2Rd*cos α) и dt = dα/|ω|,
    /// поэтому длина представляет собой интеграл от f по полному обороту α. 
    /// При d = R интеграл вычисляется элементарно (8R); в противном случае он является эллиптическим и вычисляется по правилу Симпсона.
    /// </summary>
    public double ArcLengthPerPeriod
    {
        get
        {
            if (_p.Kind == CycloidType.Common)
                return 8 * _p.Radius;

            double r = _p.Radius, d = _p.PointOffset;
            double h = 2 * Math.PI / SimpsonIntervals;
            double sum = F(0) + F(2 * Math.PI);

            for (int i = 1; i < SimpsonIntervals; i++)
                sum += (i % 2 == 0 ? 2 : 4) * F(i * h);

            return sum * h / 3;

            double F(double a) => Math.Sqrt(Math.Max(0, r * r + d * d - 2 * r * d * Math.Cos(a)));
        }
    }

    public double RadiusOfCurvature(double t)
    {
        double r = _p.Radius, d = _p.PointOffset;
        double a = _p.InitialPhase + _p.AngularVelocity * t;

        if (_p.Kind == CycloidType.Common)
            return 4 * r * Math.Abs(Math.Sin(a / 2)); // В точках может возникнуть неопределенность вида 0 делённое на 0, поэтому используется замкнутая форма.

        double f = Math.Sqrt(Math.Max(0, r * r + d * d - 2 * r * d * Math.Cos(a)));
        double denom = d * Math.Abs(r * Math.Cos(a) - d);

        return denom < Eps ? double.PositiveInfinity : f * f * f / denom;
    }

    public IReadOnlyList<double> GetCuspTimes(double tStart, double tEnd)
    {
        if (_p.Kind != CycloidType.Common || !(tEnd >= tStart))
            return Array.Empty<double>();

        double w = _p.AngularVelocity, phi = _p.InitialPhase;
        double a0 = phi + w * tStart, a1 = phi + w * tEnd;
        double lo = Math.Min(a0, a1), hi = Math.Max(a0, a1);
        double twoPi = 2 * Math.PI;

        long kMin = (long)Math.Ceiling(lo / twoPi - Eps);
        long kMax = (long)Math.Floor(hi / twoPi + Eps);

        var times = new List<double>();
        for (long k = kMin; k <= kMax; k++)
            times.Add((twoPi * k - phi) / w);

        times.Sort();

        return times;
    }
}
