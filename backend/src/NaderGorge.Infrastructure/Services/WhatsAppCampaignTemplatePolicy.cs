using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Services;

internal readonly record struct WhatsAppTemplateParameterKey(
    string ComponentType,
    int ComponentIndex,
    int Position,
    int? ButtonIndex = null);

internal sealed record WhatsAppTemplateParameterRequirement(
    WhatsAppTemplateParameterKey Key,
    string ProviderParameterType);

internal sealed record WhatsAppCampaignTemplateComponent(
    string Type,
    int ComponentIndex,
    string PreviewText,
    IReadOnlyList<WhatsAppTemplateParameterRequirement> Parameters,
    string? ButtonSubType = null,
    int? ButtonIndex = null,
    string? MediaFormat = null);

internal sealed record WhatsAppCampaignTemplate(
    IReadOnlyList<WhatsAppCampaignTemplateComponent> Components)
{
    public IReadOnlyList<WhatsAppTemplateParameterRequirement> Parameters =>
        Components.SelectMany(component => component.Parameters).ToArray();
}

internal sealed record WhatsAppCanonicalVariableMapping(
    WhatsAppTemplateParameterRequirement Requirement,
    WhatsAppCampaignVariableMappingDto Mapping);

internal static partial class WhatsAppCampaignTemplatePolicy
{
    private static readonly Regex Placeholder = new(
        @"\{\{\s*(?<position>\d+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AnyPlaceholder = new(
        @"\{\{.*?\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string Fingerprint(LiveSupportWhatsAppTemplate template) =>
        Fingerprint(template.MetaTemplateId, template.Name, template.Language, template.Category,
            template.Status, template.ComponentsJson);

    public static string Fingerprint(
        string metaId,
        string name,
        string language,
        string category,
        string status,
        string componentsJson)
    {
        using var document = JsonDocument.Parse(componentsJson);
        var canonicalComponents = CanonicalJson(document.RootElement);
        var payload = string.Join('\n', metaId.Trim(), name.Trim(), language.Trim(),
            category.Trim().ToUpperInvariant(), status.Trim().ToUpperInvariant(), canonicalComponents);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static WhatsAppCampaignTemplate RequireCampaignTemplate(LiveSupportWhatsAppTemplate template)
    {
        ValidateTemplateAuthority(template);
        try
        {
            using var document = JsonDocument.Parse(template.ComponentsJson);
            return ParseComponents(document.RootElement);
        }
        catch (JsonException)
        {
            throw InvalidTemplate("مكونات قالب واتساب غير صالحة.");
        }
    }

    public static IReadOnlyList<WhatsAppCanonicalVariableMapping> ValidateMappings(
        WhatsAppCampaignTemplate template,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings)
    {
        if (mappings is null) throw InvalidTemplate("تعيينات متغيرات القالب مطلوبة.");
        var supplied = new Dictionary<WhatsAppTemplateParameterKey, WhatsAppCampaignVariableMappingDto>();
        foreach (var mapping in mappings)
        {
            var requirement = MatchingRequirement(template, mapping);
            if (!supplied.TryAdd(requirement.Key, mapping))
                throw InvalidTemplate("لا يمكن تعيين متغير القالب نفسه أكثر من مرة.");
            if (requirement.Key.ComponentType == "BUTTON" &&
                !string.Equals(mapping.Source, "Literal", StringComparison.OrdinalIgnoreCase))
                throw InvalidTemplate("متغير زر الرابط يجب أن يكون قيمة ثابتة غير شخصية.");
        }
        if (supplied.Count != template.Parameters.Count ||
            template.Parameters.Any(requirement => !supplied.ContainsKey(requirement.Key)))
            throw InvalidTemplate("يجب تعيين قيمة لكل متغير مطلوب في القالب.");
        return template.Parameters.Select(requirement =>
            new WhatsAppCanonicalVariableMapping(requirement, supplied[requirement.Key])).ToArray();
    }

    public static IReadOnlyList<WhatsAppCloudService.TemplateComponent> ProviderComponents(
        WhatsAppCampaignTemplate template,
        IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolvedParameters,
        string? headerMediaId = null)
    {
        if (template.Components.Any(component => component.MediaFormat is not null) &&
            (string.IsNullOrWhiteSpace(headerMediaId) || headerMediaId.Length > 200 || headerMediaId.Any(char.IsControl)))
            throw InvalidTemplate("ارفع صورة رأس القالب قبل المعاينة.");
        return template.Components
            .Where(component => component.Parameters.Count > 0 || component.MediaFormat is not null)
            .Select(component => component.MediaFormat is not null
                ? new WhatsAppCloudService.TemplateComponent("HEADER", [headerMediaId!], component.MediaFormat.ToLowerInvariant())
                : ProviderComponent(component, resolvedParameters)).ToArray();
    }

    public static string RenderPreview(
        WhatsAppCampaignTemplate template,
        IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolvedParameters) =>
        string.Join("\n", template.Components
            .Select(component => RenderComponent(component, resolvedParameters))
            .Where(text => !string.IsNullOrWhiteSpace(text)));

    private static void ValidateTemplateAuthority(LiveSupportWhatsAppTemplate template)
    {
        if (template.Fingerprint.Length != 64 ||
            template.Fingerprint.Any(character => !char.IsAsciiHexDigit(character)))
            throw InvalidTemplate("يجب مزامنة قالب واتساب بنجاح قبل استخدامه في حملة.");
        if (!string.Equals(template.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
            throw InvalidTemplate("لا يمكن استخدام قالب غير معتمد في حملة واتساب.");
        if (string.Equals(template.Category, "AUTHENTICATION", StringComparison.OrdinalIgnoreCase))
            throw InvalidTemplate("قوالب التحقق لا تُستخدم في الحملات الجماعية.");
        if (!string.Equals(template.Category, "MARKETING", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(template.Category, "UTILITY", StringComparison.OrdinalIgnoreCase))
            throw InvalidTemplate("فئة قالب واتساب غير مدعومة في الحملات.");
    }

    private static WhatsAppCampaignTemplate ParseComponents(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw InvalidTemplate("مكونات قالب واتساب غير صالحة.");
        var components = new List<WhatsAppCampaignTemplateComponent>();
        var seenTypes = new HashSet<string>(StringComparer.Ordinal);
        var componentIndex = 0;
        foreach (var component in root.EnumerateArray())
        {
            var type = RequiredString(component, "type").ToUpperInvariant();
            if (!seenTypes.Add(type))
                throw InvalidTemplate("يجب أن يحتوي القالب على مكوّن واحد من كل نوع.");
            components.AddRange(ParseComponent(component, type, componentIndex));
            componentIndex++;
        }
        if (components.All(component => component.Type != "BODY"))
            throw InvalidTemplate("قالب الحملة يجب أن يحتوي على نص الرسالة.");
        return new WhatsAppCampaignTemplate(components);
    }

    private static IReadOnlyList<WhatsAppCampaignTemplateComponent> ParseComponent(
        JsonElement component,
        string type,
        int componentIndex) => type switch
    {
        "HEADER" => [ParseHeader(component, componentIndex)],
        "BODY" => [ParseText(component, type, componentIndex)],
        "FOOTER" => [ParseFooter(component, componentIndex)],
        "BUTTONS" => ParseButtons(component, componentIndex),
        _ => throw InvalidTemplate("يحتوي القالب على مكوّن غير مدعوم بأمان في الحملات.")
    };

    private static WhatsAppCampaignTemplateComponent ParseHeader(
        JsonElement component,
        int componentIndex)
    {
        var format = (OptionalString(component, "format") ?? "TEXT").ToUpperInvariant();
        if (format == "IMAGE")
            return new WhatsAppCampaignTemplateComponent("HEADER", componentIndex, "صورة رأس الرسالة", [], MediaFormat: "IMAGE");
        if (format != "TEXT")
            throw InvalidTemplate("الحملات تدعم رأس القالب النصي أو صورة فقط.");
        return ParseText(component, "HEADER", componentIndex);
    }

    private static WhatsAppCampaignTemplateComponent ParseText(
        JsonElement component,
        string type,
        int componentIndex)
    {
        var text = RequiredString(component, "text");
        var requirements = RequiredPositions(text).Select(position =>
            new WhatsAppTemplateParameterRequirement(
                new WhatsAppTemplateParameterKey(type, componentIndex, position), "text")).ToArray();
        return new WhatsAppCampaignTemplateComponent(type, componentIndex, text, requirements);
    }

    private static WhatsAppCampaignTemplateComponent ParseFooter(
        JsonElement component,
        int componentIndex)
    {
        var footer = RequiredString(component, "text");
        if (AnyPlaceholder.IsMatch(footer))
            throw InvalidTemplate("تذييل القالب يجب أن يكون نصًا ثابتًا بلا متغيرات.");
        return new WhatsAppCampaignTemplateComponent("FOOTER", componentIndex, footer, []);
    }

    private static IReadOnlyList<WhatsAppCampaignTemplateComponent> ParseButtons(
        JsonElement component,
        int componentIndex)
    {
        if (!component.TryGetProperty("buttons", out var buttons) ||
            buttons.ValueKind != JsonValueKind.Array || buttons.GetArrayLength() == 0)
            throw InvalidTemplate("أزرار قالب واتساب غير صالحة.");
        return buttons.EnumerateArray().Select((button, buttonIndex) =>
            ParseButton(button, componentIndex, buttonIndex)).ToArray();
    }

    private static WhatsAppCampaignTemplateComponent ParseButton(
        JsonElement button,
        int componentIndex,
        int buttonIndex)
    {
        var type = RequiredString(button, "type").ToUpperInvariant();
        var label = RequiredString(button, "text");
        if (AnyPlaceholder.IsMatch(label))
            throw InvalidTemplate("نص زر القالب يجب أن يكون ثابتًا.");
        return type switch
        {
            "URL" => ParseUrlButton(button, componentIndex, buttonIndex, label),
            "PHONE_NUMBER" => ParsePhoneButton(button, componentIndex, buttonIndex, label),
            _ => throw InvalidTemplate("نوع زر قالب واتساب غير مدعوم بأمان في الحملات.")
        };
    }

    private static WhatsAppCampaignTemplateComponent ParseUrlButton(
        JsonElement button,
        int componentIndex,
        int buttonIndex,
        string label)
    {
        var url = RequiredString(button, "url");
        var positions = RequiredPositions(url);
        ValidateUrlPositions(url, positions);
        var validationUrl = positions.Count == 0 ? url : Placeholder.Replace(url, "sample");
        if (!WhatsAppCloudService.IsSafeTemplateUrl(validationUrl))
            throw InvalidTemplate("رابط زر قالب واتساب غير صالح أو غير آمن.");
        var requirements = UrlRequirements(componentIndex, buttonIndex, positions.Count > 0);
        return new WhatsAppCampaignTemplateComponent(
            "BUTTON", componentIndex, $"زر رابط: {label} — {url}", requirements, "url", buttonIndex);
    }

    private static void ValidateUrlPositions(string url, IReadOnlyList<int> positions)
    {
        if (positions.Count > 1 || positions.Count == 1 && positions[0] != 1)
            throw InvalidTemplate("زر الرابط الديناميكي يدعم متغيرًا واحدًا مرقمًا {{1}}.");
        if (positions.Count == 0) return;
        var match = Placeholder.Match(url);
        if (!match.Success || match.Index + match.Length != url.Length)
            throw InvalidTemplate("متغير زر الرابط يجب أن يكون في نهاية الرابط.");
    }

    private static IReadOnlyList<WhatsAppTemplateParameterRequirement> UrlRequirements(
        int componentIndex,
        int buttonIndex,
        bool isDynamic) => isDynamic
        ? [new WhatsAppTemplateParameterRequirement(
            new WhatsAppTemplateParameterKey("BUTTON", componentIndex, 1, buttonIndex), "text")]
        : [];

    private static WhatsAppCampaignTemplateComponent ParsePhoneButton(
        JsonElement button,
        int componentIndex,
        int buttonIndex,
        string label)
    {
        var phoneNumber = RequiredString(button, "phone_number");
        if (AnyPlaceholder.IsMatch(phoneNumber))
            throw InvalidTemplate("رقم زر الاتصال يجب أن يكون ثابتًا.");
        return new WhatsAppCampaignTemplateComponent(
            "BUTTON", componentIndex, $"زر اتصال: {label} — {phoneNumber}", [], "phone_number", buttonIndex);
    }

    private static IReadOnlyList<int> RequiredPositions(string text)
    {
        var matches = Placeholder.Matches(text);
        if (AnyPlaceholder.Matches(text).Count != matches.Count)
            throw InvalidTemplate("متغيرات القالب المسماة غير مدعومة؛ يلزم ترقيمها.");
        var positions = new SortedSet<int>();
        foreach (Match match in matches)
        {
            if (!int.TryParse(match.Groups["position"].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out var position) || position < 1)
                throw InvalidTemplate("ترقيم متغيرات القالب غير صالح.");
            positions.Add(position);
        }
        if (positions.Where((position, index) => position != index + 1).Any())
            throw InvalidTemplate("ترقيم متغيرات القالب يجب أن يبدأ من 1 دون فجوات.");
        return positions.ToArray();
    }

    private static WhatsAppTemplateParameterRequirement MatchingRequirement(
        WhatsAppCampaignTemplate template,
        WhatsAppCampaignVariableMappingDto mapping)
    {
        if (mapping is null || string.IsNullOrWhiteSpace(mapping.ComponentType) ||
            string.IsNullOrWhiteSpace(mapping.Source) || mapping.Position < 1)
            throw InvalidTemplate("تعيين متغيرات القالب غير صالح.");
        var componentType = mapping.ComponentType.Trim().ToUpperInvariant();
        if (componentType == "BUTTON" && (!mapping.ComponentIndex.HasValue || !mapping.ButtonIndex.HasValue) ||
            componentType != "BUTTON" && mapping.ButtonIndex.HasValue ||
            mapping.ComponentIndex is < 0 || mapping.ButtonIndex is < 0)
            throw InvalidTemplate("هوية مكوّن متغير القالب غير صالحة.");
        var candidates = template.Parameters.Where(requirement =>
            requirement.Key.ComponentType == componentType &&
            requirement.Key.Position == mapping.Position &&
            (!mapping.ComponentIndex.HasValue || requirement.Key.ComponentIndex == mapping.ComponentIndex) &&
            (!mapping.ButtonIndex.HasValue || requirement.Key.ButtonIndex == mapping.ButtonIndex)).ToArray();
        return candidates.Length == 1
            ? candidates[0]
            : throw InvalidTemplate("تعيين متغيرات القالب غير مطابق للمتغيرات المعتمدة.");
    }

    private static WhatsAppCloudService.TemplateComponent ProviderComponent(
        WhatsAppCampaignTemplateComponent component,
        IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolvedParameters) =>
        new(component.Type,
            component.Parameters.Select(parameter => resolvedParameters[parameter.Key]).ToArray(),
            component.Parameters[0].ProviderParameterType,
            component.ButtonSubType,
            component.ButtonIndex);

    private static string RenderComponent(
        WhatsAppCampaignTemplateComponent component,
        IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> resolvedParameters) =>
        Placeholder.Replace(component.PreviewText, match => resolvedParameters[new WhatsAppTemplateParameterKey(
            component.Type,
            component.ComponentIndex,
            int.Parse(match.Groups["position"].Value, CultureInfo.InvariantCulture),
            component.ButtonIndex)]);

    private static string CanonicalJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static string RequiredString(JsonElement element, string property) =>
        OptionalString(element, property) is { Length: > 0 } value
            ? value
            : throw InvalidTemplate("مكونات قالب واتساب غير صالحة.");

    private static string? OptionalString(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static WhatsAppCampaignException InvalidTemplate(string message) =>
        new(WhatsAppCampaignErrorCodes.TemplateInvalid, message, 422);
}
