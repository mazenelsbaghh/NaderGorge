using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed class AdminAICommercialPreviewSource(IAppDbContext db) : IAdminAIActionPreviewSource
{
    public Task<AdminAIActionPreview> PreviewAsync<TInput>(
        string capabilityKey, Guid actorId, TInput input, CancellationToken ct) where TInput : class =>
        input switch
        {
            AdminAICreateFormInput create when capabilityKey == "admin.commercial.form.create" =>
                PreviewCreateAsync(actorId, create, ct),
            AdminAIUpdateFormInput update when capabilityKey == "admin.commercial.form.update" =>
                PreviewUpdateAsync(actorId, update, ct),
            _ => throw new NotSupportedException("Admin AI commercial preview capability is unavailable.")
        };

    private async Task<AdminAIActionPreview> PreviewCreateAsync(
        Guid actorId, AdminAICreateFormInput input, CancellationToken ct)
    {
        var fieldCount = ValidateDraft(input.Title, input.Slug, input.IsActive, input.FieldsJson);
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId, ct))
            throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");
        var slug = input.Slug.ToLowerInvariant();
        if (await db.CustomForms.AsNoTracking().AnyAsync(item => item.Slug == slug, ct))
            throw new AdminAIActionPreviewUnavailableException("The form slug is already used.");
        return new AdminAIActionPreview(
            "form", $"form-slug:{slug}",
            new { slugAvailable = true },
            new { input.Title, slug, input.IsActive, input.StartsAt, input.ExpiresAt,
                fieldCount, coverProvided = !string.IsNullOrWhiteSpace(input.CoverImageUrl) },
            new { inactiveFormWillBeCreated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.commercial.form.create", new { actorId, slug }));
    }

    private async Task<AdminAIActionPreview> PreviewUpdateAsync(
        Guid actorId, AdminAIUpdateFormInput input, CancellationToken ct)
    {
        var fieldCount = ValidateDraft(input.Title, input.Slug, input.IsActive, input.FieldsJson);
        if (input.FormId == Guid.Empty)
            throw new ArgumentException("Form identifier is required.", nameof(input));
        if (!await db.Users.AsNoTracking().AnyAsync(item => item.Id == actorId, ct))
            throw new AdminAIActionPreviewUnavailableException("The actor is unavailable.");
        var form = await db.CustomForms.AsNoTracking()
            .Where(item => item.Id == input.FormId)
            .Select(item => new
            {
                item.Id, item.Title, item.Description, item.Slug, item.IsActive,
                item.FieldsJson, item.CoverImageUrl, item.StartsAt, item.ExpiresAt
            })
            .SingleOrDefaultAsync(ct)
            ?? throw new AdminAIActionPreviewUnavailableException("The form is unavailable.");
        if (form.IsActive || await db.FormSubmissions.AsNoTracking()
                .AnyAsync(item => item.CustomFormId == form.Id, ct))
            throw new AdminAIActionPreviewUnavailableException(
                "Active forms or forms with responses require a high-risk action.");
        var slug = input.Slug.ToLowerInvariant();
        if (await db.CustomForms.AsNoTracking()
                .AnyAsync(item => item.Slug == slug && item.Id != form.Id, ct))
            throw new AdminAIActionPreviewUnavailableException("The form slug is already used.");
        return new AdminAIActionPreview(
            "form", $"form:{form.Id:D}",
            new { form.Title, form.Slug, form.IsActive,
                fieldsHash = Hash(form.FieldsJson) },
            new { input.Title, slug, input.IsActive, input.StartsAt, input.ExpiresAt,
                fieldCount, coverProvided = !string.IsNullOrWhiteSpace(input.CoverImageUrl) },
            new { inactiveFormWillBeUpdated = true, affected = 1 },
            new { valid = true },
            Fingerprint("admin.commercial.form.update", new { actorId, form,
                fieldsHash = Hash(form.FieldsJson), submissionCount = 0 }));
    }

    private static int ValidateDraft(string title, string slug, bool isActive, string fieldsJson)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200
            || string.IsNullOrWhiteSpace(slug) || slug.Length > 100
            || !Regex.IsMatch(slug, "^[a-zA-Z0-9-_]+$", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
            throw new ArgumentException("Form metadata is invalid.");
        if (isActive)
            throw new AdminAIActionPreviewUnavailableException("Form activation requires a high-risk action.");
        try
        {
            using var parsed = JsonDocument.Parse(fieldsJson);
            if (parsed.RootElement.ValueKind == JsonValueKind.Array)
                return parsed.RootElement.GetArrayLength();
        }
        catch (JsonException) { }
        throw new ArgumentException("Form fields must be a JSON array.");
    }

    private static string Fingerprint(string key, object state) =>
        Hash(JsonSerializer.Serialize(new { key, state }));

    private static string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
