using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;
using NaderGorge.Domain.Interfaces;
using NaderGorge.Domain.Entities.LiveSupport;
using System.Security.Cryptography;
using System.Text;

namespace NaderGorge.Infrastructure.Services.LiveSupportAI;

public sealed class LiveSupportAIKnowledgeService(IAppDbContext db) : ILiveSupportAIKnowledgeService
{
    public async Task<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>> ListAsync(CancellationToken cancellationToken)
    {
        var query = from revision in db.LiveSupportAIKnowledgeRevisions.AsNoTracking()
                    join entry in db.LiveSupportAIKnowledgeEntries.AsNoTracking() on revision.EntryId equals entry.Id
                    orderby entry.Title, revision.RevisionNumber descending
                    select new LiveSupportAIKnowledgeRevisionDto(entry.Id, revision.Id, entry.Title, revision.RevisionNumber, revision.Content,
                        revision.SourceLabel, revision.IsPublished, revision.ValidFrom, revision.ValidUntil, revision.PublishedAt);
        return await query.Take(500).ToListAsync(cancellationToken);
    }

    public async Task<LiveSupportAIKnowledgeRevisionDto> SaveRevisionAsync(Guid adminUserId, SaveLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200 || string.IsNullOrWhiteSpace(request.Content) || request.Content.Length > 100_000 || request.ValidUntil <= request.ValidFrom)
            throw new InvalidOperationException("KNOWLEDGE_VALIDATION_FAILED");
        LiveSupportAIKnowledgeEntry entry;
        if (request.EntryId.HasValue)
            entry = await db.LiveSupportAIKnowledgeEntries.SingleOrDefaultAsync(item => item.Id == request.EntryId, cancellationToken) ?? throw new InvalidOperationException("KNOWLEDGE_NOT_FOUND");
        else
        {
            entry = new LiveSupportAIKnowledgeEntry { Title = request.Title.Trim(), CreatedByUserId = adminUserId, Version = 1 };
            db.LiveSupportAIKnowledgeEntries.Add(entry);
        }
        entry.Title = request.Title.Trim();
        entry.Version++;
        var revisionNumber = (await db.LiveSupportAIKnowledgeRevisions.Where(item => item.EntryId == entry.Id).MaxAsync(item => (int?)item.RevisionNumber, cancellationToken) ?? 0) + 1;
        var content = request.Content.Trim();
        var revision = new LiveSupportAIKnowledgeRevision
        {
            EntryId = entry.Id,
            RevisionNumber = revisionNumber,
            Content = content,
            SourceLabel = request.SourceLabel?.Trim(),
            SearchText = $"{entry.Title} {content}".ToLowerInvariant(),
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant(),
            IsPublished = request.Publish,
            ValidFrom = request.ValidFrom,
            ValidUntil = request.ValidUntil,
            CreatedByUserId = adminUserId,
            PublishedByUserId = request.Publish ? adminUserId : null,
            PublishedAt = request.Publish ? DateTime.UtcNow : null
        };
        db.LiveSupportAIKnowledgeRevisions.Add(revision);
        await db.SaveChangesAsync(cancellationToken);
        return new(entry.Id, revision.Id, entry.Title, revision.RevisionNumber, revision.Content, revision.SourceLabel, revision.IsPublished, revision.ValidFrom, revision.ValidUntil, revision.PublishedAt);
    }

    public async Task LinkPublishedRevisionsAsync(Guid adminUserId, LinkLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken)
    {
        _ = adminUserId;
        if (!await db.LiveSupportAIPolicyVersions.AnyAsync(item => item.Id == request.PolicyVersionId, cancellationToken)) throw new InvalidOperationException("POLICY_NOT_FOUND");
        var ids = request.RevisionIds.Distinct().ToArray();
        if (ids.Length > 100 || await db.LiveSupportAIKnowledgeRevisions.CountAsync(item => ids.Contains(item.Id) && item.IsPublished, cancellationToken) != ids.Length)
            throw new InvalidOperationException("KNOWLEDGE_REVISION_NOT_PUBLISHED");
        var existing = await db.LiveSupportAIPolicyKnowledgeRevisions.Where(item => item.PolicyVersionId == request.PolicyVersionId).ToListAsync(cancellationToken);
        db.LiveSupportAIPolicyKnowledgeRevisions.RemoveRange(existing);
        foreach (var revisionId in ids) db.LiveSupportAIPolicyKnowledgeRevisions.Add(new LiveSupportAIPolicyKnowledgeRevision { PolicyVersionId = request.PolicyVersionId, KnowledgeRevisionId = revisionId });
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<LiveSupportAIKnowledgeDocumentDto>> SearchPublishedAsync(
        Guid policyVersionId,
        string query,
        int maximumDocuments,
        int maximumCharacters,
        CancellationToken cancellationToken)
    {
        var documentLimit = Math.Clamp(maximumDocuments, 1, LiveSupportAIContractLimits.MaxKnowledgeDocuments);
        var characterLimit = Math.Clamp(maximumCharacters, 1_000, LiveSupportAIContractLimits.MaxContextCharacters);
        var linkedRevisionIds = await db.LiveSupportAIPolicyKnowledgeRevisions
            .Where(link => link.PolicyVersionId == policyVersionId)
            .Select(link => link.KnowledgeRevisionId)
            .ToListAsync(cancellationToken);

        if (linkedRevisionIds.Count == 0) return [];

        var candidates = await db.LiveSupportAIKnowledgeRevisions
            .AsNoTracking()
            .Where(revision => linkedRevisionIds.Contains(revision.Id) && revision.IsPublished)
            .OrderByDescending(revision => revision.PublishedAt)
            .ThenByDescending(revision => revision.RevisionNumber)
            .Take(50)
            .Select(revision => new
            {
                revision.Id,
                revision.EntryId,
                revision.Content,
                revision.SearchText
            })
            .ToListAsync(cancellationToken);

        var titles = await db.LiveSupportAIKnowledgeEntries
            .AsNoTracking()
            .Where(entry => candidates.Select(candidate => candidate.EntryId).Contains(entry.Id))
            .ToDictionaryAsync(entry => entry.Id, entry => entry.Title, cancellationToken);

        var terms = Normalize(query)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();

        var ranked = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = terms.Count(term => Normalize(candidate.SearchText).Contains(term, StringComparison.Ordinal))
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Candidate.Id)
            .ToList();

        var result = new List<LiveSupportAIKnowledgeDocumentDto>(documentLimit);
        var usedCharacters = 0;
        foreach (var item in ranked)
        {
            if (result.Count >= documentLimit || usedCharacters >= characterLimit) break;
            var remaining = characterLimit - usedCharacters;
            var content = item.Candidate.Content.Length <= remaining
                ? item.Candidate.Content
                : item.Candidate.Content[..remaining];
            if (content.Length == 0) break;
            result.Add(new LiveSupportAIKnowledgeDocumentDto(
                item.Candidate.Id,
                titles.GetValueOrDefault(item.Candidate.EntryId, "معلومة دعم"),
                content));
            usedCharacters += content.Length;
        }

        return result;
    }

    private static string Normalize(string value) => string.Join(
        ' ',
        value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
