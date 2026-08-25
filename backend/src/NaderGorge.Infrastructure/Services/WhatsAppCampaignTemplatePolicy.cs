using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Services;

internal sealed record WhatsAppTextTemplateComponent(
    string Type,
    string Text,
    IReadOnlyList<int> RequiredPositions);

internal sealed record WhatsAppTextTemplate(
    IReadOnlyList<WhatsAppTextTemplateComponent> Components);

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

    public static WhatsAppTextTemplate RequireCampaignTemplate(LiveSupportWhatsAppTemplate template)
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

        JsonDocument document;
        try { document = JsonDocument.Parse(template.ComponentsJson); }
        catch (JsonException) { throw InvalidTemplate("مكونات قالب واتساب غير صالحة."); }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw InvalidTemplate("مكونات قالب واتساب غير صالحة.");
            var textComponents = new List<WhatsAppTextTemplateComponent>();
            var seenTypes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var component in document.RootElement.EnumerateArray())
            {
                var type = RequiredString(component, "type").ToUpperInvariant();
                if (type == "FOOTER")
                {
                    var footer = OptionalString(component, "text") ?? string.Empty;
                    if (AnyPlaceholder.IsMatch(footer))
                        throw InvalidTemplate("متغيرات تذييل القالب غير مدعومة في الحملات.");
                    textComponents.Add(new WhatsAppTextTemplateComponent(type, footer, []));
                    continue;
                }
                if (type is not ("HEADER" or "BODY"))
                    throw InvalidTemplate("يدعم مركز الحملات قوالب النص فقط دون وسائط أو أزرار.");
                if (!seenTypes.Add(type))
                    throw InvalidTemplate("يجب أن يحتوي القالب على مكوّن نص واحد من كل نوع.");
                if (type == "HEADER")
                {
                    var format = OptionalString(component, "format") ?? "TEXT";
                    if (!string.Equals(format, "TEXT", StringComparison.OrdinalIgnoreCase))
                        throw InvalidTemplate("رأس القالب غير النصي غير مدعوم في الحملات.");
                }

                var text = OptionalString(component, "text") ?? string.Empty;
                var matches = Placeholder.Matches(text);
                if (AnyPlaceholder.Matches(text).Count != matches.Count)
                    throw InvalidTemplate("متغيرات القالب المسماة غير مدعومة؛ يلزم ترقيمها.");
                var positions = matches.Select(match => int.Parse(match.Groups["position"].Value))
                    .Distinct().OrderBy(position => position).ToArray();
                if (positions.Length > 0 && !positions.SequenceEqual(Enumerable.Range(1, positions[^1])))
                    throw InvalidTemplate("ترقيم متغيرات القالب يجب أن يبدأ من 1 دون فجوات.");
                textComponents.Add(new WhatsAppTextTemplateComponent(type, text, positions));
            }
            if (textComponents.All(component => component.Type != "BODY"))
                throw InvalidTemplate("قالب الحملة يجب أن يحتوي على نص الرسالة.");
            return new WhatsAppTextTemplate(textComponents);
        }
    }

    public static IReadOnlyList<WhatsAppCloudService.TemplateComponent> ProviderComponents(
        WhatsAppTextTemplate template,
        IReadOnlyDictionary<(string Type, int Position), string> values) =>
        template.Components
            .Where(component => component.RequiredPositions.Count > 0)
            .Select(component => new WhatsAppCloudService.TemplateComponent(
                component.Type,
                component.RequiredPositions.Select(position => values[(component.Type, position)]).ToArray()))
            .ToArray();

    public static string RenderPreview(
        WhatsAppTextTemplate template,
        IReadOnlyDictionary<(string Type, int Position), string> values) =>
        string.Join("\n", template.Components.Select(component => Placeholder.Replace(
            component.Text,
            match => values[(component.Type, int.Parse(match.Groups["position"].Value))])));

    public static void ValidateMappings(
        WhatsAppTextTemplate template,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings)
    {
        if (mappings is null) throw InvalidTemplate("تعيينات متغيرات القالب مطلوبة.");
        var required = template.Components
            .SelectMany(component => component.RequiredPositions.Select(position => (component.Type, position)))
            .ToHashSet();
        var supplied = new HashSet<(string Type, int Position)>();
        foreach (var mapping in mappings)
        {
            if (mapping is null || string.IsNullOrWhiteSpace(mapping.ComponentType) ||
                string.IsNullOrWhiteSpace(mapping.Source))
                throw InvalidTemplate("تعيين متغيرات القالب غير صالح.");
            var key = (mapping.ComponentType.Trim().ToUpperInvariant(), mapping.Position);
            if (!required.Contains(key) || !supplied.Add(key))
                throw InvalidTemplate("تعيين متغيرات القالب غير مطابق للمتغيرات المعتمدة.");
        }
        if (!supplied.SetEquals(required))
            throw InvalidTemplate("يجب تعيين قيمة لكل متغير مطلوب في القالب.");
    }

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
