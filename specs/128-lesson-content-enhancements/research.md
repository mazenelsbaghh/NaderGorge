# Research: 128 — Lesson Content Enhancements

## R-01: Video IsActive field behavior

**Decision**: Add `IsActive` (bool, default `true`) to `LessonVideo` entity.

**Rationale**: The `LessonVideo` entity currently has no visibility toggle. Adding a boolean is the simplest approach and consistent with `StudentAccessGrant.IsActive` pattern already in the codebase.

**Alternatives considered**:
- Status enum (Draft/Published/Archived) — over-engineering for binary visibility
- Soft-delete — doesn't allow re-enabling

**Student-side impact**: The `GetLessonCockpitQuery` (backend) and student-side lesson queries must filter `where IsActive == true` to hide inactive videos from students.

---

## R-02: File upload storage pattern

**Decision**: Reuse existing `wwwroot/uploads/` storage pattern with a new `resources/` subdirectory.

**Rationale**: The codebase already stores files in `backend/wwwroot/`:
- Audio: `/uploads/audio/{guid}_{filename}`
- Subtitles: `subtitles/{videoId}.srt`
- Mindmaps: `mindmaps/{chapterId}.png`
- Content images: via `IImageStorageService.SaveAsWebpAsync()`

For resources, we store at `/uploads/resources/{guid}_{originalFilename}`. The backend serves these via static files middleware already configured.

**Alternatives considered**:
- S3 bucket — not currently used, would add infrastructure complexity
- Separate upload service — over-engineering for the current scale

**Max file size**: 10MB (consistent with content image upload limit)

**Allowed MIME types**: `application/pdf`, `image/*`, `application/msword`, `application/vnd.openxmlformats-officedocument.*`

---

## R-03: Exam per-question statistics

**Decision**: Enhance `GetExamDashboardQuery` to include real stats from `StudentAnswer` data.

**Rationale**: The `AttachedExamViewer` currently shows "إحصائيات الإجابات غير متاحة" because the backend query doesn't aggregate `StudentAnswer` data. The join path exists: `ExamQuestion → StudentAnswer (via ExamQuestionId) → IsCorrect`.

**Query approach**:
```sql
SELECT eq.Id, 
  COUNT(sa.Id) as TotalAttempts,
  SUM(CASE WHEN sa.IsCorrect THEN 1 ELSE 0 END) as CorrectCount,
  SUM(CASE WHEN NOT sa.IsCorrect THEN 1 ELSE 0 END) as WrongCount
FROM ExamQuestions eq
LEFT JOIN StudentAnswers sa ON sa.ExamQuestionId = eq.Id
GROUP BY eq.Id
```

---

## R-04: Auto-save pattern for UnifiedAssessmentBuilder

**Decision**: After the exam is created (gets a backend ID), switch to incremental mode. Each question added calls `POST /api/admin/exams/{examId}/questions` to add it immediately.

**Rationale**: The current `UnifiedAssessmentBuilder` builds all questions in-memory then submits them all at once when the user clicks "Save". This means navigating away loses everything.

**Implementation**: 
1. User starts creating exam → on first save, call `createExam()` to get an `examId`
2. Each subsequent question → call `addQuestionToExam(examId, question)` immediately
3. Show status indicator: idle → saving → saved → error

**Alternatives considered**:
- localStorage drafts — doesn't sync across devices, complex recovery logic
- Full auto-save of entire exam object — wasteful, risks race conditions
