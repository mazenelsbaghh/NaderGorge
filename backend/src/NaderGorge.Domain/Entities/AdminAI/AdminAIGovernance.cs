using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities.AdminAI;

public sealed class AdminAICapabilityBaseline : BaseEntity
{
    public string Version { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    public string SafeManifestJson { get; set; } = "{}";
    public string SourceRevision { get; set; } = string.Empty;
    public string RuntimeInventoryHash { get; set; } = string.Empty;
    public string FrontendInventoryHash { get; set; } = string.Empty;
    public int SupportedReadCount { get; set; }
    public int SupportedActionCount { get; set; }
    public int ExcludedCount { get; set; }
    public AdminAICapabilityBaselineStatus Status { get; set; }
    public Guid? ApprovedByAdminUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}

public sealed class AdminAISensitiveDataPolicyVersion : BaseEntity
{
    public string Version { get; set; } = string.Empty;
    public string PolicyHash { get; set; } = string.Empty;
    public string SafeRulesJson { get; set; } = "{}";
    public AdminAISensitiveDataPolicyStatus Status { get; set; }
    public Guid? ApprovedByAdminUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
