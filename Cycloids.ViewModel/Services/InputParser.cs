using System.Globalization;

namespace Cycloids.ViewModel.Services;

/// <summary>Разбор чисел из текстовых полей. Принимает и точку, и запятую как десятичный разделитель.</summary>
public static class InputParser
{
    public static bool TryParse(string? text, string fieldName, ICollection<string> errors, out double value)
    {
        value = double.NaN;

        if (string.IsNullOrWhiteSpace(text))
        {
            errors.Add($"Поле «{fieldName}» не заполнено.");
            return false;
        }

        string normalized = text.Trim().Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            || !double.IsFinite(parsed))
        {
            errors.Add($"Поле «{fieldName}» должно быть числом.");
            return false;
        }

        value = parsed;
        return true;
    }
}
