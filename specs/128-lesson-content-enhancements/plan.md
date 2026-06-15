# 128 — Lesson Content Enhancements: Technical Plan

## Sub-Feature A: Video Edit Form (replace window.prompt)

### Frontend: `LessonVideoList.tsx`
- Add editing state: `editingVideo: any | null`
- When `editingVideo` is set, render an **inline form** below the video row (not a modal)
- Pre-populate fields: title, provider, urlOrEmbedCode, order, maxWatchCount
- Reuse existing `Dropdown`, `NumberField` components from admin design system
- On submit: call `adminService.updateVideo()`, clear editing state, call `onRefresh()`
- On cancel: clear editing state
- Remove the old `handleEditVideo` function (which used `window.prompt`)

---

## Sub-Feature B: Video IsActive Toggle

### Backend: EF Core Migration
- Add `public bool IsActive { get; set; } = true;` to `LessonVideo` entity in `ContentEntities.cs`
- Create EF migration: `AddIsActiveToLessonVideo`

### Backend: Toggle Endpoint
- **File**: `AdminController.cs`
- `PATCH /api/admin/videos/{id:guid}/toggle-active`
- New command: `ToggleVideoActiveCommand(Guid VideoId)` in `Features/Admin/Commands/`
- Handler toggles `IsActive` and saves

### Backend: Student-side Filtering
- In lesson content queries, filter `Videos` where `IsActive == true` for student views

### Frontend: Toggle UI
- Add Eye/EyeOff icon button in `LessonVideoList` action buttons
- Dimmed row styling for inactive videos (`opacity-50` + "مخفي" badge)
- Service: `adminService.toggleVideoActive(videoId)` → `PATCH`

---

## Sub-Feature C: Resource File Upload

### Backend: Upload Endpoint
- **File**: `AdminController.cs`
- `POST /api/admin/resources/upload` — accepts `IFormFile`
- Saves to `/uploads/resources/{guid}_{filename}` directory
- Returns `{ url: "https://assets.domain/uploads/resources/..." }`
- Max file size: 10MB
- Allowed types: PDF, images, Word docs

### Frontend: `AddResourceForm.tsx` Enhancement
- Add tab/radio toggle: "رفع ملف" | "لينك مباشر"
- File upload tab: `<input type="file">` with drag area
- On file select, upload via `adminService.uploadResourceFile(file)`
- Use returned URL as the `fileUrl` for `createResource()`
- URL tab: keep current URL input as-is

---

## Sub-Feature D: Exam Profile Page

### Frontend: New Page
- **File**: `frontend/src/app/admin/content/exams/[id]/page.tsx` + `ExamProfilePageClient.tsx`
- Fetch exam dashboard via existing `adminService.getExamDashboard(examId)`
- Layout:
  1. Header with exam title, stats row (questions, score, duration, pass rate)
  2. Students table: who took the exam, their scores
  3. Question list with per-question stats (correct/wrong percentage)
  4. Inline edit capability per question
  5. "Add Question" form at the bottom

### Backend: Enhance ExamDashboardQuery
- Add per-question answer stats: `CorrectCount`, `WrongCount`, `CorrectPercentage`
- Calculated from `StudentExamAnswer` join table data

### Frontend: AttachedExamViewer Button
- Add "عرض البروفايل" button → `router.push(/admin/content/exams/${examId})`

---

## Sub-Feature E: Exam Auto-Save

### Frontend: `UnifiedAssessmentBuilder.tsx` Enhancement
- After the exam is first created (gets an ID), switch to incremental mode
- Each new question added → `adminService.addQuestionsToExam(examId, [question])`
- Visual status: "⏳ جارٍ الحفظ..." → "✓ تم الحفظ" → "✕ خطأ في الحفظ"
- State: `autoSaveStatus: 'idle' | 'saving' | 'saved' | 'error'`

---

## Verification Plan
- `dotnet build` — backend builds with no new errors
- `npm run build` — frontend builds with no errors
- Manual: test video edit, toggle, resource upload, exam profile, auto-save
