namespace Cycloids.Model.State;

/// <summary> Состояние колеса в момент времени <see cref="Time"/>.</summary>
/// <param name="Time"> Время, с.</param>
/// <param name="CenterX"> Координата X центра колеса (R*ω*t).</param>
/// <param name="CenterY"> Координата Y центра колеса (всегда R).</param>
/// <param name="RotationAngle"> Угол поворота колеса ω*t (в радианах, без приведения к диапазону [0, 2π]). При ω > 0 — вращение по часовой стрелке.</param>
/// <param name="PointAngle"> Угол положения отслеживаемой точки относительно направления строго вниз: φ0 + ω*t (радианы, без приведения к диапазону [0, 2π]).</param>
public readonly record struct WheelState(
    double Time,
    double CenterX,
    double CenterY,
    double RotationAngle,
    double PointAngle);
