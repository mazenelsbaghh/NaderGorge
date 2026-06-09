using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities.Student;

namespace NaderGorge.Application.Features.Warnings.Commands;

public record TriggerWarningCommand(
    Guid StudentId,
    WarningSeverity Severity,
    string TriggerReason) : IRequest<ApiResponse<Guid>>;
