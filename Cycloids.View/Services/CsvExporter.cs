using System.Globalization;
using Cycloids.Model.Parameters;
using Cycloids.Model.State;

namespace Cycloids.View.Services;

/// <summary>
/// Сервис экспорта результатов вычислительного эксперимента в CSV.
/// Обеспечивает воспроизводимость: сохраняет метаданные запуска
/// (параметры колеса, угловую скорость, период, дату) и числовой ряд.
/// </summary>
public static class CsvExporter
{
    public static void ExportToCsv(
        string filePath,
        IReadOnlyList<PointState> points,
        double vLinWheel,
        double rWheel,
        double rDot,
        double dt,
        double tEnd,
        CycloidParameters? parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(points);

        using var writer = new StreamWriter(filePath, append: false, System.Text.Encoding.UTF8);

        // Блок метаданных эксперимента (комментарии #)
        writer.WriteLine("# Лабораторная работа №1. Циклоида: точка на ободе колеса");
        writer.WriteLine($"# Дата расчёта: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine($"# vLinWheel: {vLinWheel:F4} м/с");
        writer.WriteLine($"# rWheel: {rWheel:F4} м");
        writer.WriteLine($"# rDot: {rDot:F4} м");
        writer.WriteLine($"# dt: {dt:F4} с");
        writer.WriteLine($"# tEnd: {tEnd:F4} с");

        if (parameters is not null)
        {
            writer.WriteLine($"# AngularVelocity (omega): {parameters.AngularVelocity:F4} рад/с");
            writer.WriteLine($"# Period: {parameters.Period:F4} с");
            writer.WriteLine($"# DistancePerPeriod: {parameters.DistancePerPeriod:F4} м");
            writer.WriteLine($"# Kind: {parameters.Kind}");
        }

        // Заголовки столбцов с единицами измерения
        writer.WriteLine("time_s,x_m,y_m,speed_ms,vx_ms,vy_ms,ax_ms2,ay_ms2");

        // Строки данных
        foreach (var p in points)
        {
            writer.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0:F4},{1:F6},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6},{7:F6}",
                p.Time, p.X, p.Y, p.Speed, p.Vx, p.Vy, p.Ax, p.Ay));
        }
    }
}
