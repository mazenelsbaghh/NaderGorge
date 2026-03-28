using System;

namespace NaderGorge.Application.Features.Admin.Commands;

public class ToggleStudentStatusRequest
{
    public bool IsActive { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class OverrideVideoLimitRequest
{
    public Guid VideoId { get; set; }
    public int AddedViews { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class GamificationAdjustmentRequest
{
    public decimal Points { get; set; }
    public string Reason { get; set; } = string.Empty;
}
