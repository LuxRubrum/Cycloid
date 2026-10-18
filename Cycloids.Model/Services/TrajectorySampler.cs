using Cycloids.Model.Calculations;
using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.Model.Services;

public sealed class TrajectorySampler : ITrajectorySampler
{
    public IReadOnlyList<PointState> Sample(CycloidParameters parameters, double tStart, double tEnd, int steps)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        if (steps < 1)
            throw new ArgumentOutOfRangeException(nameof(steps), "Требуется как минимум один шаг.");
        if (!double.IsFinite(tStart) || !double.IsFinite(tEnd) || tEnd <= tStart)
            throw new ArgumentException("Временной интервал должен быть конечным, при этом tEnd > tStart ");

        var calculator = new CycloidCalculator(parameters);
        var points = new PointState[steps + 1];
        double dt = (tEnd - tStart) / steps;

        for (int i = 0; i <= steps; i++)
            points[i] = calculator.GetPointState(i == steps ? tEnd : tStart + i * dt);

        return points;
    }

    public IReadOnlyList<PointState> SamplePeriods(CycloidParameters parameters, double periods, int stepsPerPeriod)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.EnsureValid();
        if (!double.IsFinite(periods) || periods <= 0)
            throw new ArgumentOutOfRangeException(nameof(periods), "Значение количества периодов должно быть положительным ");
        if (stepsPerPeriod < 1)
            throw new ArgumentOutOfRangeException(nameof(stepsPerPeriod));

        int steps = Math.Max(1, (int)Math.Ceiling(periods * stepsPerPeriod));
        return Sample(parameters, 0, periods * parameters.Period, steps);
    }

    /// <summary> Длина полилинии, построенной по сгенерированным точкам (используется для проверок и тестирования) </summary>
    public static double PolylineLength(IReadOnlyList<PointState> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        double length = 0;
        for (int i = 1; i < points.Count; i++)
            length += double.Hypot(points[i].X - points[i - 1].X, points[i].Y - points[i - 1].Y);
        return length;
    }
}
