using System.Globalization;

namespace Cycloids.ViewModel.Infrastructure;

/// <summary>Форматирование чисел для интерфейса: десятичная запятая, как принято в русскоязычных подписях.</summary>
public static class RuFormat
{
    public static CultureInfo Culture { get; } = Create();

    private static CultureInfo Create()
    {
        try
        {
            return CultureInfo.GetCultureInfo("ru-RU");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    public static string N(double value, int decimals = 2)
        => double.IsFinite(value) ? value.ToString("F" + decimals, Culture) : "—";

    public static string Sci(double value)
        => double.IsFinite(value) ? value.ToString("0.00E+00", Culture) : "—";
}
