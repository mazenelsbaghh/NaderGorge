using NaderGorge.Domain.Common;
namespace NaderGorge.Domain.Entities;
public sealed class ApprovalDelegation : BaseEntity
{
    public Guid PrincipalUserId { get; set; } public Guid DelegateUserId { get; set; }
    public string Scope { get; set; } = string.Empty; public DateTime StartsAt { get; set; } public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true; public string Reason { get; set; } = string.Empty;
}
