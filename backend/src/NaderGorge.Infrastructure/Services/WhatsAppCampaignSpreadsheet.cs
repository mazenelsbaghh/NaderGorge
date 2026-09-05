using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities.LiveSupport;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    private async Task<AudienceBuildResult> BuildRequestedAudienceAsync(
        LiveSupportWhatsAppTemplate template,
        WhatsAppCampaignAudienceFilterDto filters,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        IReadOnlyList<WhatsAppCampaignSpreadsheetRowDto>? spreadsheetRows,
        CancellationToken ct)
    {
        return spreadsheetRows is { Count: > 0 }
            ? await BuildSpreadsheetAudienceAsync(template, mappings, spreadsheetRows, ct)
            : await BuildAudienceAsync(template, filters, mappings, ct);
    }

    private async Task<AudienceBuildResult> BuildSpreadsheetAudienceAsync(
        LiveSupportWhatsAppTemplate template,
        IReadOnlyList<WhatsAppCampaignVariableMappingDto> mappings,
        IReadOnlyList<WhatsAppCampaignSpreadsheetRowDto> rows,
        CancellationToken ct)
    {
        if (rows.Count > MaximumAudienceRows)
            throw Invalid($"ملف الجمهور يتجاوز الحد الأقصى ({MaximumAudienceRows} صف). ");

        var campaignTemplate = WhatsAppCampaignTemplatePolicy.RequireCampaignTemplate(template);
        var canonicalMappings = WhatsAppCampaignTemplatePolicy.ValidateMappings(campaignTemplate, mappings);
        ValidateSpreadsheetMappings(canonicalMappings);
        var exclusions = NewExclusionCounts();
        var normalizedRows = NormalizeSpreadsheetRows(rows, exclusions);
        var preferences = await SpreadsheetPreferencesAsync(normalizedRows, ct);
        var sendable = ResolveSpreadsheetRecipients(
            campaignTemplate, canonicalMappings, normalizedRows, preferences, template.Category, exclusions);
        var recipients = DeduplicateSpreadsheetRecipients(sendable, exclusions);
        var fingerprint = SpreadsheetAudienceFingerprint(template, canonicalMappings, recipients);
        return new AudienceBuildResult(recipients, exclusions, fingerprint);
    }

    private static void ValidateSpreadsheetMappings(
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings)
    {
        foreach (var entry in mappings)
        {
            var mapping = entry.Mapping;
            if (string.Equals(mapping.Source, "Literal", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(mapping.LiteralValue) || !string.IsNullOrWhiteSpace(mapping.ColumnName))
                    throw Invalid("القيمة الثابتة لمتغير القالب غير صالحة.");
                continue;
            }
            if (!string.Equals(mapping.Source, "SpreadsheetColumn", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(mapping.ColumnName) || mapping.ColumnName.Trim().Length > 128 ||
                mapping.LiteralValue is not null || mapping.ReferenceId.HasValue)
                throw Invalid("اربط كل متغير بعمود صالح من ملف الجمهور أو بقيمة ثابتة.");
        }
    }

    private List<SpreadsheetContact> NormalizeSpreadsheetRows(
        IReadOnlyList<WhatsAppCampaignSpreadsheetRowDto> rows,
        Dictionary<string, int> exclusions)
    {
        var normalizedRows = new List<SpreadsheetContact>(rows.Count);
        foreach (var row in rows)
        {
            if (row.RowNumber < 2) throw Invalid("رقم صف الشيت غير صالح.");
            var e164 = NormalizeE164(row.Phone);
            if (e164 is null)
            {
                Increment(exclusions, "invalid_phone");
                continue;
            }
            var columns = NormalizeSpreadsheetColumns(row.Columns);
            normalizedRows.Add(new SpreadsheetContact(
                row.RowNumber, e164, _protector.DestinationHash(e164), columns));
        }
        return normalizedRows;
    }

    private static IReadOnlyDictionary<string, string> NormalizeSpreadsheetColumns(
        IReadOnlyDictionary<string, string> columns)
    {
        if (columns is null || columns.Count is 0 or > MaximumSpreadsheetColumns)
            throw Invalid("أعمدة أحد صفوف الشيت غير صالحة.");
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in columns)
        {
            var name = column.Key.Normalize().Trim();
            if (name.Length is 0 or > 128 || !normalized.TryAdd(name, BoundedCell(column.Value ?? string.Empty)))
                throw Invalid("عناوين أعمدة الشيت فارغة أو مكررة أو طويلة.");
        }
        return normalized;
    }

    private async Task<IReadOnlyDictionary<string, WhatsAppContactPreference[]>> SpreadsheetPreferencesAsync(
        IReadOnlyList<SpreadsheetContact> rows,
        CancellationToken ct)
    {
        var hashes = rows.Select(row => row.DestinationHash).Distinct().ToArray();
        if (hashes.Length == 0) return new Dictionary<string, WhatsAppContactPreference[]>();
        var preferences = await _db.WhatsAppContactPreferences.AsNoTracking()
            .Where(preference => hashes.Contains(preference.DestinationHash) &&
                preference.EffectiveAt <= DateTime.UtcNow)
            .ToListAsync(ct);
        return preferences.GroupBy(preference => preference.DestinationHash)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
    }

    private static List<ResolvedAudienceRecipient> ResolveSpreadsheetRecipients(
        WhatsAppCampaignTemplate template,
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings,
        IReadOnlyList<SpreadsheetContact> rows,
        IReadOnlyDictionary<string, WhatsAppContactPreference[]> preferences,
        string templateCategory,
        Dictionary<string, int> exclusions)
    {
        var recipients = new List<ResolvedAudienceRecipient>(rows.Count);
        foreach (var row in rows)
        {
            if (!DestinationAllowsCampaign(
                    preferences.GetValueOrDefault(row.DestinationHash) ?? [], templateCategory))
            {
                Increment(exclusions, "opted_out");
                continue;
            }
            try
            {
                var parameters = ResolveSpreadsheetValues(row.Columns, mappings);
                var components = WhatsAppCampaignTemplatePolicy.ProviderComponents(template, parameters);
                recipients.Add(new ResolvedAudienceRecipient(
                    null, $"صف {row.RowNumber}", "Spreadsheet", row.DestinationHash, row.Phone[^4..],
                    SerializeFrozenRecipientPayload(new FrozenRecipientPayload(row.Phone, components)),
                    WhatsAppCampaignTemplatePolicy.RenderPreview(template, parameters)));
            }
            catch (MissingCampaignVariableException)
            {
                Increment(exclusions, "missing_variable");
            }
        }
        return recipients;
    }

    private static IReadOnlyDictionary<WhatsAppTemplateParameterKey, string> ResolveSpreadsheetValues(
        IReadOnlyDictionary<string, string> columns,
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings)
    {
        var parameters = new Dictionary<WhatsAppTemplateParameterKey, string>();
        foreach (var entry in mappings)
        {
            var mapping = entry.Mapping;
            var rawValue = string.Equals(mapping.Source, "Literal", StringComparison.OrdinalIgnoreCase)
                ? mapping.LiteralValue
                : columns.GetValueOrDefault(mapping.ColumnName!.Normalize().Trim());
            parameters[entry.Requirement.Key] = SafeVariable(rawValue);
        }
        return parameters;
    }

    private static IReadOnlyList<ResolvedAudienceRecipient> DeduplicateSpreadsheetRecipients(
        IReadOnlyList<ResolvedAudienceRecipient> recipients,
        Dictionary<string, int> exclusions)
    {
        var deduplicated = new List<ResolvedAudienceRecipient>();
        foreach (var group in recipients.GroupBy(recipient => recipient.DestinationHash)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var candidates = group.OrderBy(recipient => recipient.StudentName, StringComparer.Ordinal).ToArray();
            if (candidates.Select(recipient => recipient.PayloadJson).Distinct(StringComparer.Ordinal).Take(2).Count() > 1)
            {
                Add(exclusions, "ambiguous_personalization", candidates.Length);
                continue;
            }
            deduplicated.Add(candidates[0]);
            Add(exclusions, "duplicate_collapsed", candidates.Length - 1);
        }
        return deduplicated;
    }

    private static string SpreadsheetAudienceFingerprint(
        LiveSupportWhatsAppTemplate template,
        IReadOnlyList<WhatsAppCanonicalVariableMapping> mappings,
        IReadOnlyList<ResolvedAudienceRecipient> recipients)
    {
        return HashJson(new
        {
            source = "spreadsheet",
            template = template.Fingerprint,
            mappings = mappings.Select(entry => entry.Mapping),
            recipients = recipients.OrderBy(recipient => recipient.DestinationHash, StringComparer.Ordinal)
                .Select(recipient => new { recipient.DestinationHash, payload = HashText(recipient.PayloadJson) })
        });
    }

    private sealed record SpreadsheetContact(
        int RowNumber,
        string Phone,
        string DestinationHash,
        IReadOnlyDictionary<string, string> Columns);
}
