using FluentValidation;

namespace NaderGorge.Application.Features.Reporting;

public sealed class ExecuteReportRequestValidator : AbstractValidator<ExecuteReportRequest>
{
    public ExecuteReportRequestValidator()
    {
        RuleFor(x => x.Domain).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleForEach(x => x.Columns).MaximumLength(64);
        RuleFor(x => x.FilterGroup).Must(group => ReportFilterRules.CountConditions(group) <= 30)
            .WithMessage("لا يمكن استخدام أكثر من 30 شرطًا في التقرير.");
        RuleFor(x => x.FilterGroup).Must(ReportFilterRules.HasValidStructure)
            .WithMessage("مجموعات الفلاتر تقبل and/or فقط، وبحد أقصى 10 مجموعات و3 مستويات.");
    }
}

internal static class ReportFilterRules
{
    public static int CountConditions(ReportFilterGroup? group) => group == null
        ? 0
        : (group.Filters?.Count ?? 0) + (group.Groups?.Sum(CountConditions) ?? 0);

    public static bool HasValidStructure(ReportFilterGroup? group) => group == null ||
        CountGroups(group) <= 10 && Depth(group) <= 3 && AllLogicValuesAreValid(group);

    private static int CountGroups(ReportFilterGroup group) => 1 + (group.Groups?.Sum(CountGroups) ?? 0);
    private static int Depth(ReportFilterGroup group) => 1 + (group.Groups is { Count: > 0 } ? group.Groups.Max(Depth) : 0);
    private static bool AllLogicValuesAreValid(ReportFilterGroup group) =>
        group.Logic is "and" or "or" && (group.Groups?.All(AllLogicValuesAreValid) ?? true);
}

public sealed class SaveReportDefinitionRequestValidator : AbstractValidator<SaveReportDefinitionRequest>
{
    public SaveReportDefinitionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Configuration).SetValidator(new ExecuteReportRequestValidator());
    }
}

public sealed class UpdateReportDefinitionRequestValidator : AbstractValidator<UpdateReportDefinitionRequest>
{
    public UpdateReportDefinitionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Configuration).SetValidator(new ExecuteReportRequestValidator());
    }
}

public sealed class ExecuteReportQueryValidator : AbstractValidator<ExecuteReportQuery>
{
    public ExecuteReportQueryValidator() => RuleFor(query => query.Request).SetValidator(new ExecuteReportRequestValidator());
}

public sealed class CreateReportDefinitionCommandValidator : AbstractValidator<CreateReportDefinitionCommand>
{
    public CreateReportDefinitionCommandValidator() => RuleFor(command => command.Request).SetValidator(new SaveReportDefinitionRequestValidator());
}

public sealed class UpdateReportDefinitionCommandValidator : AbstractValidator<UpdateReportDefinitionCommand>
{
    public UpdateReportDefinitionCommandValidator() => RuleFor(command => command.Request).SetValidator(new UpdateReportDefinitionRequestValidator());
}

public sealed class CopyReportDefinitionCommandValidator : AbstractValidator<CopyReportDefinitionCommand>
{
    public CopyReportDefinitionCommandValidator() => RuleFor(command => command.Request.Name).MaximumLength(120);
}
