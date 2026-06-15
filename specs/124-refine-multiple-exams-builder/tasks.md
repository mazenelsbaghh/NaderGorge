# Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

# Tasks: Refine Multiple Exams Builder

**Input**: Design documents from `/specs/124-refine-multiple-exams-builder/`
**Prerequisites**: plan.md, spec.md

## Phase 1: Implementation

### User Story 1 - Add Target Locking & Context Labels to UnifiedAssessmentBuilder

**Goal**: Support forcing target types and updating submit button text contextually.

- [ ] T001 Modify `frontend/src/components/admin/UnifiedAssessmentBuilder.tsx` to add `forceTargetType?: 'Lesson' | 'Video'` to the component props.
- [ ] T002 Update `UnifiedAssessmentBuilder.tsx` state initialization:
  - If `forceTargetType` is provided, initialize `targetType` with it.
  - Hide the "ارتباط الامتحان (الهدف)" selection buttons when `forceTargetType` is defined.
- [ ] T003 Update `UnifiedAssessmentBuilder.tsx` submit button label:
  - If `isExam && targetType === 'Video'`: "إنشاء امتحان الفيديو (Pop Quiz)"
  - If `isExam && targetType === 'Lesson'`: "إنشاء امتحان الحصة ككل"
  - If `type === 'homework'`: "إنشاء الواجب"
- [ ] T004 Update `frontend/src/app/admin/content/lessons/[id]/LessonProfilePageClient.tsx`:
  - Pass `forceTargetType="Lesson"` to the first `UnifiedAssessmentBuilder` (main lesson exam builder, line 245).
  - Pass `forceTargetType="Video"` to the second `UnifiedAssessmentBuilder` (video exam builder, line 295).
- [ ] T005 Update `frontend/src/app/teacher/packages/lessons/[id]/TeacherLessonProfilePageClient.tsx`:
  - Pass `forceTargetType="Lesson"` to the `UnifiedAssessmentBuilder` (lesson exam builder, line 179).

### User Story 2 - Video List Exam Badges

**Goal**: Display exam attachment badges in the videos list.

- [ ] T006 Update `frontend/src/components/admin/LessonVideoList.tsx` at line 404:
  - Change the condition `{video.examId && (` to `{(video.examId || (video.exams && video.exams.length > 0)) && (`.
  - Display the number of attached exams if greater than 1: `"امتحان مرفق" + (video.exams.length > 1 ? ` (${video.exams.length})` : '')`.

---

## Phase 2: Polish & Quality Gates

- [ ] T007 Run `clean-code-guard` against all modified frontend component files.
- [ ] T008 Run `test-guard` to verify no changed test files exist or review test diffs.
- [ ] T009 Run `npm run build` locally in the frontend workspace to verify type safety and next compilation.

---

## Phase 3: Verification & Deploy

- [ ] T010 Run `deploy.sh` to build and deploy modified frontend packages on the VPS.
- [ ] T011 Manually verify on the deployed page:
  - Main Lesson Exam builder has target type toggles hidden.
  - Video Exam builder has target type toggles hidden and allows selecting a video.
  - Creating a video exam succeeds and links to the correct video.
  - Attached video exams show a badge "امتحان مرفق" on the video item in the Videos tab.
