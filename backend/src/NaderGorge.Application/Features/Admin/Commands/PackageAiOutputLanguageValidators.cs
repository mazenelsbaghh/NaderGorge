using FluentValidation;

namespace NaderGorge.Application.Features.Admin.Commands;

public sealed class CreatePackageCommandValidator : AbstractValidator<CreatePackageCommand>
{
    public CreatePackageCommandValidator()
    {
        RuleFor(command => command.AiOutputLanguage).IsInEnum();
    }
}

public sealed class UpdatePackageCommandValidator : AbstractValidator<UpdatePackageCommand>
{
    public UpdatePackageCommandValidator()
    {
        RuleFor(command => command.AiOutputLanguage!.Value)
            .IsInEnum()
            .When(command => command.AiOutputLanguage.HasValue);
    }
}
