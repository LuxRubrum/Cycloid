using Cycloids.Model.Enums;
using Cycloids.Model.State;
using Cycloids.ViewModel;
using ScottPlot;

namespace Cycloids.View;

/// <summary>
/// Главная форма представления (View) по архитектуре MVVM.
/// Отвечает исключительно за UI: отображение элементов,
/// перехват кликов пользователя и отрисовку графиков ScottPlot.
/// Физические формулы и шаг расчёта здесь отсутствуют (делегируются во ViewModel).
/// </summary>
public partial class MainForm : Form
{
    private readonly MainViewModel _viewModel;

    public MainForm(MainViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        InitializeComponent();
        SetupPlotStyle();

        // Подписываемся на события ViewModel
        _viewModel.CalculationCompleted += OnCalculationCompleted;
        _viewModel.ErrorOccurred        += OnErrorOccurred;
    }

    /// <summary>
    /// Начальная настройка внешнего вида графика (PlotArea).
    /// </summary>
    private void SetupPlotStyle()
    {
        formsPlot1.Plot.Title("Траектория точки катящегося колеса (Циклоида)");
        formsPlot1.Plot.XLabel("x, метры");
        formsPlot1.Plot.YLabel("y, метры");
        formsPlot1.Plot.ShowLegend();
        formsPlot1.Refresh();
    }

    /// <summary>
    /// Обработчик кнопки запуска расчёта (UI-V -> UI-VM).
    /// </summary>
    private void buttonRun_Click(object? sender, EventArgs e)
    {
        // 1. Валидация входных данных через ViewModel
        if (!_viewModel.ValidateInputs(
            textBoxVLinWheel.Text,
            textBoxRWheel.Text,
            textBoxRDot.Text,
            textBoxDt.Text,
            textBoxTEnd.Text,
            out string validationError))
        {
            MessageBox.Show(validationError, "Ошибка ввода", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // 2. Запуск моделирования через ViewModel
        labelStatus.Text = "Статус: вычисление траектории...";
        if (!_viewModel.RunSimulation(out string calcError))
        {
            MessageBox.Show(calcError, "Ошибка расчёта", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Обработка успешного завершения расчёта (UI-V обновляет отрисовку).
    /// </summary>
    private void OnCalculationCompleted(object? sender, EventArgs e)
    {
        if (!_viewModel.HasResults || _viewModel.Points is null) return;

        // 1. Обновляем информационную панель (InfoPanel)
        UpdateInfoPanel();

        // 2. Отрисовываем траекторию и маркеры на графике (PlotArea)
        DrawTrajectory(_viewModel.Points);

        // 3. Активируем кнопку экспорта
        buttonExport.Enabled = true;
        labelStatus.Text = _viewModel.StatusMessage;
    }

    /// <summary>
    /// Отображение аналитических данных и характеристик циклоиды.
    /// </summary>
    private void UpdateInfoPanel()
    {
        if (_viewModel.CurrentParameters is null) return;

        string kindText = _viewModel.CurrentParameters.Kind switch
        {
            CycloidType.Common  => "Обыкновенная (r = R)",
            CycloidType.Curtate => "Укороченная (r < R)",
            CycloidType.Prolate => "Удлинённая (r > R)",
            _                   => "—"
        };

        labelKind.Text     = $"Тип циклоиды:\n  {kindText}";
        labelPeriod.Text   = $"Период оборота T:\n  {_viewModel.CurrentParameters.Period:F3} с";
        labelDistance.Text = $"Смещение за оборот:\n  {_viewModel.CurrentParameters.DistancePerPeriod:F3} м";

        if (_viewModel.PointAt04.HasValue)
        {
            var p = _viewModel.PointAt04.Value;
            labelSpeed04.Text = $"Скорость при t=0.4 с:\n  V = {p.Speed:F3} м/с (Vx={p.Vx:F2}, Vy={p.Vy:F2})";
        }

        if (_viewModel.PointAt08.HasValue)
        {
            var p = _viewModel.PointAt08.Value;
            labelSpeed08.Text = $"Скорость при t=0.8 с:\n  V = {p.Speed:F3} м/с (Vx={p.Vx:F2}, Vy={p.Vy:F2})";
        }
    }

    /// <summary>
    /// Отрисовка траектории, линии дороги, маркеров и векторов скорости.
    /// </summary>
    private void DrawTrajectory(IReadOnlyList<PointState> points)
    {
        formsPlot1.Plot.Clear();
        SetupPlotStyle();

        // Извлекаем массивы координат X и Y
        double[] xs = new double[points.Count];
        double[] ys = new double[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            xs[i] = points[i].X;
            ys[i] = points[i].Y;
        }

        // 1. Непрерывная линия траектории
        var scatter = formsPlot1.Plot.Add.Scatter(xs, ys);
        scatter.LegendText = "Траектория точки";
        scatter.Color      = ScottPlot.Color.FromHex("#1976D2"); // насыщенный синий
        scatter.LineWidth  = 2.2f;
        scatter.MarkerSize = 0;

        // 2. Линия поверхности / дороги (y = 0)
        var road = formsPlot1.Plot.Add.HorizontalLine(0);
        road.Color     = ScottPlot.Color.FromHex("#757575"); // нейтральный серый
        road.LineWidth = 1.2f;

        // 3. Маркеры и векторы скорости в моменты t = 0.4 с и t = 0.8 с
        if (_viewModel.PointAt04.HasValue)
            DrawVelocityMarker(_viewModel.PointAt04.Value, "#D32F2F", "t = 0.4 с"); // красный

        if (_viewModel.PointAt08.HasValue)
            DrawVelocityMarker(_viewModel.PointAt08.Value, "#388E3C", "t = 0.8 с"); // зелёный

        formsPlot1.Plot.Axes.AutoScale();
        formsPlot1.Refresh();
    }

    /// <summary>
    /// Отрисовка маркера положения и вектора скорости по соседним точкам.
    /// </summary>
    private void DrawVelocityMarker(PointState pt, string hexColor, string title)
    {
        var color = ScottPlot.Color.FromHex(hexColor);

        // Маркер точки на траектории
        var marker = formsPlot1.Plot.Add.Marker(pt.X, pt.Y);
        marker.MarkerStyle.Shape     = MarkerShape.FilledCircle;
        marker.MarkerStyle.Size      = 10;
        marker.MarkerStyle.FillColor = color;
        marker.LegendText            = title;

        // Стрелка вектора линейной скорости
        // Вектор направлен по скорости (Vx, Vy) и масштабирован для наглядности
        double scale = 0.08;
        var tail = new Coordinates(pt.X, pt.Y);
        var tip  = new Coordinates(pt.X + pt.Vx * scale, pt.Y + pt.Vy * scale);

        var arrow = formsPlot1.Plot.Add.Arrow(tail, tip);
        arrow.ArrowStyle.LineColor = color;
        arrow.ArrowStyle.LineWidth = 2.5f;

        // Текстовая аннотация рядом с вектором: модуль линейной скорости по ТЗ
        var label = formsPlot1.Plot.Add.Text(
            $"{title}\nV = {pt.Speed:F2} м/с\n({pt.X:F2}; {pt.Y:F2})",
            pt.X + pt.Vx * scale * 1.15,
            pt.Y + pt.Vy * scale * 1.15);

        label.LabelFontColor = color;
        label.LabelFontSize  = 9.5f;
        label.LabelBold      = true;
    }

    /// <summary>
    /// Экспорт результатов расчёта в CSV с метаданными.
    /// </summary>
    private void buttonExport_Click(object? sender, EventArgs e)
    {
        if (!_viewModel.HasResults || _viewModel.Points is null) return;

        using var sfd = new SaveFileDialog
        {
            Filter   = "CSV файлы (*.csv)|*.csv",
            FileName = "cycloid_trajectory.csv",
            Title    = "Экспорт результатов моделирования циклоиды"
        };

        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            Services.CsvExporter.ExportToCsv(
                sfd.FileName,
                _viewModel.Points,
                _viewModel.VLinWheel,
                _viewModel.RWheel,
                _viewModel.RDot,
                _viewModel.Dt,
                _viewModel.TEnd,
                _viewModel.CurrentParameters);

            MessageBox.Show("Данные успешно экспортированы!", "Экспорт", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка при экспорте файла: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnErrorOccurred(string message)
    {
        labelStatus.Text = $"Ошибка: {message}";
    }
}
