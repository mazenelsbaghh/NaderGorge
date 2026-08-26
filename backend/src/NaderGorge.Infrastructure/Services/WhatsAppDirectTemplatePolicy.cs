using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Services;

public sealed record WhatsAppDirectTemplateValidation(
    IReadOnlyList<WhatsAppCloudService.TemplateComponent> ProviderComponents,
    string Preview);

public sealed record WhatsAppDirectTemplateParameterSnapshot(
    string? Fingerprint,
    IReadOnlyList<string> Parameters);

public static class WhatsAppDirectTemplatePolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex TemplatePlaceholder = new(
        @"\{\{(?<position>\d+)\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AnyTemplatePlaceholder = new(
        @"\{\{.*?\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private sealed record TextComponentParse(
        WhatsAppCloudService.TemplateComponent? ProviderComponent,
        int ConsumedParameters,
        string Preview);

    public static WhatsAppDirectTemplateValidation? Validate(
        LiveSupportWhatsAppTemplate? template,
        IReadOnlyList<string>? parameters)
    {
        if (template is null ||
            !string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(template.ComponentsJson) ||
            !IsValidFingerprint(template.Fingerprint) ||
            parameters is null ||
            parameters.Count > 30 || parameters.Any(parameter =>
                string.IsNullOrWhiteSpace(parameter) || parameter.Length > 1_000 ||
                parameter.Any(char.IsControl)))
            return null;
        try
        {
            using var document = JsonDocument.Parse(template.ComponentsJson);
            return Validate(document.RootElement, parameters);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string SerializeParameterSnapshot(
        string fingerprint,
        IReadOnlyList<string> parameters) =>
        JsonSerializer.Serialize(
            new WhatsAppDirectTemplateParameterSnapshot(fingerprint, parameters),
            JsonOptions);

    public static WhatsAppDirectTemplateParameterSnapshot? DeserializeParameterSnapshot(
        string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var legacyParameters = JsonSerializer.Deserialize<List<string>>(payloadJson, JsonOptions);
                return legacyParameters is null
                    ? null
                    : new WhatsAppDirectTemplateParameterSnapshot(null, legacyParameters);
            }
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            var snapshot = JsonSerializer.Deserialize<WhatsAppDirectTemplateParameterSnapshot>(
                payloadJson,
                JsonOptions);
            return snapshot is not null && IsValidFingerprint(snapshot.Fingerprint) &&
                snapshot.Parameters is not null
                    ? snapshot
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WhatsAppDirectTemplateValidation? Validate(
        JsonElement root,
        IReadOnlyList<string> parameters)
    {
        if (root.ValueKind != JsonValueKind.Array) return null;
        var providerComponents = new List<WhatsAppCloudService.TemplateComponent>();
        var previewParts = new List<string>();
        var componentTypes = new HashSet<string>(StringComparer.Ordinal);
        var parameterIndex = 0;
        var hasBody = false;
        foreach (var component in root.EnumerateArray())
        {
            var type = StringProperty(component, "type")?.ToUpperInvariant();
            if (type is null || !componentTypes.Add(type)) return null;
            if (type == "FOOTER")
            {
                var footer = StaticText(component);
                if (footer is null) return null;
                previewParts.Add(footer);
                continue;
            }
            if (type == "BUTTONS")
            {
                if (!StaticButtonsAreValid(component)) return null;
                continue;
            }

            var parsed = ParseTextComponent(component, type, parameters, parameterIndex);
            if (parsed is null) return null;
            hasBody |= type == "BODY";
            if (parsed.ProviderComponent is not null)
                providerComponents.Add(parsed.ProviderComponent);
            previewParts.Add(parsed.Preview);
            parameterIndex += parsed.ConsumedParameters;
        }

        return hasBody && parameterIndex == parameters.Count
            ? new WhatsAppDirectTemplateValidation(
                providerComponents,
                string.Join('\n', previewParts).Trim())
            : null;
    }

    private static TextComponentParse? ParseTextComponent(
        JsonElement component,
        string type,
        IReadOnlyList<string> parameters,
        int parameterIndex)
    {
        if (type is not ("HEADER" or "BODY")) return null;
        if (type == "HEADER" &&
            !string.Equals(StringProperty(component, "format") ?? "TEXT", "TEXT",
                StringComparison.OrdinalIgnoreCase))
            return null;
        var text = StringProperty(component, "text");
        var parameterCount = text is null ? null : TemplateParameterCount(text);
        if (!parameterCount.HasValue || parameterIndex + parameterCount > parameters.Count)
            return null;
        var componentParameters = parameters
            .Skip(parameterIndex)
            .Take(parameterCount.Value)
            .ToArray();
        var preview = TemplatePlaceholder.Replace(text!, match =>
            componentParameters[int.Parse(
                match.Groups["position"].Value,
                CultureInfo.InvariantCulture) - 1]);
        var providerComponent = parameterCount == 0
            ? null
            : new WhatsAppCloudService.TemplateComponent(type, componentParameters);
        return new TextComponentParse(providerComponent, parameterCount.Value, preview);
    }

    private static int? TemplateParameterCount(string text)
    {
        var placeholders = AnyTemplatePlaceholder.Matches(text);
        var matches = TemplatePlaceholder.Matches(text);
        if (placeholders.Count != matches.Count || ContainsUnmatchedTemplateBraces(text))
            return null;
        for (var index = 0; index < matches.Count; index++)
        {
            if (placeholders[index].Index != matches[index].Index ||
                placeholders[index].Length != matches[index].Length)
                return null;
        }

        var positions = new SortedSet<int>();
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups["position"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var position) ||
                position < 1 || !positions.Add(position))
                return null;
        }
        return positions.Where((position, index) => position != index + 1).Any()
            ? null
            : positions.Count;
    }

    private static string? StaticText(JsonElement component)
    {
        var text = StringProperty(component, "text");
        return text is not null && !ContainsTemplateSyntax(text) ? text : null;
    }

    private static bool StaticButtonsAreValid(JsonElement component)
    {
        if (!component.TryGetProperty("buttons", out var buttons) ||
            buttons.ValueKind != JsonValueKind.Array || buttons.GetArrayLength() == 0)
            return false;
        return buttons.EnumerateArray().All(StaticButtonIsValid);
    }

    private static bool StaticButtonIsValid(JsonElement button)
    {
        var type = StringProperty(button, "type")?.ToUpperInvariant();
        var label = StringProperty(button, "text");
        if (label is null || ContainsTemplateSyntax(label)) return false;
        if (type == "URL")
        {
            var url = StringProperty(button, "url");
            return url is not null && !ContainsTemplateSyntax(url) &&
                WhatsAppCloudService.IsSafeTemplateUrl(url);
        }
        if (type == "PHONE_NUMBER")
        {
            var phoneNumber = StringProperty(button, "phone_number");
            return phoneNumber is not null && !ContainsTemplateSyntax(phoneNumber);
        }
        return false;
    }

    private static bool ContainsTemplateSyntax(string text) =>
        AnyTemplatePlaceholder.IsMatch(text) || ContainsUnmatchedTemplateBraces(text);

    private static bool ContainsUnmatchedTemplateBraces(string text)
    {
        var withoutPlaceholders = AnyTemplatePlaceholder.Replace(text, string.Empty);
        return withoutPlaceholders.Contains("{{", StringComparison.Ordinal) ||
            withoutPlaceholders.Contains("}}", StringComparison.Ordinal);
    }

    private static string? StringProperty(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var stringProperty) &&
        stringProperty.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(stringProperty.GetString())
            ? stringProperty.GetString()!.Trim()
            : null;

    private static bool IsValidFingerprint(string? fingerprint) =>
        fingerprint is { Length: 64 } &&
        fingerprint.All(char.IsAsciiHexDigit);
}
