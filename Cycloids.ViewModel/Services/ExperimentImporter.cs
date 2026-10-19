using System.Globalization;
using System.Text.Json;
using Cycloids.Model.State;
using Cycloids.ViewModel.Models;

namespace Cycloids.ViewModel.Services;

/// <summary>
/// Чтение JSON, сохранённого <see cref="ExperimentExporter"/>. Формат проверяется до использования данных:
/// при любой ошибке выбрасывается <see cref="FormatException"/> с понятным сообщением на русском.
/// </summary>
public static class ExperimentImporter
{
    public const int MaxPoints = 2_000_000;

    private static readonly string[] ExpectedColumns = { "t", "x", "y", "vx", "vy", "speed" };

    public static ImportedExperiment Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new FormatException("Файл пуст.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Файл не является корректным JSON: {ex.Message}", ex);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("Ожидался JSON-объект с результатами эксперимента.");

            string? name = root.TryGetProperty("experiment", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : null;
            if (name != ExperimentExporter.ExperimentName)
                throw new FormatException($"Файл не от этого эксперимента (ожидалось «{ExperimentExporter.ExperimentName}»).");

            double schema = Number(root, "schemaVersion", "schemaVersion");
            if (schema != ExperimentExporter.SchemaVersion)
                throw new FormatException($"Неподдерживаемая версия формата: {schema}.");

            if (!root.TryGetProperty("parameters", out var p) || p.ValueKind != JsonValueKind.Object)
                throw new FormatException("В файле нет раздела «parameters».");

            var parameters = new ImportedParameters(
                LinearVelocity: Number(p, "vLinWheel", "parameters.vLinWheel"),
                Radius: Number(p, "rWheel", "parameters.rWheel"),
                PointOffset: Number(p, "rDot", "parameters.rDot"),
                Omega: Number(p, "omega", "parameters.omega"),
                InitialPhase: Number(p, "initialPhase", "parameters.initialPhase"),
                Step: Number(p, "dt", "parameters.dt"),
                SimulationTime: Number(p, "simulationTime", "parameters.simulationTime"));

            CheckColumns(root);

            if (!root.TryGetProperty("points", out var pointsEl) || pointsEl.ValueKind != JsonValueKind.Array)
                throw new FormatException("В файле нет массива «points».");

            int count = pointsEl.GetArrayLength();
            if (count < 2)
                throw new FormatException("В файле меньше двух точек траектории.");
            if (count > MaxPoints)
                throw new FormatException($"Слишком много точек ({count}); допустимо не более {MaxPoints}.");

            var points = new PointState[count];
            var cells = new double[ExpectedColumns.Length];
            int i = 0;

            foreach (var row in pointsEl.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array || row.GetArrayLength() != cells.Length)
                    throw new FormatException($"Точка №{i + 1}: ожидалось {cells.Length} чисел.");

                int k = 0;
                foreach (var cell in row.EnumerateArray())
                {
                    if (cell.ValueKind != JsonValueKind.Number || !cell.TryGetDouble(out double value) || !double.IsFinite(value))
                        throw new FormatException($"Точка №{i + 1}: значение «{ExpectedColumns[k]}» не является конечным числом.");
                    cells[k++] = value;
                }

                if (i > 0 && cells[0] < points[i - 1].Time)
                    throw new FormatException($"Точка №{i + 1}: время убывает.");

                points[i++] = new PointState(cells[0], cells[1], cells[2], cells[3], cells[4], 0, 0);
            }

            return new ImportedExperiment(string.Empty, parameters, points);
        }
    }

    /// <summary>
    /// Чтение CSV, сохранённого <see cref="ExperimentExporter.ToCsv"/>: строки «# ключ: значение» с метаданными,
    /// строка заголовка и по одной строке на точку. Разделитель — запятая, десятичный — точка.
    /// </summary>
    public static ImportedExperiment ParseCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new FormatException("Файл пуст.");

        var meta = new Dictionary<string, string>(StringComparer.Ordinal);
        var points = new List<PointState>();
        const string header = "t_s,x_m,y_m,vx_m_s,vy_m_s,speed_m_s";
        bool headerSeen = false;
        int lineNo = 0;

        foreach (string raw in csv.Split('\n'))
        {
            lineNo++;
            string line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
                continue;

            if (line.StartsWith('#'))
            {
                int colon = line.IndexOf(':');
                if (colon > 0)
                    meta[line[1..colon].Trim()] = line[(colon + 1)..].Trim();
                continue;
            }

            if (!headerSeen)
            {
                if (line != header)
                    throw new FormatException($"Строка {lineNo}: ожидался заголовок «{header}».");
                headerSeen = true;
                continue;
            }

            if (points.Count >= MaxPoints)
                throw new FormatException($"Слишком много точек; допустимо не более {MaxPoints}.");

            string[] cells = line.Split(',');
            if (cells.Length != 6)
                throw new FormatException($"Строка {lineNo}: ожидалось 6 значений через запятую.");

            var v = new double[6];
            for (int k = 0; k < 6; k++)
            {
                if (!double.TryParse(cells[k].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[k])
                    || !double.IsFinite(v[k]))
                {
                    throw new FormatException($"Строка {lineNo}: «{cells[k].Trim()}» не является числом (нужна точка как разделитель).");
                }
            }

            if (points.Count > 0 && v[0] < points[^1].Time)
                throw new FormatException($"Строка {lineNo}: время убывает.");

            points.Add(new PointState(v[0], v[1], v[2], v[3], v[4], 0, 0));
        }

        if (!headerSeen)
            throw new FormatException($"Не найден заголовок «{header}».");
        if (!meta.TryGetValue("experiment", out string? name) || name != ExperimentExporter.ExperimentName)
            throw new FormatException($"Файл не от этого эксперимента (ожидалось «# experiment: {ExperimentExporter.ExperimentName}»).");
        if (points.Count < 2)
            throw new FormatException("В файле меньше двух точек траектории.");

        var parameters = new ImportedParameters(
            LinearVelocity: MetaNumber(meta, "vLinWheel_m_s"),
            Radius: MetaNumber(meta, "rWheel_m"),
            PointOffset: MetaNumber(meta, "rDot_m"),
            Omega: MetaNumber(meta, "omega_rad_s"),
            InitialPhase: MetaNumber(meta, "initialPhase_rad"),
            Step: MetaNumber(meta, "dt_s"),
            SimulationTime: MetaNumber(meta, "simulationTime_s"));

        return new ImportedExperiment(string.Empty, parameters, points);
    }

    private static double MetaNumber(Dictionary<string, string> meta, string key)
    {
        if (!meta.TryGetValue(key, out string? text)
            || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || !double.IsFinite(value))
        {
            throw new FormatException($"В метаданных «# {key}: …» нет числа.");
        }

        return value;
    }

    private static void CheckColumns(JsonElement root)
    {
        if (!root.TryGetProperty("columns", out var columns))
            return; // колонки необязательны, но если указаны, должны совпадать

        if (columns.ValueKind != JsonValueKind.Array || columns.GetArrayLength() != ExpectedColumns.Length)
            throw new FormatException("Неверный список колонок «columns».");

        int i = 0;
        foreach (var c in columns.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.String || c.GetString() != ExpectedColumns[i])
                throw new FormatException($"Колонка №{i + 1} должна называться «{ExpectedColumns[i]}».");
            i++;
        }
    }

    private static double Number(JsonElement obj, string property, string path)
    {
        if (!obj.TryGetProperty(property, out var el) || el.ValueKind != JsonValueKind.Number
            || !el.TryGetDouble(out double value) || !double.IsFinite(value))
        {
            throw new FormatException($"Поле «{path}» отсутствует или не является числом.");
        }

        return value;
    }
}
