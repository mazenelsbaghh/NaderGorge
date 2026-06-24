using NaderGorge.Application.Features.LiveSupportAI.Dtos;

namespace NaderGorge.Application.Features.LiveSupportAI.Interfaces;

public interface ILiveSupportAIKnowledgeService
{
    Task<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>> ListAsync(CancellationToken cancellationToken);
    Task<LiveSupportAIKnowledgeRevisionDto> SaveRevisionAsync(Guid adminUserId, SaveLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken);
    Task LinkPublishedRevisionsAsync(Guid adminUserId, LinkLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<LiveSupportAIKnowledgeDocumentDto>> SearchPublishedAsync(
        Guid policyVersionId,
        string query,
        int maximumDocuments,
        int maximumCharacters,
        CancellationToken cancellationToken);
}
