using NaderGorge.Application.Common.Configuration;
using NaderGorge.Application.Interfaces.Finance;
using NaderGorge.Domain.Entities;
using Microsoft.Extensions.Options;

namespace NaderGorge.Application.Services.Finance;

/// <summary>Coordinates source adapters and allows a safe shadow-only rollout.</summary>
public sealed class LiveFinancialProjectionCoordinator(
    IEnumerable<IFinancialSourceAdapter> adapters,
    IOptions<PlatformFinanceOptions> options) : ILiveFinancialProjectionCoordinator
{
    public async Task<JournalEntry> PostSourceAsync(FinanceSourcePostingRequest request, CancellationToken ct)
    {
        if (!options.Value.MutationsEnabled && !options.Value.ShadowPostingEnabled)
            throw new InvalidOperationException("FINANCE_MUTATIONS_DISABLED");
        var adapter = adapters.SingleOrDefault(candidate => candidate.CanHandle(request.SourceType))
            ?? throw new InvalidOperationException($"FINANCE_ADAPTER_NOT_REGISTERED:{request.SourceType}");
        return await adapter.PostAsync(request, ct);
    }
}
