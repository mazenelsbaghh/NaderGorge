namespace NaderGorge.Application.Common.Configuration;

public sealed class PlatformFinanceOptions
{
    public const string SectionName = "PlatformFinance";

    /// <summary>Allows the cockpit to read journals before mutation flows are enabled.</summary>
    public bool ReadOnlyCockpitEnabled { get; set; } = true;

    /// <summary>Records source comparisons without creating financial journal entries.</summary>
    public bool ShadowPostingEnabled { get; set; } = false;

    /// <summary>Enables expense/refund/treasury mutations. Keep disabled during a staged rollout.</summary>
    public bool MutationsEnabled { get; set; } = true;
}
