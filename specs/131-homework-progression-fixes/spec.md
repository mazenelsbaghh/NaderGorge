# Feature Specification: Homework Progression & Location Fixes

**Feature Branch**: `131-homework-progression-fixes`  
**Created**: 2026-06-15  
**Status**: Draft  
**Input**: User description: "دلوقتي فيه مشكله انو رابط كلو ببعض ff دي اصلا مش ف القسم المفروض القسم بس اللي مربوط ببعض فاهمني و الواجب لس جوه الحصه ذات نفسها لي"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Section-Isolated Progression Locking (Priority: P1)

As a student on the platform, I want the progression locking system to only lock a lesson based on the previous lesson in the same section, so that my progress in one section is not blocked by lessons in a completely different section.

**Why this priority**: Highly critical because the current behavior locks lessons across sections, blocking students from moving forward in their current section due to unrelated/prior sections.

**Independent Test**: Create two sections, Section A and Section B. Create a lesson in Section B (with no previous lessons in Section B). It should not be locked by the last lesson of Section A.

**Acceptance Scenarios**:

1. **Given** a package with Section A containing Lesson 1 and Section B containing Lesson 2, **When** I look at Lesson 2 in Section B, **Then** it should not be locked by any uncompleted requirements from Lesson 1 in Section A.
2. **Given** Lesson 1 and Lesson 2 both belong to Section A where Lesson 1 has a mandatory homework, **When** I attempt to access Lesson 2 before completing Lesson 1's homework, **Then** Lesson 2 should be locked.

---

### User Story 2 - Standalone Homework Solving Workspace (Priority: P1)

As a student, I want to solve my homework on a dedicated, standalone page and not have the homework solving interface embedded directly at the bottom of the lesson details page, so that the lesson details page remains focused on learning materials (videos/files) and the homework experience feels like an exam workspace.

**Why this priority**: Confuses the user as they are seeing homework questions inline on the lesson page even when they click to solve it, and we want to align the UX with the standalone exam page.

**Independent Test**: Navigate to a lesson page. The bottom of the page should not render any homework questions or the multi-step questionnaire. Instead, clicking the "حل الواجب" button in the carousel should direct the student to `/student/homework/[homeworkId]`.

**Acceptance Scenarios**:

1. **Given** I am on the lesson detail page for a lesson that has a homework assignment, **When** I scroll to the bottom of the page, **Then** I should only see the comments section, files/resources, and lesson exam cards, but no homework question cards or solver.
2. **Given** I am on the lesson detail page, **When** I click the "حل الواجب" button on the carousel, **Then** I should be redirected to the standalone homework solver page under `/student/homework/[homeworkId]?packageId=[packageId]`.

---

### Edge Cases

- What happens if the lesson has no videos but has a homework? The lesson detail page should display the lesson title, details, files, and exam cards, and the carousel (if shown or replaced with a fallback card) should allow navigating to the homework.
- How does the system handle progression locking if there is no previous lesson in the section? It should unlock by default (assuming the student has access/ownership of the package/lesson).

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Student - Progression Flow**: Login as a student, verify that a lesson at the beginning of Section B is unlocked regardless of whether the last lesson of Section A has an incomplete homework or exam.
- **Manual QA Student - UI Flow**: Open the lesson detail page. Scroll to the bottom and ensure the inline homework questions are completely gone. Click "حل الواجب" in the carousel, make sure it redirects to `/student/homework/[homeworkId]`.
- **Docker Acceptance**: Build both frontend and backend successfully. Verify container integrity via `docker compose ps` or similar on local environment.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST restrict progression lock checks in `GetLessonDetailQuery` and `GetLessonsQuery` to lessons belonging to the same section.
- **FR-002**: System MUST NOT render the interactive homework solver (with questions, inputs, and submission buttons) inline on the lesson details page.
- **FR-003**: The lesson detail page MUST still expose the homework button in the video carousel (navigating to the standalone page) if a homework is configured for the lesson.
- **FR-004**: System MUST keep the standalone homework workspace page `/student/homework/[homeworkId]` fully functional for starting, solving, and viewing results of homework submissions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Progression locks do not span across sections, yielding 0 requests failing to unlock a correct lesson.
- **SC-002**: 0 items representing homework questions are rendered on `/student/packages/[packageId]/lessons/[lessonId]` page.

## Assumptions

- We assume the database schemas are already in sync and do not require new migrations since this is a logic and frontend UI correction.
