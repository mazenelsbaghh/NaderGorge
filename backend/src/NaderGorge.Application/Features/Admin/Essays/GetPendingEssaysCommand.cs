using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Essays;

public sealed record GetPendingEssaysCommand(Guid CurrentUserId) : IRequest<IReadOnlyList<PendingEssayDto>>;

public sealed record PendingEssayDto(
    Guid Id,
    Guid StudentId,
    Guid QuestionId,
    string? AnswerText,
    string? AudioUrl,
    decimal? AiInitialScore,
    string? AiFeedback,
    EssaySubmissionStatus Status);

public sealed class GetPendingEssaysCommandHandler(IAppDbContext db)
    : IRequestHandler<GetPendingEssaysCommand, IReadOnlyList<PendingEssayDto>>
{
    public async Task<IReadOnlyList<PendingEssayDto>> Handle(GetPendingEssaysCommand request, CancellationToken cancellationToken)
    {
        var teacherId = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == request.CurrentUserId
                && user.UserRoles.Any(userRole => userRole.Role.Type == RoleType.Teacher))
            .Select(user => (Guid?)user.TeacherProfile!.Id)
            .SingleOrDefaultAsync(cancellationToken);

        var scoped = db.EssaySubmissions
            .Where(submission => !teacherId.HasValue
                || submission.Question.CreatedByTeacherId == teacherId.Value);

        var aiScored = await scoped
            .Where(submission => submission.Status == EssaySubmissionStatus.AIScored)
            .ToListAsync(cancellationToken);
        foreach (var submission in aiScored)
            submission.Status = EssaySubmissionStatus.WaitTeacher;

        if (aiScored.Count > 0)
            await db.SaveChangesAsync(cancellationToken);

        return await scoped
            .AsNoTracking()
            .Where(submission => submission.Status != EssaySubmissionStatus.TeacherGraded)
            .OrderBy(submission => submission.CreatedAt)
            .Select(submission => new PendingEssayDto(
                submission.Id,
                submission.StudentId,
                submission.QuestionId,
                submission.AnswerText,
                submission.AudioUrl,
                submission.AiInitialScore,
                submission.AiFeedback,
                submission.Status))
            .ToListAsync(cancellationToken);
    }
}
