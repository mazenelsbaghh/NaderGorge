using System.Data;
using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Exams.Commands;

public sealed record SaveExamDraftCommand(ExamDraftScope Scope, SaveExamDraftRequest Draft) : IRequest<ApiResponse<ExamDraftDto>>;

public sealed class SaveExamDraftCommandHandler(IAppDbContext db) : IRequestHandler<SaveExamDraftCommand, ApiResponse<ExamDraftDto>>
{
    public async Task<ApiResponse<ExamDraftDto>> Handle(SaveExamDraftCommand request, CancellationToken ct)
    {
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ExamDraftState.Lock(db, request.Scope, ct);
        var state = await ExamDraftState.Read(db, request.Scope, ct);
        if (state is null) return ApiResponse<ExamDraftDto>.Fail("المحاولة غير موجودة.", ["NOT_FOUND"]);
        var rejection = state.WriteRejection(request.Scope);
        if (rejection is not null) return ApiResponse<ExamDraftDto>.Fail("تغيّرت حالة المحاولة؛ أعد فتح الامتحان.", [rejection]);
        if (request.Draft.ExpectedVersion < 0 || request.Draft.MutationId == Guid.Empty || !state.Accepts(request.Draft.Answers))
            return ApiResponse<ExamDraftDto>.Fail("الإجابات لا تطابق أسئلة المحاولة الحالية.", ["EXAM_DRAFT_INVALID"]);
        var response = await Save(request, state, ct);
        if (response.Success) await transaction.CommitAsync(ct);
        return response;
    }

    private async Task<ApiResponse<ExamDraftDto>> Save(SaveExamDraftCommand request, ExamDraftState state, CancellationToken ct)
    {
        var draft = await db.StudentExamDrafts.SingleOrDefaultAsync(d =>
            d.AttemptId == request.Scope.AttemptId && d.RevisionId == state.RevisionId, ct);
        var savedAnswers = ExamDraftState.Parse(draft);
        if (draft?.LastMutationId == request.Draft.MutationId && draft.Version == request.Draft.ExpectedVersion + 1
            && savedAnswers.Count == request.Draft.Answers.Count
            && savedAnswers.All(a => request.Draft.Answers.GetValueOrDefault(a.Key) == a.Value))
            return ApiResponse<ExamDraftDto>.Ok(state.View(draft));
        if ((draft?.Version ?? 0) != request.Draft.ExpectedVersion)
            return ApiResponse<ExamDraftDto>.Fail("توجد إجابات أحدث من جهاز أو صفحة أخرى. راجع النسخة المحفوظة.",
                ["EXAM_DRAFT_CONFLICT"], state.View(draft));
        if (draft is null)
        {
            draft = new StudentExamDraft { AttemptId = request.Scope.AttemptId, RevisionId = state.RevisionId };
            db.StudentExamDrafts.Add(draft);
        }
        draft.AnswersJson = JsonSerializer.Serialize(request.Draft.Answers);
        draft.Version++;
        draft.LastMutationId = request.Draft.MutationId;
        draft.SavedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ApiResponse<ExamDraftDto>.Ok(state.View(draft));
    }
}
