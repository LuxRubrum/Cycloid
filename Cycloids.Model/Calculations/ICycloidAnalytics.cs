namespace Cycloids.Model.Calculations;

/// <summary> Характеристики траектории в замкнутой или интегральной форме для панели информации </summary>
public interface ICycloidAnalytics
{
    /// <summary> Период одного оборота колеса за T = 2π/|ω| </summary>
    double Period { get; }

    /// <summary> Горизонтальное расстояние, проходимое за один оборот в 2πR </summary>
    double DistancePerPeriod { get; }

    /// <summary> Самая высокая точка траектории на R + d </summary>
    double MaxHeight { get; }

    /// <summary> Самая низкая точка траектории на R − d (Для удлиненной циклоиды будет учитываться отрицательное значение : траектория опускаются ниже уровня земли) </summary>
    double MinHeight { get; }

    /// <summary> Минимальная скорость точки vmin = |ω|*|R − d| </summary>
    double MinSpeed { get; }

    /// <summary> Максимальная скорость точки vmax = |ω|*(R + d) </summary>
    double MaxSpeed { get; }

    /// <summary> Длина траектории за один период (обычно равна 8R для обычной циклоиды) </summary>
    double ArcLengthPerPeriod { get; }

    /// <summary> Знакопеременная площадь между траекторией и осью X за один период: π*(2R^2 + d^2) </summary>
    double AreaPerPeriod { get; }

    /// <summary> Радиус кривизны в момент времени t (будет 0 в точке повтора/касания земли, и + бесконечность в точке перегиба </summary>
    double RadiusOfCurvature(double t);

    /// <summary> Моменты времени на отрезке от/до - [tStart, tEnd], когда точка касается земли с нулевой скоростью (только для обыкновенной циклоиды, где (d = R), расположенные по возрастанию) </summary>
    IReadOnlyList<double> GetCuspTimes(double tStart, double tEnd);
}
