# Implementation Plan: Refine Multiple Exams Builder

**Branch**: `124-refine-multiple-exams-builder` | **Date**: 2026-06-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/124-refine-multiple-exams-builder/spec.md`

## Summary

Refine the `UnifiedAssessmentBuilder` frontend component to allow locking/forcing target types (`Lesson` or `Video`). This ensures that:
1. When editing or creating a lesson-level exam, the target is locked to "Lesson" and target-selection toggles are hidden.
2. When creating a video-level exam (pop quiz), the target is locked to "Video" and target-selection toggles are hidden, but the video selection dropdown remains visible.
3. The button labels are updated contextually based on the target type to prevent user confusion.
4. The videos list page (`LessonVideoList.tsx`) displays the "امتحان مرفق" status badge for videos with associated exams.

## Technical Context

**Language/Version**: TypeScript 5.x, React 19 / Next.js 16.2.1  
**Primary Dependencies**: lucide-react, react-hot-toast  
**Storage**: N/A (Frontend changes only)  
**Testing**: npm run lint, npm run build  
**Target Platform**: Web (Admin Panel and Teacher Panel)  
**Performance Goals**: Instant UI rendering, zero layout shifts.  
**Constraints**: Keep backwards compatibility for legacy components.

## Constitution Check

- **Layer impact**: Frontend components only (`UnifiedAssessmentBuilder.tsx`, `LessonProfilePageClient.tsx`, `TeacherLessonProfilePageClient.tsx`, `LessonVideoList.tsx`). No backend or database schema changes required.
- **Automated tests**: Verify frontend compilation with `npm run build`.
- **Manual QA flows**: Verified by logged-in admin navigating to a lesson page, testing both tabs, and verifying the creation and layout.
- **Docker gate**: Verified by deploying and verifying status of containers on the VPS or local dev build.

## Project Structure

```text
frontend/
├── src/
│   ├── components/
│   │   └── admin/
│   │       ├── UnifiedAssessmentBuilder.tsx
│   │       └── LessonVideoList.tsx
│   └── app/
│       ├── admin/
│       │   └── content/
│       │       └── lessons/
│       │           └── [id]/
│       │               └── LessonProfilePageClient.tsx
│       └── teacher/
│           └── packages/
│               └── lessons/
│                   └── [id]/
│                       └── TeacherLessonProfilePageClient.tsx
```

## Proposed Changes

### 1. `UnifiedAssessmentBuilder.tsx`
- Add `forceTargetType?: 'Lesson' | 'Video'` to props interface.
- Initialize `targetType` state using `forceTargetType` if provided, otherwise default to `'Lesson'`.
- In rendering, if `forceTargetType` is provided, hide the "ارتباط الامتحان (الهدف)" selection buttons.
- Update the submit button text:
  - If `isExam && targetType === 'Video'`: "إنشاء امتحان الفيديو (Pop Quiz)"
  - If `isExam && targetType === 'Lesson'`: "إنشاء امتحان الحصة ككل"
  - If `type === 'homework'`: "إنشاء الواجب"

### 2. `LessonProfilePageClient.tsx`
- Update the first `UnifiedAssessmentBuilder` (under Lesson Exam tab) to pass `forceTargetType="Lesson"`.
- Update the second `UnifiedAssessmentBuilder` (under video exam builder card) to pass `forceTargetType="Video"`.

### 3. `TeacherLessonProfilePageClient.tsx`
- Update the `UnifiedAssessmentBuilder` (under Lesson Exam tab) to pass `forceTargetType="Lesson"`.

### 4. `LessonVideoList.tsx`
- Update the badge display condition for attached exams from `{video.examId && ...}` to `{(video.examId || (video.exams && video.exams.length > 0)) && ...}`.
- If multiple exams are attached, show the count: `"امتحان مرفق" + (video.exams.length > 1 ? ` (${video.exams.length})` : '')`.

## Phase Closure & Verification Plan

**Automated Tests Required**:
- `npm run build` in the `frontend` folder to ensure no type safety or next build errors.

**Docker Gate Required**:
- Verify all docker containers are up and healthy.

**Manual QA Required**:
- **Admin Flow**: Open `/admin/content/lessons/[id]`, verify under "الامتحان المرفق" tab that the target toggles are hidden and the save buttons say "إنشاء امتحان الحصة ككل" / "إنشاء امتحان الفيديو (Pop Quiz)" respectively.
- Verify that videos with exams display the badge "امتحان مرفق" in the "الفيديوهات" tab.
