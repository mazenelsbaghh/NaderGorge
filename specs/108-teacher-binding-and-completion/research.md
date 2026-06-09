# Research: Teacher Binding and Teacher/Student Completion

## Decisions

### 1. Teacher Dashboard Statistics & Roster Query
- **Decision**: Introduce a new MediatR query `GetTeacherDashboardStatsQuery` inside `NaderGorge.Application`.
- **Rationale**: Keeps the codebase DRY and utilizes Entity Framework's `IQueryable` to fetch counts dynamically without loading full collections.
- **Alternatives Considered**: Fetching counts on the client-side. Rejected due to poor performance and excessive database payload.

### 2. Forced Binding & Validation Error instead of GUID Fallback
- **Decision**: In `BulkGenerateCodesCommand` and `CreateInlineExamCommand`, replace the hardcoded Guid check with a direct validation exception or fail response:
  ```csharp
  if (groupTeacherId == Guid.Empty)
  {
      return ApiResponse<BulkGenerateCodesResponse>.Fail("Teacher could not be resolved from the target resource.");
  }
  ```
- **Rationale**: Eliminates the risk of assigning resources to a default "orphaned" teacher profile and strictly enforces the multi-teacher security boundary.

### 3. Student Portal Teacher Branding display
- **Decision**: Extend package queries (`GetPackageDetailsQuery`, `GetLessonDetailsQuery`) to populate the `TeacherDto` inside the response, and update the Next.js frontend pages to render the teacher details with a premium Arabic card.
- **Rationale**: Enhances brand alignment and builds confidence in course authorship.

## Backend APIs to Implement

### Teacher Controller (`TeacherController.cs`)
- `GET /api/v1/teacher/dashboard/stats`: Returns stats counts.
- `GET /api/v1/teacher/students`: Returns unique students list.
- `GET /api/v1/teacher/essays`: Returns pending essays for the teacher.
- `POST /api/v1/teacher/essays/{submissionId}/grade`: Submits a grade for an essay.
- `GET /api/v1/teacher/profile`: Returns the teacher's profile.
- `PUT /api/v1/teacher/profile`: Updates biography, contact info, specialization, and image.
