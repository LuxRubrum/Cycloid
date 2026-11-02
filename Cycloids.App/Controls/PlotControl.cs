using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Cycloids.ViewModel;
using Cycloids.ViewModel.Infrastructure;
using Cycloids.ViewModel.Models;

namespace Cycloids.App.Controls;

/// <summary>
/// График сцены: земля, колесо, траектория точки, маркеры t = 0,4 и 0,8 с с векторами скорости,
/// мгновенный центр скоростей (точка касания), оси с единицами и легенда.
/// Рисует только то, что посчитано во ViewModel: физических формул здесь нет.
/// </summary>
public sealed class PlotControl : Control
{
    private const double MarginLeft = 54;
    private const double MarginRight = 16;
    private const double MarginTop = 30;
    private const double MarginBottom = 38;

    private enum Align { Left, Center, Right }

    private static SolidColorBrush Solid(string hex) => new(Color.Parse(hex));

    private static readonly IBrush PaperBrush = Solid("#FFFFFF");
    private static readonly IBrush GroundFill = Solid("#F3EFE7");
    private static readonly IBrush InkBrush = Solid("#1F2933");
    private static readonly IBrush MutedBrush = Solid("#616E7C");
    private static readonly IBrush TrajectoryBrush = Solid("#1565C0");
    private static readonly IBrush WheelFill = new SolidColorBrush(Color.FromArgb(22, 31, 41, 51));
    private static readonly IBrush WheelLineBrush = Solid("#8A94A0");
    private static readonly IBrush IcsBrush = Solid("#7B1FA2");
    private static readonly IBrush Marker1Brush = Solid("#D84315");
    private static readonly IBrush Marker2Brush = Solid("#2E7D32");
    private static readonly IBrush LegendFill = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));

    private static readonly Pen GridPen = new(Solid("#E8EBEF"), 1);
    private static readonly Pen GroundPen = new(Solid("#1F2933"), 2);
    private static readonly Pen FramePen = new(Solid("#C5CBD3"), 1);
    private static readonly Pen WheelPen = new(WheelLineBrush, 1.6);
    private static readonly Pen SpokePen = new(WheelLineBrush, 1.2);
    private static readonly Pen FaintSpokePen = new(Solid("#C5CBD3"), 1);
    private static readonly Pen TrajectoryPen = new(TrajectoryBrush, 2.2, lineJoin: PenLineJoin.Round);
    private static readonly Pen IcsPen = new(IcsBrush, 1.2, new DashStyle(new double[] { 4, 3 }, 0));
    private static readonly Pen ImportPen = new(Solid("#6B7280"), 2, new DashStyle(new double[] { 6, 4 }, 0));
    private static readonly Pen WhitePen = new(Solid("#FFFFFF"), 1.5);

    private static readonly Typeface NormalFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.Normal);
    private static readonly Typeface BoldFace = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    private MainViewModel? _vm;

    // текущее преобразование мир → экран: sx = _ox + x·_s, sy = _oy − y·_s
    private double _s = 1;
    private double _ox;
    private double _oy;

    // кэш геометрии траектории (в экранных координатах)
    private StreamGeometry? _geometry;
    private object? _geometryTrajectory;
    private double _geometryScale;
    private double _geometryOx;
    private double _geometryOy;

    // кэш геометрии импортированной траектории
    private StreamGeometry? _importGeometry;
    private object? _importGeometryKey;
    private double _importGeometryScale;
    private double _importGeometryOx;
    private double _importGeometryOy;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;

        _vm = DataContext as MainViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnViewModelPropertyChanged;

        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e) => InvalidateVisual();

    private Point P(double x, double y) => new(_ox + x * _s, _oy - y * _s);

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var size = Bounds.Size;
        context.DrawRectangle(PaperBrush, null, new Rect(size));

        if (_vm is null || size.Width < 120 || size.Height < 120)
            return;

        var plot = new Rect(MarginLeft, MarginTop, size.Width - MarginLeft - MarginRight, size.Height - MarginTop - MarginBottom);
        if (plot.Width < 40 || plot.Height < 40)
            return;

        UpdateTransform(plot);

        double step = NiceStep(90 / _s);
        var xTicks = TickValues((plot.Left - _ox) / _s, (plot.Right - _ox) / _s, step);
        var yTicks = TickValues((_oy - plot.Bottom) / _s, (_oy - plot.Top) / _s, step);

        using (context.PushClip(plot))
        {
            DrawGroundAndGrid(context, plot, xTicks, yTicks);
            DrawImported(context);
            DrawTrajectory(context);
            DrawWheel(context);

            for (int i = 0; i < _vm.Markers.Count; i++)
            {
                var m = _vm.Markers[i];
                var brush = i == 0 ? Marker1Brush : Marker2Brush;
                string label = $"t = {RuFormat.N(m.Time, 1)} с, v = {RuFormat.N(m.Point.Speed, 1)} м/с";
                DrawReport(context, m, brush, label);
            }

            DrawCurrentPoint(context);
            DrawLegend(context, plot);
        }

        context.DrawRectangle(null, FramePen, plot);
        DrawAxisLabels(context, plot, step, xTicks, yTicks);
    }

    // ───────────── Масштаб ─────────────

    private void UpdateTransform(Rect plot)
    {
        var b = _vm!.Bounds;
        double xr = Math.Max(b.XMax - b.XMin, 1e-6);
        double yr = Math.Max(b.YMax - b.YMin, 1e-6);

        double wx0 = b.XMin - xr * 0.03;
        double wx1 = b.XMax + xr * 0.03;
        double wy0 = b.YMin - yr * 0.12;
        double wy1 = b.YMax + yr * 0.30; // запас сверху под стрелки и подписи

        if (_vm.AutoScale)
        {
            // одинаковый масштаб по осям, чтобы колесо оставалось круглым
            _s = Math.Min(plot.Width / (wx1 - wx0), plot.Height / (wy1 - wy0));
            double contentW = (wx1 - wx0) * _s;
            double contentH = (wy1 - wy0) * _s;
            _ox = plot.Left + (plot.Width - contentW) / 2 - wx0 * _s;
            _oy = plot.Bottom - (plot.Height - contentH) / 2 + wy0 * _s;
        }
        else
        {
            _s = Math.Max(_vm.ManualScale, 1e-3);
            _ox = plot.Left - wx0 * _s + 8;
            _oy = plot.Bottom + wy0 * _s - 8;
        }
    }

    // ───────────── Сетка, земля, оси ─────────────

    private void DrawGroundAndGrid(DrawingContext ctx, Rect plot, List<double> xTicks, List<double> yTicks)
    {
        double groundY = Math.Clamp(_oy, plot.Top, plot.Bottom);
        if (_oy < plot.Bottom)
            ctx.DrawRectangle(GroundFill, null, new Rect(plot.Left, groundY, plot.Width, plot.Bottom - groundY + 1));

        foreach (double x in xTicks)
            ctx.DrawLine(GridPen, new Point(P(x, 0).X, plot.Top), new Point(P(x, 0).X, plot.Bottom));
        foreach (double y in yTicks)
            ctx.DrawLine(GridPen, new Point(plot.Left, P(0, y).Y), new Point(plot.Right, P(0, y).Y));

        if (_oy >= plot.Top && _oy <= plot.Bottom)
            ctx.DrawLine(GroundPen, new Point(plot.Left, _oy), new Point(plot.Right, _oy));
    }

    private void DrawAxisLabels(DrawingContext ctx, Rect plot, double step, List<double> xTicks, List<double> yTicks)
    {
        int decimals = Math.Max(0, (int)Math.Ceiling(-Math.Log10(step) - 1e-9));

        foreach (double x in xTicks)
            DrawText(ctx, RuFormat.N(x, decimals), new Point(P(x, 0).X, plot.Bottom + 5), MutedBrush, 11, false, Align.Center);

        foreach (double y in yTicks)
            DrawText(ctx, RuFormat.N(y, decimals), new Point(plot.Left - 6, P(0, y).Y - 7), MutedBrush, 11, false, Align.Right);

        DrawText(ctx, "x, м", new Point(plot.Right - 4, plot.Bottom + 20), InkBrush, 12, true, Align.Right);
        DrawText(ctx, "y, м", new Point(plot.Left, 8), InkBrush, 12, true, Align.Left);
    }

    // ───────────── Объекты сцены ─────────────

    private void DrawTrajectory(DrawingContext ctx)
    {
        var points = _vm!.Trajectory;
        if (points.Count < 2)
            return;

        if (_geometry is null
            || !ReferenceEquals(_geometryTrajectory, points)
            || _geometryScale != _s || _geometryOx != _ox || _geometryOy != _oy)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(P(points[0].X, points[0].Y), false);
                for (int i = 1; i < points.Count; i++)
                    g.LineTo(P(points[i].X, points[i].Y));
                g.EndFigure(false);
            }

            _geometry = geometry;
            _geometryTrajectory = points;
            _geometryScale = _s;
            _geometryOx = _ox;
            _geometryOy = _oy;
        }

        ctx.DrawGeometry(null, TrajectoryPen, _geometry);
    }

    private void DrawImported(DrawingContext ctx)
    {
        var imported = _vm!.Imported;
        if (imported is null || imported.Points.Count < 2)
            return;

        if (_importGeometry is null
            || !ReferenceEquals(_importGeometryKey, imported)
            || _importGeometryScale != _s || _importGeometryOx != _ox || _importGeometryOy != _oy)
        {
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                var pts = imported.Points;
                g.BeginFigure(P(pts[0].X, pts[0].Y), false);
                for (int i = 1; i < pts.Count; i++)
                    g.LineTo(P(pts[i].X, pts[i].Y));
                g.EndFigure(false);
            }

            _importGeometry = geometry;
            _importGeometryKey = imported;
            _importGeometryScale = _s;
            _importGeometryOx = _ox;
            _importGeometryOy = _oy;
        }

        ctx.DrawGeometry(null, ImportPen, _importGeometry);
    }

    private void DrawWheel(DrawingContext ctx)
    {
        var w = _vm!.CurrentWheel;
        var p = _vm.CurrentPoint;
        double radius = _vm.Parameters.Radius;

        var center = P(w.CenterX, w.CenterY);
        double rPx = radius * _s;

        ctx.DrawEllipse(WheelFill, WheelPen, center, rPx, rPx);

        // Спицы: основная проходит через отслеживаемую точку, остальные повёрнуты на 120° и 240°,
        // чтобы вращение колеса было заметно.
        var pt = P(p.X, p.Y);
        double ux = pt.X - center.X;
        double uy = pt.Y - center.Y;
        double len = double.Hypot(ux, uy);

        if (len > 1e-6)
        {
            ux /= len;
            uy /= len;

            foreach (double angle in new[] { 2 * Math.PI / 3, 4 * Math.PI / 3 })
            {
                double c = Math.Cos(angle), s = Math.Sin(angle);
                double rx = ux * c - uy * s;
                double ry = ux * s + uy * c;
                ctx.DrawLine(FaintSpokePen, center, new Point(center.X + rx * rPx, center.Y + ry * rPx));
            }

            double spokeLen = Math.Max(rPx, len);
            ctx.DrawLine(SpokePen, center, new Point(center.X + ux * spokeLen, center.Y + uy * spokeLen));
        }

        ctx.DrawEllipse(WheelLineBrush, null, center, 3, 3);
    }

    private void DrawCurrentPoint(DrawingContext ctx)
    {
        var p = _vm!.CurrentPoint;
        var w = _vm.CurrentWheel;

        var pt = P(p.X, p.Y);
        var contact = P(w.CenterX, 0);

        ctx.DrawLine(IcsPen, contact, pt);
        ctx.DrawEllipse(IcsBrush, null, contact, 4, 4);

        var pen = new Pen(InkBrush, 2.2);
        DrawArrow(ctx, pt, p.Vx, p.Vy, p.Speed * _vm.VectorScale * _s, pen);
        ctx.DrawEllipse(InkBrush, WhitePen, pt, 5.5, 5.5);
    }

    private void DrawReport(DrawingContext ctx, PointReport rep, IBrush brush, string label)
    {
        var pt = P(rep.Point.X, rep.Point.Y);
        var contact = P(rep.ContactX, 0);

        ctx.DrawLine(IcsPen, contact, pt);
        ctx.DrawEllipse(IcsBrush, null, contact, 3.5, 3.5);

        var pen = new Pen(brush, 2.4);
        var tip = DrawArrow(ctx, pt, rep.Point.Vx, rep.Point.Vy, rep.Point.Speed * _vm!.VectorScale * _s, pen);
        ctx.DrawEllipse(brush, WhitePen, pt, 6, 6);

        DrawText(ctx, label, new Point(tip.X + 8, tip.Y - 18), brush, 12, true, Align.Left);
    }

    /// <summary>Рисует стрелку из точки <paramref name="from"/> по мировому направлению (dx, dy); возвращает остриё.</summary>
    private static Point DrawArrow(DrawingContext ctx, Point from, double dx, double dy, double lengthPx, IPen pen)
    {
        double n = double.Hypot(dx, dy);
        if (n < 1e-12 || lengthPx < 3)
            return from;

        double ux = dx / n;
        double uy = -dy / n; // экранная ось Y направлена вниз

        var tip = new Point(from.X + ux * lengthPx, from.Y + uy * lengthPx);
        ctx.DrawLine(pen, from, tip);

        double back = Math.Atan2(uy, ux) + Math.PI;
        const double head = 10;
        const double spread = 0.45;
        foreach (double sign in new[] { -1.0, 1.0 })
        {
            double a = back + sign * spread;
            ctx.DrawLine(pen, tip, new Point(tip.X + head * Math.Cos(a), tip.Y + head * Math.Sin(a)));
        }

        return tip;
    }

    // ───────────── Легенда ─────────────

    private void DrawLegend(DrawingContext ctx, Rect plot)
    {
        var rows = new List<(string Text, int Kind, IBrush Brush)>
        {
            ("Траектория точки", 0, TrajectoryBrush),
            ("Колесо и спицы", 1, WheelLineBrush),
            ("Текущее положение и вектор v", 2, InkBrush),
        };

        for (int i = 0; i < _vm!.Markers.Count; i++)
        {
            var m = _vm.Markers[i];
            rows.Add(($"Маркер t = {RuFormat.N(m.Time, 1)} с", 2, i == 0 ? Marker1Brush : Marker2Brush));
        }

        rows.Add(("МЦС: касание колеса с землёй", 3, IcsBrush));

        if (_vm.Imported is { } imported)
        {
            string file = imported.FileName.Length > 22 ? imported.FileName[..21] + "…" : imported.FileName;
            rows.Add(($"Импорт: {file}", 4, ImportPen.Brush!));
        }

        const double rowH = 19;
        const double width = 232;
        double height = rows.Count * rowH + 12;
        var box = new Rect(plot.Right - width - 10, plot.Top + 10, width, height);

        ctx.DrawRectangle(LegendFill, FramePen, box, 4, 4);

        for (int i = 0; i < rows.Count; i++)
        {
            var (text, kind, brush) = rows[i];
            double cy = box.Top + 6 + i * rowH + rowH / 2;
            double x0 = box.Left + 10;

            switch (kind)
            {
                case 0:
                    ctx.DrawLine(new Pen(brush, 2.4), new Point(x0, cy), new Point(x0 + 24, cy));
                    break;
                case 1:
                    ctx.DrawEllipse(null, new Pen(brush, 1.6), new Point(x0 + 12, cy), 7, 7);
                    break;
                case 2:
                    ctx.DrawLine(new Pen(brush, 2.2), new Point(x0, cy), new Point(x0 + 24, cy));
                    ctx.DrawEllipse(brush, WhitePen, new Point(x0 + 4, cy), 4.5, 4.5);
                    break;
                case 4:
                    ctx.DrawLine(ImportPen, new Point(x0, cy), new Point(x0 + 24, cy));
                    break;
                default:
                    ctx.DrawLine(IcsPen, new Point(x0, cy), new Point(x0 + 24, cy));
                    ctx.DrawEllipse(brush, null, new Point(x0 + 12, cy), 3.5, 3.5);
                    break;
            }

            DrawText(ctx, text, new Point(x0 + 34, cy - 8), InkBrush, 11.5, false, Align.Left);
        }
    }

    // ───────────── Вспомогательное ─────────────

    private static void DrawText(DrawingContext ctx, string text, Point at, IBrush brush, double size, bool bold, Align align)
    {
        var ft = new FormattedText(
            text, RuFormat.Culture, FlowDirection.LeftToRight, bold ? BoldFace : NormalFace, size, brush);

        double x = align switch
        {
            Align.Center => at.X - ft.Width / 2,
            Align.Right => at.X - ft.Width,
            _ => at.X,
        };

        ctx.DrawText(ft, new Point(x, at.Y));
    }

    /// <summary>Шаг сетки из ряда 1–2–5·10^k, не меньший заданного.</summary>
    private static double NiceStep(double raw)
    {
        if (!double.IsFinite(raw) || raw <= 0)
            return 1;

        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        double n = raw / magnitude;
        double factor = n <= 1 ? 1 : n <= 2 ? 2 : n <= 5 ? 5 : 10;
        return factor * magnitude;
    }

    private static List<double> TickValues(double from, double to, double step)
    {
        var list = new List<double>();
        if (!(to > from) || step <= 0)
            return list;

        long k0 = (long)Math.Ceiling(from / step);
        long k1 = (long)Math.Floor(to / step);
        if (k1 - k0 > 400)
            return list;

        for (long k = k0; k <= k1; k++)
            list.Add(k * step);

        return list;
    }
}
