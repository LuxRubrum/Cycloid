using System.Globalization;
using Cycloids.Model.Calculations;
using Cycloids.Model.Parameters;
using Cycloids.Model.Services;
using Cycloids.Model.State;

namespace Cycloids.ViewModel;

/// <summary>
/// Главная ViewModel приложения по шаблону MVVM.
/// Отвечает за:
/// 1. Валидацию пользовательских параметров (vLinWheel, rWheel, rDot, dt, tEnd)
/// 2. Преобразование входных величин в параметры физической модели (ω = vLinWheel / rWheel)
/// 3. Вызов численного сэмплера TrajectorySampler
/// 4. Хранение результатов расчёта и точек для отметок t = 0.4 с и t = 0.8 с
/// 5. Оповещение View через события обновления
/// </summary>
public sealed class MainViewModel
{
    private readonly ITrajectorySampler _sampler;

    // ── Входные параметры (со значениями по умолчанию) ───────
    public double VLinWheel { get; private set; } = 10.0;
    public double RWheel    { get; private set; } = 1.0;
    public double RDot      { get; private set; } = 0.5;
    public double Dt        { get; private set; } = 0.01;
    public double TEnd      { get; private set; } = 2.0;

    // ── Результаты расчёта ────────────────────────────────────
    public IReadOnlyList<PointState>? Points { get; private set; }
    public CycloidParameters? CurrentParameters { get; private set; }
    public ICycloidAnalytics? Analytics { get; private set; }

    public PointState? PointAt04 { get; private set; }
    public PointState? PointAt08 { get; private set; }

    public bool HasResults => Points is not null && Points.Count > 0;
    public string StatusMessage { get; private set; } = "Готов к расчёту. Введите параметры и нажмите «Запустить».";

    // ── События обновления (по ТЗ архитектуры MVVM) ──────────
    public event EventHandler? InputUpdated;
    public event EventHandler? CalculationCompleted;
    public event Action<string>? ErrorOccurred;

    public MainViewModel(ITrajectorySampler? sampler = null)
    {
        _sampler = sampler ?? new TrajectorySampler();
    }

    /// <summary>
    /// Валидация входных строк из View (блок валидации UI-VM по ТЗ).
    /// </summary>
    public bool ValidateInputs(
        string vLinWheelText,
        string rWheelText,
        string rDotText,
        string dtText,
        string tEndText,
        out string errorMessage)
    {
        errorMessage = string.Empty;

        // Вспомогательный метод парсинга независимо от запятой или точки
        static bool TryParseNumber(string text, out double value) =>
            double.TryParse(text.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        if (!TryParseNumber(rWheelText, out double rWheel) || rWheel <= 0 || !double.IsFinite(rWheel))
        {
            errorMessage = "Радиус колеса rWheel должен быть строго положительным числом (rWheel > 0).";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        if (!TryParseNumber(rDotText, out double rDot) || rDot <= 0 || !double.IsFinite(rDot))
        {
            errorMessage = "Расстояние до точки rDot должно быть строго положительным числом (rDot > 0).";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        if (!TryParseNumber(vLinWheelText, out double vLin) || !double.IsFinite(vLin))
        {
            errorMessage = "Линейная скорость колеса vLinWheel должна быть действительным числом.";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        if (!TryParseNumber(dtText, out double dt) || dt <= 0 || !double.IsFinite(dt))
        {
            errorMessage = "Шаг по времени dt должен быть строго положительным числом (dt > 0).";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        if (!TryParseNumber(tEndText, out double tEnd) || tEnd <= 0 || !double.IsFinite(tEnd))
        {
            errorMessage = "Время расчёта tEnd должно быть строго положительным числом (tEnd > 0).";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        if (dt >= tEnd)
        {
            errorMessage = "Шаг времени dt должен быть меньше общего времени расчёта tEnd.";
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }

        // Сохраняем проверенные значения
        VLinWheel = vLin;
        RWheel    = rWheel;
        RDot      = rDot;
        Dt        = dt;
        TEnd      = tEnd;

        InputUpdated?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Запуск моделирования по проверенным параметрам (Блок управления временем и расчётами).
    /// </summary>
    public bool RunSimulation(out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            // Угловая скорость: ω = vLinWheel / rWheel
            // Если скорость колеса нулевая, задаём минимальную эпсилон для поддержания формул
            double omega = Math.Abs(VLinWheel) > 1e-9 
                ? (VLinWheel / RWheel) 
                : 1e-9;

            // Создаём параметры модели (Core Model)
            // При начальной фазе 0 в момент t=0 точка находится в нижнем положении (y = R - r)
            var parameters = new CycloidParameters(
                Radius: RWheel,
                AngularVelocity: omega,
                PointOffset: RDot,
                InitialPhase: 0);

            parameters.EnsureValid();

            int steps = Math.Max(10, (int)Math.Round((TEnd - 0.0) / Dt));

            // Вызываем сэмплер траектории модели
            Points = _sampler.Sample(parameters, 0.0, TEnd, steps);
            CurrentParameters = parameters;
            Analytics = new CycloidAnalytics(parameters);

            // Находим контрольные точки в моменты t = 0.4 с и t = 0.8 с
            PointAt04 = FindClosestPoint(0.4);
            PointAt08 = FindClosestPoint(0.8);

            StatusMessage = $"Расчёт завершён успешно. Сгенерировано {Points.Count} точек.";
            CalculationCompleted?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Ошибка во время расчёта: {ex.Message}";
            StatusMessage = errorMessage;
            ErrorOccurred?.Invoke(errorMessage);
            return false;
        }
    }

    private PointState? FindClosestPoint(double targetTime)
    {
        if (Points is null || Points.Count == 0) return null;
        if (targetTime > TEnd) return null;

        PointState closest = Points[0];
        double minDiff = Math.Abs(closest.Time - targetTime);

        for (int i = 1; i < Points.Count; i++)
        {
            double diff = Math.Abs(Points[i].Time - targetTime);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = Points[i];
            }
        }

        return closest;
    }
}
