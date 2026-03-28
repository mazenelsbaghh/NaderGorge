# 09 — Data Model Draft

*Note: This is an entity blueprint, representing the logical domains and their core entities prior to Phase 1 database schema generation.*

## User & Identity Domain
- **User:** Base login entity (Phone, PasswordHash).
- **Role:** Definition of authority (Admin, Student, Teacher).
- **Permission:** Granular access right tied to Roles.
- **UserSession:** Tracks active JWT refresh tokens device info.
- **Device:** Tracks physical hardware fingerprints to prevent account sharing.
- **ParentContact:** Independent contact record.

## Student Domain
- **StudentProfile:** Extends User with Grade, Track, Governorate, School.
- **StudentStatus:** Tracks "Committed", "Average", "At-Risk" algorithmic status.
- **StudentProgress:** Aggregated current academic standing.
- **StudentLeaderboard:** Gamification standings.
- **StudentNotificationPreference:** Opt-in states for WhatsApp vs in-app alerts.

## Content Domain
- **Program:** Highest level grouping (e.g., "Full Year Platform").
- **Package:** Unit of sale (e.g., "Term 1 Tracker").
- **ContentSection:** Thematic unit, canonically referred to here; alias "Month".
- **Lesson:** The core atomic container.
- **Lesson components (1:N to Lesson):** LessonVideo, LessonSummary, LessonResource, MindMap, RevisionBlock.

## Assessment Domain
- **Exam:** Definitions, types (pre-requisite vs standalone), passing scores.
- **QuestionBankItem:** Reusable question entity tied to topics.
- **ExamQuestion:** Mapping of QuestionBankItems to a specific Exam.
- **StudentExamAttempt:** Tracks start/end times and final score.
- **StudentAnswer:** Granular record of what the student submitted.
- **Homework:** The assignment definition.
- **HomeworkSubmission:** The student's submitted file/text and review state.

## Access Domain
- **CodeGroup:** A generated batch of codes (e.g., "Center X September Batch").
- **AccessCode:** The unique alphanumeric string.
- **CodeActivation:** Audit log of who activated what and when.
- **StudentAccessGrant:** The actual materialized entitlement mapping Student → Content ID → Expiry Date.

## Tracking & Observability Domain
- **VideoWatchEvent:** Heartbeat pings detailing percentage watched and skip attempts.
- **LessonProgress:** Aggregated state of a student navigating a lesson workflow.
- **EngagementMetric:** Analytics rollups.
- **WarningEvent:** System-generated flags for poor behavior.
- **NotificationEvent:** Audit log of SMS/WhatsApp messages sent.
- **AuditLog:** System-wide action tracking for Admins/Assistants.

## AI Domain (Phase 4 scope preparation)
- **AITask:** A queued job request for the BullMQ worker.
- **AIAnalysisResult:** Stored output of weak-point analysis.
- **EssayReviewResult:** AI grading feedback attached to an ExamSubmission.
- **WeaknessInsight:** Derived data identifying a student's recurring mistakes.
- **RecommendationItem:** AI-suggested next steps.

## Cross-Domain Relationships
- **Access → Content:** StudentAccessGrant resolves against Package.Id or Lesson.Id.
- **Tracking → Assessment:** Warnings are generated based on StudentExamAttempt scores dropping below thresholds.
- **Identity → Student:** User (Id) 1:1 StudentProfile (UserId).
- **Identity → Content:** ParentContact 1:N StudentProfile.
