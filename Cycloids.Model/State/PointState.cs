namespace Cycloids.Model.State;

/// <summary>
/// Кинематика отслеживаемой точки в момент времени <see cref="Time"/>
/// Координаты X — вправо, Y — вверх (при отрисовке View должен перевернуть Y координату).
/// </summary>
public readonly record struct PointState(
    double Time,
    double X,
    double Y,
    double Vx,
    double Vy,
    double Ax,
    double Ay)
{
    public double Speed => Math.Sqrt(Vx * Vx + Vy * Vy);

    public double AccelerationMagnitude => Math.Sqrt(Ax * Ax + Ay * Ay);
}
