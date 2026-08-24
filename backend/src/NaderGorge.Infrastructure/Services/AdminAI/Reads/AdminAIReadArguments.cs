using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

internal static class AdminAIReadArguments
{
    public static string RequireQuery(object input, int minLength = 2, int maxLength = 200)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty("query", out var queryArgument) ||
            queryArgument.ValueKind != JsonValueKind.String)
            throw new InvalidOperationException("A string query is required.");

        var query = NormalizeWhitespace(queryArgument.GetString() ?? string.Empty);
        if (query.Length < minLength || query.Length > maxLength)
            throw new InvalidOperationException("The search query is outside the allowed length.");
        return query;
    }

    public static Guid RequireGuid(object input, string propertyName)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty(propertyName, out var identifierArgument) ||
            identifierArgument.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(identifierArgument.GetString(), "D", out var id) ||
            id == Guid.Empty)
            throw new InvalidOperationException($"A valid {propertyName} is required.");
        return id;
    }

    public static Guid? OptionalGuid(object input, string propertyName)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty(propertyName, out var identifierArgument) ||
            identifierArgument.ValueKind == JsonValueKind.Null)
            return null;
        if (identifierArgument.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(identifierArgument.GetString(), "D", out var id) ||
            id == Guid.Empty)
            throw new InvalidOperationException($"A valid {propertyName} is required when supplied.");
        return id;
    }

    public static int RequireInt32(object input, string propertyName, int minimum, int maximum)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty(propertyName, out var integerArgument) ||
            integerArgument.ValueKind != JsonValueKind.Number ||
            !integerArgument.TryGetInt32(out var parsed) ||
            parsed < minimum || parsed > maximum)
            throw new InvalidOperationException($"{propertyName} is outside the allowed range.");
        return parsed;
    }

    public static IReadOnlySet<string> RequireStringSet(
        object input,
        string propertyName,
        IReadOnlySet<string> allowed,
        int minimumItems,
        int maximumItems)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty(propertyName, out var arrayArgument) ||
            arrayArgument.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"A {propertyName} array is required.");

        var selectedStrings = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stringArgument in arrayArgument.EnumerateArray())
        {
            if (stringArgument.ValueKind != JsonValueKind.String ||
                stringArgument.GetString() is not { } section ||
                !allowed.Contains(section) ||
                !selectedStrings.Add(section))
                throw new InvalidOperationException($"{propertyName} contains an invalid or duplicate value.");
        }

        if (selectedStrings.Count < minimumItems || selectedStrings.Count > maximumItems)
            throw new InvalidOperationException($"{propertyName} contains an unsafe number of values.");
        return selectedStrings;
    }

    public static JsonElement RequireObject(object input, string propertyName)
    {
        var arguments = ToObject(input);
        if (!arguments.TryGetProperty(propertyName, out var objectArgument) ||
            objectArgument.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"A {propertyName} object is required.");
        return objectArgument;
    }

    public static IReadOnlySet<string> RequireObjectKeys(
        object input,
        IReadOnlySet<string> allowed,
        int minimumItems,
        int maximumItems)
    {
        var arguments = ToObject(input);
        var selectedKeys = arguments.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        if (selectedKeys.Count < minimumItems || selectedKeys.Count > maximumItems)
            throw new InvalidOperationException("The selection contains an unsafe number of sections.");
        if (selectedKeys.Any(key => !allowed.Contains(key)))
            throw new InvalidOperationException("The selection contains an unsupported section.");
        return selectedKeys;
    }

    public static JsonElement GetSelectedObject(JsonElement selection, string propertyName)
    {
        if (!selection.TryGetProperty(propertyName, out var selectedObject) ||
            selectedObject.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"A {propertyName} selection object is required.");
        return selectedObject;
    }

    public static string NormalizeTeacherQuery(string query)
    {
        return NormalizeArabic(StripTeacherHonorific(query));
    }

    public static string StripTeacherHonorific(string query)
    {
        var normalizedWhitespace = NormalizeWhitespace(query);
        var words = normalizedWhitespace.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 2)
        {
            var normalizedHonorific = NormalizeArabic(words[0]);
            string[] honorifics = ["مستر", "استاذ", "الاستاذ", "دكتور", "الدكتور"];
            if (honorifics.Contains(normalizedHonorific, StringComparer.Ordinal))
                return words[1].Trim();
        }
        return normalizedWhitespace;
    }

    public static string NormalizeArabic(string inputText)
    {
        var normalized = NormalizeWhitespace(inputText.Normalize(NormalizationForm.FormKC)).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark || character == '\u0640')
                continue;
            builder.Append(character switch
            {
                '\u0622' or '\u0623' or '\u0625' => '\u0627',
                '\u0649' => '\u064a',
                _ => character
            });
        }
        return builder.ToString();
    }

    public static string SafeText(string? inputText, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(inputText)) return string.Empty;
        var builder = new StringBuilder(inputText.Length);
        foreach (var character in inputText.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsControl(character) && !char.IsWhiteSpace(character)) continue;
            builder.Append(character is '<' or '>' ? ' ' : character);
        }
        var sanitizedText = NormalizeWhitespace(builder.ToString())
            .Replace("javascript:", string.Empty, StringComparison.OrdinalIgnoreCase);
        return sanitizedText.Length <= maximumLength ? sanitizedText : sanitizedText[..maximumLength];
    }

    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return string.Empty;
        return $"***{digits[^Math.Min(4, digits.Length)..]}";
    }

    private static JsonElement ToObject(object input)
    {
        var arguments = input switch
        {
            JsonElement element => element,
            JsonDocument document => document.RootElement,
            _ => JsonSerializer.SerializeToElement(input)
        };
        if (arguments.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Read arguments must be an object.");
        return arguments;
    }

    private static string NormalizeWhitespace(string inputText) =>
        string.Join(' ', inputText.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
