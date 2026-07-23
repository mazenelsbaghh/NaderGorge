using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Features.HR.Payroll;

public sealed record PayrollCalculationInput(Guid EmployeeId, decimal BaseSalary, int LateMinutes, int AbsenceDays,
    int OvertimeMinutes, IReadOnlyDictionary<string, decimal>? Variables = null);
public sealed record CalculatedPayrollLine(Guid ComponentId, string ComponentCode, PayComponentClass Classification,
    decimal Amount, string InputsJson, string Explanation, Guid RuleId, int RuleVersion);
public sealed record PayrollCalculationResult(decimal Gross, decimal Deductions, decimal Net, IReadOnlyList<CalculatedPayrollLine> Lines);

public sealed class PayrollCalculationEngine
{
    private static readonly Regex Fixed = new("^fixed:(?<value>-?\\d+(?:\\.\\d{1,4})?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Percentage = new("^percentage:(?<value>\\d+(?:\\.\\d{1,4})?)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex InputRate = new("^input:(?<key>[a-z0-9_.-]+) \\* rate$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> AttendanceExpressions = ["attendance.late_minutes * rate", "attendance.absence_days * rate", "attendance.overtime_minutes * rate"];

    public static bool IsValidExpression(string expression)
        => expression == "base" || Fixed.IsMatch(expression) || Percentage.IsMatch(expression) ||
           AttendanceExpressions.Contains(expression) || InputRate.IsMatch(expression);

    public PayrollCalculationResult Calculate(PayrollCalculationInput input, IEnumerable<PayrollRule> rules)
    {
        var lines = new List<CalculatedPayrollLine>();
        foreach (var rule in rules.Where(item => item.IsActive).OrderBy(item => item.Priority).ThenBy(item => item.Id))
        {
            if (!IsValidExpression(rule.Expression)) throw new InvalidOperationException($"PAYROLL_EXPRESSION_INVALID:{rule.Id}");
            var raw = Evaluate(rule, input); var amount = decimal.Round(raw, 2, MidpointRounding.AwayFromZero);
            var component = rule.PayComponent ?? throw new InvalidOperationException("PAY_COMPONENT_NOT_LOADED");
            var inputs = JsonSerializer.Serialize(new { input.BaseSalary, input.LateMinutes, input.AbsenceDays, input.OvertimeMinutes, rule.Rate, variables = input.Variables });
            lines.Add(new CalculatedPayrollLine(component.Id, component.Code, component.Classification, amount, inputs,
                $"{rule.Name}: {rule.Expression} = {amount:0.00} (rule v{rule.Version})", rule.Id, rule.Version));
        }
        var gross = lines.Where(item => item.Classification == PayComponentClass.Earning).Sum(item => item.Amount);
        var deductions = lines.Where(item => item.Classification == PayComponentClass.Deduction).Sum(item => item.Amount);
        return new PayrollCalculationResult(gross, deductions, decimal.Round(gross - deductions, 2, MidpointRounding.AwayFromZero), lines);
    }

    private static decimal Evaluate(PayrollRule rule, PayrollCalculationInput input)
    {
        if (rule.Expression == "base") return input.BaseSalary;
        var fixedMatch = Fixed.Match(rule.Expression); if (fixedMatch.Success) return Parse(fixedMatch.Groups["value"].Value);
        var percentageMatch = Percentage.Match(rule.Expression); if (percentageMatch.Success) return input.BaseSalary * Parse(percentageMatch.Groups["value"].Value) / 100m;
        if (rule.Expression == "attendance.late_minutes * rate") return input.LateMinutes * rule.Rate;
        if (rule.Expression == "attendance.absence_days * rate") return input.AbsenceDays * rule.Rate;
        if (rule.Expression == "attendance.overtime_minutes * rate") return input.OvertimeMinutes * rule.Rate;
        var inputMatch = InputRate.Match(rule.Expression);
        if (inputMatch.Success && input.Variables?.TryGetValue(inputMatch.Groups["key"].Value, out var value) == true) return value * rule.Rate;
        return 0;
    }

    private static decimal Parse(string value) => decimal.Parse(value, NumberStyles.Number, CultureInfo.InvariantCulture);
}

public static class PayrollRunTransitions
{
    public static bool TryMove(HrPayrollRun run, HrPayrollRunStatus target, Guid actorUserId, DateTime now)
    {
        if (run.Status == HrPayrollRunStatus.Closed) return false;
        var allowed = (run.Status, target) switch
        {
            (HrPayrollRunStatus.Draft, HrPayrollRunStatus.Prepared) => true,
            (HrPayrollRunStatus.Prepared, HrPayrollRunStatus.FinanceReview) => true,
            (HrPayrollRunStatus.FinanceReview, HrPayrollRunStatus.FinanceApproved) => true,
            (HrPayrollRunStatus.FinanceReview, HrPayrollRunStatus.Returned) => true,
            (HrPayrollRunStatus.FinanceApproved, HrPayrollRunStatus.GMApproved) => true,
            (HrPayrollRunStatus.FinanceApproved, HrPayrollRunStatus.Returned) => true,
            (HrPayrollRunStatus.Returned, HrPayrollRunStatus.Draft) => true,
            (HrPayrollRunStatus.GMApproved, HrPayrollRunStatus.Paid) => true,
            (HrPayrollRunStatus.Paid, HrPayrollRunStatus.Closed) => true,
            _ => false
        };
        if (!allowed) return false;
        run.Status = target; run.Version++;
        if (target == HrPayrollRunStatus.Prepared) { run.PreparedByUserId = actorUserId; run.PreparedAt = now; }
        if (target is HrPayrollRunStatus.FinanceReview or HrPayrollRunStatus.FinanceApproved) { run.FinanceReviewedByUserId = actorUserId; run.FinanceReviewedAt = now; }
        if (target == HrPayrollRunStatus.GMApproved) { run.GmApprovedByUserId = actorUserId; run.GmApprovedAt = now; }
        if (target == HrPayrollRunStatus.Paid) { run.PaidByUserId = actorUserId; run.PaidAt = now; }
        if (target == HrPayrollRunStatus.Closed) run.ClosedAt = now;
        return true;
    }
}
