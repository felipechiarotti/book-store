using System.Text.RegularExpressions;

namespace BookStore.Domain.Rules;

public static partial class IsbnRules
{
    [GeneratedRegex(@"\A(?:[0-9]{9}[0-9X]|[0-9]{13})\z")]
    private static partial Regex FormatRegex { get; }

    public static string Normalize(string? value)
    {
        return string.Concat(
                (value ?? string.Empty)
                .Where(character =>
                    !char.IsWhiteSpace(character) && character != '-'))
            .ToUpperInvariant();
    }

    public static bool HasValidFormat(string? value)
    {
        return FormatRegex.IsMatch(Normalize(value));
    }
}