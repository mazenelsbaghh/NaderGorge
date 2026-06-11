# Research Findings: Real-time Speed Remaining Completion

## 1. Outbox Event Inventory & Schemas

To achieve 100% real-time parity, the remaining state-changing operations in the backend must emit outbox events. The table below outlines the mappings, targets, and payload structures:

| Event Type | Command Handler | Target Group/User | Payload Schema |
|---|---|---|---|
| `TermUpdated` | `UpdateTermCommandHandler` | `Package_{packageId}` | `{ termId, packageId, name }` |
| `TermDeleted` | `DeleteTermCommandHandler` | `Package_{packageId}` | `{ termId, packageId }` |
| `SectionCreated` | `CreateSectionCommandHandler` | `Package_{packageId}` | `{ sectionId, packageId, name }` |
| `LessonPublished` | `CreateLessonCommandHandler` | `Package_{packageId}` | `{ lessonId, packageId, sectionId, title }` |
| `VideoProcessingStarted`| `CreateVideoCommandHandler` | `Lesson_{lessonId}` | `{ videoId, lessonId, title }` |
| `VideoUpdated` | `UpdateVideoCommandHandler` | `Lesson_{lessonId}` | `{ videoId, lessonId, title }` |
| `VideoDeleted` | `DeleteVideoCommandHandler` | `Lesson_{lessonId}` | `{ videoId, lessonId }` |
| `ResourceReady` | `CreateLessonResourceCommandHandler`| `Lesson_{lessonId}` | `{ resourceId, lessonId, title }` |
| `HomeworkSubmitted` | `SubmitHomeworkCommandHandler` | `Role_Admin` | `{ homeworkId, studentId, submissionId }` |
| `HomeworkGraded` | `GradeEssayCommandHandler` (if homework) | `User_{studentId}` | `{ homeworkId, studentId, grade }` |
| `ExamSubmitted` | `SubmitExamCommand` | `Role_Admin` | `{ examId, studentId, attemptId }` |
| `CommunityPostCreated` | `CreateCommunityPostCommand` | `Role_Admin` (for moderation) | `{ postId, authorId }` |
| `CommunityPostApproved`| `ApproveCommunityPostCommand` | `Role_Student` (global feed) | `{ postId, authorId, body }` |
| `CommunityPostLiked` | `ToggleCommunityPostLikeCommand` | `Role_Student` (global feed) | `{ postId, userId, count }` |
| `CommunityCommentCreated`| `CreateCommunityPostCommentCommand`| `Role_Admin` (for moderation) | `{ commentId, postId, authorId }` |

*Decision*: Register all these event handlers in `usePlatformEvents.ts` and map them to targeted cache keys in `cache-invalidation.ts`.

---

## 2. API Partitioning for Lesson Detail

Currently, `GetLessonDetailQuery` returns the entire lesson payload including metadata, videos, resources, homework, and all comments. This causes high query latency.

*Proposed Partitioning*:
- `GET /api/v1/content/lessons/{lessonId}`: Core metadata + active video description.
- `GET /api/v1/content/lessons/{lessonId}/resources`: Array of resources/downloads.
- `GET /api/v1/content/lessons/{lessonId}/comments`: Paginated list of approved comments (default 20 per page).

*Rationales*:
- Avoids loading comments eagerly, reducing initial page load time.
- Standardizes paginated comment loading via React Query/Axios lazy fetch.

---

## 3. Web Vitals Logging

To monitor performance in production, the Next.js frontend will report Core Web Vitals using the built-in `useReportWebVitals` hook or Next.js metrics.

*WebVitalsMetric Entity Schema*:
- `Id` (Guid)
- `MetricName` (string: LCP, FID, CLS, FCP, TTFB, INP)
- `Value` (double)
- `Rating` (string: good, needs-improvement, poor)
- `PageUrl` (string)
- `UserAgent` (string)
- `CreatedAt` (DateTime)

*Endpoint*: `POST /api/v1/metrics/web-vitals`
*Rate limit*: Max 10 requests/minute per client IP to prevent log floods.

---

## 4. Redis Rate Limiting Enhancements

Enforce per-user rate limiting using StackExchange.Redis on critical actions:
- **Code Activation**: 5 requests/minute per user.
- **Starting AI Job**: 2 requests/minute per admin/teacher.
- **Signed Download URL**: 10 requests/minute per user.

*Implementation*: Enforced via `[Idempotent]` filter or custom Redis rate limiter middleware.
