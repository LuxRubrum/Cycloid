using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Cycloids.Model.Calculations;
using Cycloids.Model.Enums;
using Cycloids.Model.Parameters;
using Cycloids.Model.Services;
using Cycloids.Model.State;
using Cycloids.ViewModel.Infrastructure;
using Cycloids.ViewModel.Models;
using Cycloids.ViewModel.Services;

namespace Cycloids.ViewModel;

/// <summary>
/// Связующее звено между View (Avalonia) и физической моделью (Cycloids.Model).
/// Входы — текстовые поля в терминах задания (v, R, d, dt, T); любое изменение пересобирает модель.
/// Выходы — только для чтения: траектория, текущее состояние, маркеры t = 0,4 и 0,8 с, проверки.
/// Координаты математические (Y вверх); View сам масштабирует и переворачивает Y.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    public const double FirstMarkerTime = 0.4;
    public const double SecondMarkerTime = 0.8;

    private const int MaxSamples = 200_000;
    private const double RadiansPerDegree = Math.PI / 180.0;

    private readonly ITrajectorySampler _sampler;
    private readonly IFrameTimer _timer;
    private readonly Stopwatch _clock = new();
    private TimeSpan _lastTick;

    // входы
    private string _vText = "10";
    private string _rText = "1";
    private string _dText = "0.5";
    private string _dtText = "0.005";
    private string _tText = "1.2";
    private string _phaseText = "0";
    private string _speedText = "1";

    // состояние
    private double _timeScale = 1;
    private double _linearVelocity = 10;
    private double _simulationTime = 1.2;
    private double _currentTime;
    private bool _isPlaying;
    private bool _autoScale = true;
    private double _manualScale = 60;
    private string? _errorMessage;
    private string? _statusMessage;

    // объекты модели (пересобираются при каждом изменении входов)
    private ICycloidCalculator _calculator = null!;
    private CycloidParameters _parameters = null!;
    private ICycloidAnalytics _analytics = null!;
    private IReadOnlyList<PointState> _trajectory = Array.Empty<PointState>();
    private IReadOnlyList<PointReport> _markers = Array.Empty<PointReport>();
    private IReadOnlyList<string> _analyticsLines = Array.Empty<string>();
    private IReadOnlyList<string> _verificationLines = Array.Empty<string>();
    private SceneBounds _bounds = new(0, 1, 0, 1);
    private PointState _currentPoint;
    private WheelState _currentWheel;
    private string _currentReadout = string.Empty;
    private double _omega;
    private double _actualDt;
    private double _vectorScale = 0.1;
    private bool _verificationOk = true;
    private SceneBounds _baseBounds = new(0, 1, 0, 1);
    private SceneBounds? _importedExtent;
    private ImportedExperiment? _imported;
    private bool _playImportedData;
    private IReadOnlyList<string> _importLines = Array.Empty<string>();
    private readonly RelayCommand _clearImportCommand;

    public MainViewModel(IFrameTimer timer, ITrajectorySampler? sampler = null)
    {
        _timer = timer ?? throw new ArgumentNullException(nameof(timer));
        _sampler = sampler ?? new TrajectorySampler();
        _timer.Tick += OnTick;

        PlayPauseCommand = new RelayCommand(TogglePlay, () => !HasError);
        ResetCommand = new RelayCommand(() => CurrentTime = 0);
        _clearImportCommand = new RelayCommand(ClearImport, () => HasImport);
        ClearImportCommand = _clearImportCommand;

        Rebuild();
    }

    // ───────────── Входы (TwoWay) ─────────────

    /// <summary>Линейная скорость оси колеса v, м/с. Может быть отрицательной (катится влево), но не нулевой.</summary>
    public string VLinWheelText
    {
        get => _vText;
        set { if (SetProperty(ref _vText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Радиус колеса R, м.</summary>
    public string WheelRadiusText
    {
        get => _rText;
        set { if (SetProperty(ref _rText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Расстояние d от оси колеса до отслеживаемой точки, м.</summary>
    public string PointOffsetText
    {
        get => _dText;
        set { if (SetProperty(ref _dText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Шаг времени dt, с.</summary>
    public string DtText
    {
        get => _dtText;
        set { if (SetProperty(ref _dtText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Полное время моделирования T, с (анимация зациклена на этом отрезке).</summary>
    public string SimulationTimeText
    {
        get => _tText;
        set { if (SetProperty(ref _tText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Начальная фаза φ0 в градусах (0 — точка в самом нижнем положении).</summary>
    public string InitialPhaseText
    {
        get => _phaseText;
        set { if (SetProperty(ref _phaseText, value)) { _playImportedData = false; Rebuild(); } }
    }

    /// <summary>Скорость анимации (1 — реальное время).</summary>
    public string TimeScaleText
    {
        get => _speedText;
        set { if (SetProperty(ref _speedText, value)) Rebuild(); }
    }

    public bool AutoScale
    {
        get => _autoScale;
        set => SetProperty(ref _autoScale, value);
    }

    /// <summary>Масштаб в пикселях на метр, когда автомасштаб выключен.</summary>
    public double ManualScale
    {
        get => _manualScale;
        set => SetProperty(ref _manualScale, value);
    }

    /// <summary>Текущее время анимации; к нему привязывается ползунок (0 … <see cref="MaxTime"/>).</summary>
    public double CurrentTime
    {
        get => _currentTime;
        set
        {
            if (SetProperty(ref _currentTime, value))
                UpdateState();
        }
    }

    // ───────────── Выходы (только чтение для View) ─────────────

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(CanExport));
                ((RelayCommand)PlayPauseCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasError => _errorMessage is not null;

    public bool CanExport => !HasError;

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (SetProperty(ref _isPlaying, value))
                OnPropertyChanged(nameof(PlayPauseLabel));
        }
    }

    public string PlayPauseLabel => IsPlaying ? "Пауза" : "Пуск";

    public CycloidParameters Parameters => _parameters;

    public ICycloidAnalytics Analytics => _analytics;

    /// <summary>ω = v / R, рад/с.</summary>
    public double Omega
    {
        get => _omega;
        private set
        {
            if (SetProperty(ref _omega, value))
                OnPropertyChanged(nameof(OmegaText));
        }
    }

    public string OmegaText => $"ω = v / R = {RuFormat.N(Omega, 3)} рад/с";

    /// <summary>Фактический шаг дискретизации: T делится на целое число шагов.</summary>
    public double ActualDt
    {
        get => _actualDt;
        private set => SetProperty(ref _actualDt, value);
    }

    public double MaxTime => _simulationTime;

    public IReadOnlyList<PointState> Trajectory
    {
        get => _trajectory;
        private set => SetProperty(ref _trajectory, value);
    }

    /// <summary>Фиксированные отметки t = 0,4 и 0,8 с (только те, что попадают в интервал моделирования).</summary>
    public IReadOnlyList<PointReport> Markers
    {
        get => _markers;
        private set => SetProperty(ref _markers, value);
    }

    public PointState CurrentPoint
    {
        get => _currentPoint;
        private set => SetProperty(ref _currentPoint, value);
    }

    public WheelState CurrentWheel
    {
        get => _currentWheel;
        private set => SetProperty(ref _currentWheel, value);
    }

    public string CurrentReadout
    {
        get => _currentReadout;
        private set => SetProperty(ref _currentReadout, value);
    }

    public IReadOnlyList<string> AnalyticsLines
    {
        get => _analyticsLines;
        private set => SetProperty(ref _analyticsLines, value);
    }

    public IReadOnlyList<string> VerificationLines
    {
        get => _verificationLines;
        private set => SetProperty(ref _verificationLines, value);
    }

    public bool VerificationOk
    {
        get => _verificationOk;
        private set => SetProperty(ref _verificationOk, value);
    }

    /// <summary>Мировой прямоугольник, который должен поместиться на графике (траектория + колесо).</summary>
    public SceneBounds Bounds
    {
        get => _bounds;
        private set => SetProperty(ref _bounds, value);
    }

    /// <summary>Длина стрелки скорости в метрах сцены на 1 м/с (одинакова для всех векторов).</summary>
    public double VectorScale
    {
        get => _vectorScale;
        private set => SetProperty(ref _vectorScale, value);
    }

    public CycloidType Kind => _parameters.Kind;

    // ───────────── Команды ─────────────

    public ICommand PlayPauseCommand { get; }

    public ICommand ResetCommand { get; }

    public ICommand ClearImportCommand { get; }

    // ───────────── Импорт ранее сохранённых результатов ─────────────

    /// <summary>Траектория, открытая из JSON (рисуется на графике пунктиром для сравнения), либо null.</summary>
    public ImportedExperiment? Imported
    {
        get => _imported;
        private set
        {
            if (SetProperty(ref _imported, value))
            {
                OnPropertyChanged(nameof(HasImport));
                _clearImportCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasImport => _imported is not null;

    /// <summary>Сводка по импортированному файлу и сравнение с текущим расчётом.</summary>
    public IReadOnlyList<string> ImportLines
    {
        get => _importLines;
        private set => SetProperty(ref _importLines, value);
    }

    /// <summary>
    /// Открывает результаты из JSON, сохранённого этой программой. При ошибке формата прежний импорт
    /// остаётся на месте, а причина выводится в <see cref="StatusMessage"/>.
    /// </summary>
    public void ImportJson(string json, string fileName)
        => ImportFrom(() => ExperimentImporter.Parse(json), fileName);

    /// <summary>Открывает результаты из CSV, сохранённого этой программой (метаданные в строках «# ключ: значение»).</summary>
    public void ImportCsv(string csv, string fileName)
        => ImportFrom(() => ExperimentImporter.ParseCsv(csv), fileName);

    /// <summary>Открывает файл результатов; формат выбирается по расширению имени (.csv, иначе JSON).</summary>
    public void Import(string text, string fileName)
    {
        if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            ImportCsv(text, fileName);
        else
            ImportJson(text, fileName);
    }

    private void ImportFrom(Func<ImportedExperiment> parse, string fileName)
    {
        try
        {
            var data = parse() with { FileName = fileName };

            double xMin = double.MaxValue, xMax = double.MinValue, yMin = double.MaxValue, yMax = double.MinValue;
            foreach (var pt in data.Points)
            {
                xMin = Math.Min(xMin, pt.X);
                xMax = Math.Max(xMax, pt.X);
                yMin = Math.Min(yMin, pt.Y);
                yMax = Math.Max(yMax, pt.Y);
            }

            var previousBaseBounds = _baseBounds;
            var previousImported = _imported;
            var previousExtent = _importedExtent;
            var previousPlay = _playImportedData;
            var previousInputs = (_vText, _rText, _dText, _dtText, _tText, _phaseText);

            Imported = data;
            _importedExtent = new SceneBounds(xMin, xMax, yMin, yMax);
            _playImportedData = true;
            ApplyImportedParameters(data.Parameters);

            if (HasError)
            {
                string reason = (ErrorMessage ?? string.Empty).Replace(Environment.NewLine, " ");

                Imported = previousImported;
                _importedExtent = previousExtent;
                _playImportedData = previousPlay;

                (_vText, _rText, _dText, _dtText, _tText, _phaseText) = previousInputs;
                OnPropertyChanged(nameof(VLinWheelText));
                OnPropertyChanged(nameof(WheelRadiusText));
                OnPropertyChanged(nameof(PointOffsetText));
                OnPropertyChanged(nameof(DtText));
                OnPropertyChanged(nameof(SimulationTimeText));
                OnPropertyChanged(nameof(InitialPhaseText));

                Rebuild();
                StatusMessage = $"Не удалось открыть «{fileName}»: {reason}";
                return;
            }

            _baseBounds = new SceneBounds(
                Math.Min(previousBaseBounds.XMin, _baseBounds.XMin),
                Math.Max(previousBaseBounds.XMax, _baseBounds.XMax),
                Math.Min(previousBaseBounds.YMin, _baseBounds.YMin),
                Math.Max(previousBaseBounds.YMax, _baseBounds.YMax));
            BuildImportLines();
            UpdateBounds();
            CurrentTime = 0;
            StatusMessage = $"Загружено: {fileName}";
        }
        catch (FormatException ex)
        {
            StatusMessage = $"Не удалось открыть «{fileName}»: {ex.Message}";
        }
    }

    private void ClearImport()
    {
        _playImportedData = false;
        _importedExtent = null;
        Imported = null;
        ImportLines = Array.Empty<string>();
        Rebuild();
        StatusMessage = "Импортированная траектория убрана.";
    }

    private void ApplyImportedParameters(ImportedParameters parameters)
    {
        _vText = FormatInput(parameters.LinearVelocity);
        _rText = FormatInput(parameters.Radius);
        _dText = FormatInput(parameters.PointOffset);
        _dtText = FormatInput(parameters.Step);
        _tText = FormatInput(parameters.SimulationTime);
        _phaseText = FormatInput(parameters.InitialPhase / RadiansPerDegree);

        OnPropertyChanged(nameof(VLinWheelText));
        OnPropertyChanged(nameof(WheelRadiusText));
        OnPropertyChanged(nameof(PointOffsetText));
        OnPropertyChanged(nameof(DtText));
        OnPropertyChanged(nameof(SimulationTimeText));
        OnPropertyChanged(nameof(InitialPhaseText));

        Rebuild();
    }

    private static string FormatInput(double value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private void BuildImportLines()
    {
        if (_imported is null)
        {
            ImportLines = Array.Empty<string>();
            return;
        }

        var ip = _imported.Parameters;
        var cur = _parameters;
        const double tol = 1e-9;

        var differs = new List<string>();
        if (!Close(ip.LinearVelocity, _linearVelocity, tol)) differs.Add("v");
        if (!Close(ip.Radius, cur.Radius, tol)) differs.Add("R");
        if (!Close(ip.PointOffset, cur.PointOffset, tol)) differs.Add("d");
        if (!Close(ip.InitialPhase, cur.InitialPhase, tol)) differs.Add("φ0");
        bool sameParameters = differs.Count == 0;

        // Сравнение положения и скорости в моменты времени из файла (для больших файлов — с шагом).
        var pts = _imported.Points;
        int stride = Math.Max(1, pts.Count / 20_000);
        double maxPos = 0, maxVel = 0;
        for (int i = 0; i < pts.Count; i += stride)
        {
            var f = pts[i];
            var m = _calculator.GetPointState(f.Time);
            maxPos = Math.Max(maxPos, double.Hypot(f.X - m.X, f.Y - m.Y));
            maxVel = Math.Max(maxVel, double.Hypot(f.Vx - m.Vx, f.Vy - m.Vy));
        }

        string verdict = sameParameters
            ? (maxPos < 1e-6 && maxVel < 1e-6 ? "Результаты воспроизводятся — ОК" : "РАСХОЖДЕНИЕ при одинаковых параметрах")
            : "Параметры отличаются, кривые могут не совпадать";

        ImportLines = new[]
        {
            $"Файл: {_imported.FileName}",
            $"Точек: {pts.Count}, T = {RuFormat.N(ip.SimulationTime, 3)} с, dt = {RuFormat.N(ip.Step, 4)} с",
            $"В файле: v = {RuFormat.N(ip.LinearVelocity, 3)} м/с, R = {RuFormat.N(ip.Radius, 3)} м, d = {RuFormat.N(ip.PointOffset, 3)} м, φ0 = {RuFormat.N(ip.InitialPhase / RadiansPerDegree, 1)}°",
            sameParameters ? "Параметры совпадают с текущими" : $"Отличаются параметры: {string.Join(", ", differs)}",
            $"Макс. отклонение положения: {RuFormat.Sci(maxPos)} м",
            $"Макс. отклонение скорости: {RuFormat.Sci(maxVel)} м/с",
            verdict,
        };
    }

    private static bool Close(double a, double b, double relativeTolerance)
        => Math.Abs(a - b) <= relativeTolerance * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b)));

    /// <summary>Границы сцены: текущая траектория вместе с импортированной, если она есть.</summary>
    private void UpdateBounds()
    {
        var b = _baseBounds;
        if (_importedExtent is { } e)
        {
            b = new SceneBounds(
                Math.Min(b.XMin, e.XMin),
                Math.Max(b.XMax, e.XMax),
                Math.Min(b.YMin, e.YMin),
                Math.Max(b.YMax, e.YMax));
        }

        Bounds = b;
    }

    // ───────────── Экспорт ─────────────

    public string BuildCsv() => ExperimentExporter.ToCsv(CreateSnapshot());

    public string BuildJson() => ExperimentExporter.ToJson(CreateSnapshot());

    public void ReportStatus(string message) => StatusMessage = message;

    private ExperimentSnapshot CreateSnapshot()
    {
        if (HasError)
            throw new InvalidOperationException("Экспорт недоступен: во входных данных есть ошибки.");

        return new ExperimentSnapshot(_parameters, _linearVelocity, ActualDt, _simulationTime, Trajectory, Markers);
    }

    // ───────────── Внутренняя логика ─────────────

    private void TogglePlay()
    {
        if (IsPlaying)
        {
            Pause();
            return;
        }

        _clock.Restart();
        _lastTick = TimeSpan.Zero;
        _timer.Start();
        IsPlaying = true;
    }

    private void Pause()
    {
        _timer.Stop();
        _clock.Stop();
        IsPlaying = false;
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed;
        double dt = (now - _lastTick).TotalSeconds;
        _lastTick = now;

        double t = CurrentTime + dt * _timeScale;
        double max = MaxTime;
        if (t >= max)
            t %= max; // зацикливание

        CurrentTime = t;
    }

    /// <summary>Проверяет входы и, если всё корректно, пересобирает калькулятор, аналитику, траекторию и маркеры.</summary>
    private void Rebuild()
    {
        var errors = new List<string>();

        bool okV = InputParser.TryParse(VLinWheelText, "Линейная скорость v", errors, out double v);
        bool okR = InputParser.TryParse(WheelRadiusText, "Радиус колеса R", errors, out double r);
        bool okD = InputParser.TryParse(PointOffsetText, "Расстояние до точки d", errors, out double offset);
        bool okDt = InputParser.TryParse(DtText, "Шаг времени dt", errors, out double dt);
        bool okT = InputParser.TryParse(SimulationTimeText, "Время моделирования T", errors, out double tMax);
        bool okPhase = InputParser.TryParse(InitialPhaseText, "Начальная фаза φ0", errors, out double phaseDeg);
        bool okScale = InputParser.TryParse(TimeScaleText, "Скорость анимации", errors, out double scale);

        if (okV && v == 0)
            errors.Add("Линейная скорость равна нулю: колесо стоит, траектории нет.");
        if (okR && r <= 0)
            errors.Add("Радиус колеса R должен быть больше 0.");
        if (okD && offset < 0)
            errors.Add("Расстояние d от оси до точки не может быть отрицательным.");
        if (okDt && dt <= 0)
            errors.Add("Шаг времени dt должен быть строго больше 0.");
        if (okT && tMax <= 0)
            errors.Add("Время моделирования T должно быть больше 0.");
        if (okScale && scale <= 0)
            errors.Add("Скорость анимации должна быть больше 0.");
        if (okDt && okT && dt > 0 && tMax > 0 && Math.Ceiling(tMax / dt) > MaxSamples)
            errors.Add($"Слишком много точек (больше {MaxSamples}): увеличьте шаг dt или уменьшите время T.");

        if (errors.Count > 0)
        {
            Fail(errors);
            return;
        }

        double omega = v / r;
        var parameters = new CycloidParameters(r, omega, offset, phaseDeg * RadiansPerDegree);

        var coreErrors = parameters.GetErrors();
        if (coreErrors.Count > 0)
        {
            Fail(coreErrors);
            return;
        }

        ErrorMessage = null;

        _timeScale = scale;
        _linearVelocity = v;
        _simulationTime = tMax;
        _parameters = parameters;
        _calculator = new CycloidCalculator(parameters);
        _analytics = new CycloidAnalytics(parameters);

        int steps = Math.Max(1, (int)Math.Ceiling(tMax / dt - 1e-9));
        ActualDt = tMax / steps;
        Trajectory = _sampler.Sample(parameters, 0, tMax, steps);
        Omega = omega;

        // Границы сцены: центр колеса проходит от 0 до R·ω·T, точка отстоит от центра не более чем на d.
        double half = Math.Max(r, offset);
        double centerEnd = r * omega * tMax;
        _baseBounds = new SceneBounds(
            XMin: Math.Min(0, centerEnd) - half,
            XMax: Math.Max(0, centerEnd) + half,
            YMin: Math.Min(0, r - offset),
            YMax: Math.Max(2 * r, r + offset));
        UpdateBounds();

        VectorScale = 1.2 * r / _analytics.MaxSpeed;

        BuildMarkers();
        BuildAnalyticsLines();
        BuildVerificationLines();
        BuildImportLines();

        OnPropertyChanged(nameof(Parameters));
        OnPropertyChanged(nameof(Analytics));
        OnPropertyChanged(nameof(Kind));
        OnPropertyChanged(nameof(MaxTime));

        if (CurrentTime > MaxTime)
            CurrentTime = 0;

        UpdateState();
    }

    private void Fail(IEnumerable<string> errors)
    {
        if (IsPlaying)
            Pause();

        // Последняя корректная картинка остаётся на экране.
        ErrorMessage = string.Join(Environment.NewLine, errors);
    }

    private void BuildMarkers()
    {
        var list = new List<PointReport>();
        foreach (double t in new[] { FirstMarkerTime, SecondMarkerTime })
        {
            if (t <= _simulationTime + 1e-12)
                list.Add(PointReport.Create(_calculator, t, ActualDt));
        }

        Markers = list;
    }

    private void BuildAnalyticsLines()
    {
        var a = _analytics;
        var p = _parameters;

        string kind = p.Kind switch
        {
            CycloidType.Curtate => "укороченная циклоида (d < R)",
            CycloidType.Common => "обыкновенная циклоида (d = R)",
            _ => "удлинённая циклоида (d > R)",
        };

        AnalyticsLines = new[]
        {
            $"Тип траектории: {kind}",
            $"Период оборота: {RuFormat.N(a.Period, 4)} с",
            $"Путь центра за оборот 2πR: {RuFormat.N(a.DistancePerPeriod, 3)} м",
            $"Высота точки: от {RuFormat.N(a.MinHeight, 3)} до {RuFormat.N(a.MaxHeight, 3)} м",
            $"Скорость точки: от {RuFormat.N(a.MinSpeed, 3)} до {RuFormat.N(a.MaxSpeed, 3)} м/с",
            $"Длина дуги за период: {RuFormat.N(a.ArcLengthPerPeriod, 3)} м",
            $"Площадь под аркой: {RuFormat.N(a.AreaPerPeriod, 3)} м²",
        };
    }

    private void BuildVerificationLines()
    {
        var lines = new List<string>();
        bool allOk = true;

        if (Markers.Count == 0)
        {
            lines.Add($"Маркеры не показаны: время T меньше {RuFormat.N(FirstMarkerTime, 1)} с.");
        }

        foreach (var m in Markers)
        {
            bool icsOk = m.IcsRelativeError < 1e-9;
            bool angleOk = double.IsNaN(m.AngleToRadiusDegrees) || Math.Abs(m.AngleToRadiusDegrees - 90) < 1e-6;
            double wh = Math.Abs(_omega) * ActualDt;
            bool ratioApplicable = wh <= 0.5 && m.NumericError > 1e-9;
            bool ratioOk = !ratioApplicable || (m.ConvergenceRatio > 3 && m.ConvergenceRatio < 5);
            allOk &= icsOk && angleOk && ratioOk;
            string ratioVerdict = ratioApplicable
                ? Mark(ratioOk)
                : $"не применимо (ω·h = {RuFormat.N(wh, 2)}, вне асимптотического диапазона)";

            lines.Add($"t = {RuFormat.N(m.Time, 1)} с");
            lines.Add($"  МЦС: |v| = {RuFormat.N(m.Point.Speed, 4)} м/с, |ω|·|CP| = {RuFormat.N(m.SpeedFromIcs, 4)} м/с — {Mark(icsOk)}");
            lines.Add($"  угол между v и CP: {RuFormat.N(m.AngleToRadiusDegrees, 4)}° — {Mark(angleOk)}");
            lines.Add($"  скорость по соседним точкам: погрешность {RuFormat.Sci(m.NumericError)} (h), {RuFormat.Sci(m.NumericHalfStepError)} (h/2), отношение {RuFormat.N(m.ConvergenceRatio, 2)}, ожидается ≈ 4 — {ratioVerdict}");
        }

        VerificationOk = allOk;
        VerificationLines = lines;
    }

    private static string Mark(bool ok) => ok ? "ОК" : "НЕ СОВПАДАЕТ";

    private void UpdateState()
    {
        if (_calculator is null)
            return;

        CurrentPoint = _playImportedData && _imported is not null
            ? InterpolateImportedPoint(CurrentTime)
            : _calculator.GetPointState(CurrentTime);
        CurrentWheel = _calculator.GetWheelState(CurrentTime);

        var rep = PointReport.Create(_calculator, CurrentTime, ActualDt);
        var s = CurrentPoint;

        CurrentReadout = string.Join(Environment.NewLine, new[]
        {
            $"t = {RuFormat.N(s.Time, 3)} с",
            $"x = {RuFormat.N(s.X, 3)} м,  y = {RuFormat.N(s.Y, 3)} м",
            $"vx = {RuFormat.N(s.Vx, 3)},  vy = {RuFormat.N(s.Vy, 3)} м/с",
            $"|v| = {RuFormat.N(s.Speed, 3)} м/с",
            $"|a| = {RuFormat.N(s.AccelerationMagnitude, 2)} м/с²",
            $"МЦС (касание): x = {RuFormat.N(rep.ContactX, 3)} м",
            $"|CP| = {RuFormat.N(rep.DistanceToContact, 3)} м,  |ω|·|CP| = {RuFormat.N(rep.SpeedFromIcs, 3)} м/с",
        });
    }

    private PointState InterpolateImportedPoint(double time)
    {
        var points = _imported!.Points;
        if (time <= points[0].Time)
            return points[0];
        if (time >= points[^1].Time)
            return points[^1];

        int lo = 0;
        int hi = points.Count - 1;
        while (hi - lo > 1)
        {
            int mid = lo + (hi - lo) / 2;
            if (points[mid].Time <= time)
                lo = mid;
            else
                hi = mid;
        }

        var a = points[lo];
        var b = points[hi];
        double factor = (time - a.Time) / (b.Time - a.Time);
        return new PointState(
            time,
            Lerp(a.X, b.X, factor),
            Lerp(a.Y, b.Y, factor),
            Lerp(a.Vx, b.Vx, factor),
            Lerp(a.Vy, b.Vy, factor),
            Lerp(a.Ax, b.Ax, factor),
            Lerp(a.Ay, b.Ay, factor));
    }

    private static double Lerp(double a, double b, double factor)
        => a + (b - a) * factor;
}
