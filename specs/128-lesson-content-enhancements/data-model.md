# Data Model: 128 — Lesson Content Enhancements

## Modified Entities

### LessonVideo (MODIFY)

| Field | Type | Default | Notes |
|-------|------|---------|-------|
| **IsActive** | `bool` | `true` | **NEW** — Controls student-side visibility |

**Migration**: `AddIsActiveToLessonVideo`  
**SQL**: `ALTER TABLE "LessonVideos" ADD "IsActive" boolean NOT NULL DEFAULT true;`

---

### LessonResource (NO CHANGES)

Existing entity is sufficient. `FileUrl` already stores the URL — it can be either a manually pasted URL or a server-uploaded file URL.

---

### ExamQuestionSummaryDto (MODIFY — DTO only)

| Field | Type | Notes |
|-------|------|-------|
| **totalAttempts** | `int` | **NEW** — Total students who answered this question |
| **correctCount** | `int` | **NEW** — Correct answers count |
| **wrongCount** | `int` | **NEW** — Wrong answers count |
| **correctPercentage** | `decimal` | **NEW** — `correctCount / totalAttempts * 100` |

---

## New Entities

None — all changes modify existing entities or DTOs.

## Relationships

```
LessonVideo.IsActive → filters student-side queries
ExamQuestion → StudentAnswer (existing FK: ExamQuestionId) → aggregated for stats
LessonResource.FileUrl → can hold upload path or external URL
```

## State Transitions

### Video Visibility Toggle
```
Active (IsActive=true) ←→ Inactive (IsActive=false)
  Action: PATCH /api/admin/videos/{id}/toggle-active
  Impact: Student-side lesson queries filter out inactive videos
```

### Auto-Save Status
```
idle → saving → saved
idle → saving → error → (retry) → saving → saved
```
