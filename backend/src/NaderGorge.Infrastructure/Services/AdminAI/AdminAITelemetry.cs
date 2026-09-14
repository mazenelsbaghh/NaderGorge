using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace NaderGorge.Infrastructure.Services.AdminAI;

public sealed class AdminAITelemetry(ILogger<AdminAITelemetry> logger)
{
    private static readonly Meter Meter = new("NaderGorge.AdminAI", "1.0.0");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("admin_ai.duration", "ms");
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("admin_ai.outcomes");
    private static readonly Counter<long> Proposals = Meter.CreateCounter<long>("admin_ai.proposals");
    private static readonly HashSet<string> OutcomesAllowed = ["success", "failure", "rejected", "cancelled", "expired", "replayed", "recovery-required"];
    private static readonly HashSet<string> StagesAllowed = ["queue", "model", "read", "proposal", "execution", "recovery"];
    private static readonly HashSet<string> DecisionsAllowed = ["answer", "clarify", "propose_actions", "refuse", "none"];
    private static readonly HashSet<string> RiskAllowed = ["none", "ordinary", "strong"];

    public void RecordDuration(string stage, double milliseconds, string outcome)
    {
        var safeStage = Allowed(stage, StagesAllowed);
        var safeOutcome = Allowed(outcome, OutcomesAllowed);
        if (!double.IsFinite(milliseconds) || milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
        Duration.Record(milliseconds, new KeyValuePair<string, object?>("stage", safeStage), new KeyValuePair<string, object?>("outcome", safeOutcome));
    }

    public void RecordOutcome(string stage, string outcome, string decisionType = "none")
    {
        var safeStage = Allowed(stage, StagesAllowed);
        var safeOutcome = Allowed(outcome, OutcomesAllowed);
        var safeDecision = Allowed(decisionType, DecisionsAllowed);
        Outcomes.Add(1, new KeyValuePair<string, object?>("stage", safeStage), new KeyValuePair<string, object?>("outcome", safeOutcome), new KeyValuePair<string, object?>("decision_type", safeDecision));
        logger.LogInformation("AdminAI stage {Stage} completed with {Outcome} and decision {DecisionType}", safeStage, safeOutcome, safeDecision);
    }

    public void RecordProposalCount(int count, string risk)
    {
        if (count is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(count));
        var safeRisk = Allowed(risk, RiskAllowed);
        var countBucket = count switch { 0 => "zero", 1 => "one", _ => "multiple" };
        Proposals.Add(count, new KeyValuePair<string, object?>("risk", safeRisk), new KeyValuePair<string, object?>("count_bucket", countBucket));
    }

    private static string Allowed(string label, HashSet<string> allowed) =>
        allowed.Contains(label) ? label : throw new ArgumentException("Telemetry label is not allowlisted.", nameof(label));
}
