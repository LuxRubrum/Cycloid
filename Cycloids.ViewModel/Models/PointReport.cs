using Cycloids.Model.Calculations;
using Cycloids.Model.State;

namespace Cycloids.ViewModel.Models;

/// <summary>
/// Положение точки в момент времени <see cref="Time"/> вместе с проверками:
/// 1) мгновенный центр скоростей (МЦС) колеса лежит в точке касания с землёй C = (X центра, 0):
///    скорость точки перпендикулярна отрезку CP и равна |ω|·|CP|;
/// 2) скорость по соседним точкам траектории (центральная разность) сходится к скорости из Core
///    с порядком 2: при шаге h/2 погрешность падает примерно в 4 раза.
/// </summary>
public sealed record PointReport(
    double Time,
    PointState Point,
    double ContactX,
    double DistanceToContact,
    double SpeedFromIcs,
    double SpeedNumeric,
    double SpeedNumericHalfStep,
    double AngleToRadiusDegrees,
    double Step)
{
    public double IcsRelativeError => RelativeError(SpeedFromIcs, Point.Speed);

    public double NumericError => Math.Abs(SpeedNumeric - Point.Speed);

    public double NumericHalfStepError => Math.Abs(SpeedNumericHalfStep - Point.Speed);

    /// <summary>Отношение погрешностей при шагах h и h/2 (для метода 2-го порядка ожидается ≈ 4).</summary>
    public double ConvergenceRatio
        => NumericHalfStepError > 1e-14 ? NumericError / NumericHalfStepError : double.NaN;

    public static PointReport Create(ICycloidCalculator calculator, double time, double step)
    {
        ArgumentNullException.ThrowIfNull(calculator);

        PointState s = calculator.GetPointState(time);
        WheelState w = calculator.GetWheelState(time);

        // Вектор от точки касания C = (CenterX, 0) к точке P.
        double dx = s.X - w.CenterX;
        double dy = s.Y;
        double dist = double.Hypot(dx, dy);
        double omega = Math.Abs(calculator.Parameters.AngularVelocity);

        double angle = double.NaN;
        if (s.Speed > 1e-9 && dist > 1e-9)
        {
            double cos = (s.Vx * dx + s.Vy * dy) / (s.Speed * dist);
            angle = Math.Acos(Math.Clamp(cos, -1, 1)) * 180.0 / Math.PI;
        }

        return new PointReport(
            Time: time,
            Point: s,
            ContactX: w.CenterX,
            DistanceToContact: dist,
            SpeedFromIcs: omega * dist,
            SpeedNumeric: CentralSpeed(calculator, time, step),
            SpeedNumericHalfStep: CentralSpeed(calculator, time, step / 2),
            AngleToRadiusDegrees: angle,
            Step: step);
    }

    /// <summary>Скорость по двум соседним точкам траектории t − h и t + h.</summary>
    public static double CentralSpeed(ICycloidCalculator calculator, double t, double h)
    {
        PointState a = calculator.GetPointState(t - h);
        PointState b = calculator.GetPointState(t + h);
        return double.Hypot(b.X - a.X, b.Y - a.Y) / (2 * h);
    }

    private static double RelativeError(double value, double reference)
        => Math.Abs(reference) < 1e-9 ? Math.Abs(value - reference) : Math.Abs(value - reference) / Math.Abs(reference);
}
