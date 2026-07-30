using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Interfaces;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class AIAdminAuthorizationTests
{
    [Fact]
    public void Configuration_controller_requires_only_the_builtin_admin_role()
    {
        var attribute = Assert.Single(typeof(LiveSupportAIAdminController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Admin", attribute.Roles);
        Assert.Null(attribute.Policy);
    }

    [Fact]
    public async Task Disable_endpoint_returns_202_and_passes_admin_identity_to_reconciliation_request()
    {
        var service = new RecordingAdminService();
        var adminId = Guid.NewGuid();
        var controller = new LiveSupportAIAdminController(service, new NoopKnowledgeService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, adminId.ToString())], "test"))
                }
            }
        };

        var result = await controller.Disable(new ChangeAIStateRequest(8), CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Equal(adminId, service.AdminId);
        Assert.Equal(8, service.ExpectedVersion);
    }

    private sealed class RecordingAdminService : ILiveSupportAIAdminService
    {
        public Guid AdminId { get; private set; }
        public long ExpectedVersion { get; private set; }
        public LiveSupportAICatalogsDto GetCatalogs() => new([], [], [], []);
        public Task<LiveSupportAIConfigDto> GetConfigAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIPolicyDto> SaveDraftAsync(Guid adminUserId, SaveLiveSupportAIDraftRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIPolicyDto> PublishAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DisableAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken) { AdminId = adminUserId; ExpectedVersion = expectedVersion; return Task.CompletedTask; }
        public Task<LiveSupportAIPolicyDto> EnableAsync(Guid adminUserId, long expectedVersion, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIStatsDto> GetStatsAsync(string period, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LiveSupportAdminConversationDto>> GetActiveConversationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIPreviewResultDto> PreviewAsync(LiveSupportAIPreviewRequestDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIEvidencePageDto> GetEvidenceAsync(string period, string? cursor, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopKnowledgeService : ILiveSupportAIKnowledgeService
    {
        public Task<IReadOnlyList<LiveSupportAIKnowledgeRevisionDto>> ListAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<LiveSupportAIKnowledgeRevisionDto> SaveRevisionAsync(Guid adminUserId, SaveLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task LinkPublishedRevisionsAsync(Guid adminUserId, LinkLiveSupportAIKnowledgeRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<LiveSupportAIKnowledgeDocumentDto>> SearchPublishedAsync(Guid policyVersionId, string query, int maximumDocuments, int maximumCharacters, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
