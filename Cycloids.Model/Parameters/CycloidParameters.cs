using Cycloids.Model.Enums;

namespace Cycloids.Model.Parameters;

/// <summary>
/// Входные данные модели являются неизменяемыми.
/// Колесо катится без проскальзывания вдоль оси X, поверхность земли — y = 0.
/// </summary>
/// <param name="Radius">Радиус колеса R > 0</param>
/// <param name="AngularVelocity">Угловая скорость ω (неравная нулю). При ω > 0 катится вправо (+X), при ω < 0 — влево.</param>
/// <param name="PointOffset">Расстояние d ≥ 0 от центра колеса до отслеживаемой точки. При d = R точка находится на ободе</param>
/// <param name="InitialPhase">Начальный угол φ0 точки, измеренный от направления прямо вниз (в радианах)</param>
public sealed record CycloidParameters(
    double Radius,
    double AngularVelocity,
    double PointOffset,
    double InitialPhase = 0)
{
    /// <summary> Относительный допущение, используемое для принятия решения о том, что d равно R </summary>
    public const double RelativeTolerance = 1e-9;

    public CycloidType Kind
    {
        get
        {
            if (Math.Abs(PointOffset - Radius) <= RelativeTolerance * Radius)
                return CycloidType.Common;
            return PointOffset < Radius ? CycloidType.Curtate : CycloidType.Prolate;
        }
    }

    /// <summary> Время одного полного оборота колеса за период T = 2π / |ω| </summary>
    public double Period => 2 * Math.PI / Math.Abs(AngularVelocity);

    /// <summary> Расстояние, проходимое центром колеса за один оборот в 2πR </summary>
    public double DistancePerPeriod => 2 * Math.PI * Radius;

    public bool IsValid => GetErrors().Count == 0;

    /// <summary> Готовый список list сообщений об ошибках валидации </summary>
    public IReadOnlyList<string> GetErrors()
    {
        var errors = new List<string>();

        if (!double.IsFinite(Radius) || Radius <= 0)
            errors.Add("Радиус должен быть числом больше 0 и конечным числом");

        if (!double.IsFinite(AngularVelocity) || AngularVelocity == 0)
            errors.Add("Угловая скорость не должна быть бесконечной или равной нулю");

        if (!double.IsFinite(PointOffset) || PointOffset < 0)
            errors.Add("Координата смещения точки должна быть конечным числом, а также больше или равным 0");

        if (!double.IsFinite(InitialPhase))
            errors.Add("Значение начальной фазы должно представлять собой конечное число");

        return errors;
    }

    /// <summary>Вызывает <see cref="ArgumentException"/>, если параметры недопустимы</summary>
    public void EnsureValid()
    {
        var errors = GetErrors();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(CycloidParameters));
    }
}
