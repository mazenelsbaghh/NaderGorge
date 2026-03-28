# Data Model: Phase 0 — Discovery, Planning, and Product Blueprint

**Branch**: `001-phase0-discovery-blueprint`
**Date**: 2026-03-21

## Overview

Phase 0 is a documentation phase — the "data model" here describes the **structure of the deliverables themselves** and the **platform entities they must define**. This serves as a checklist and reference for deliverable authors.

---

## Deliverable Entities

Each deliverable is a Markdown file with defined sections. Below is the required structure for each.

### 01 — Product Requirements Document (PRD)

| Section | Required Content |
|---------|-----------------|
| Platform Identity | Full academic control system, not content site |
| Target Audience | 4 student grades + parents + assistants + admin |
| Brand Identity | Youthful, simple, energetic, story-based, maps/tables, motivating |
| Value Proposition | What makes this different from generic course platforms |
| Content Hierarchy | Package → Content Section → Lesson → Video/Summary/Quiz/Homework/Resources/MindMap/Revision |
| Academic Flow | Exam → Lesson → Homework cycle |

### 02 — Content Blueprint

| Section | Required Content |
|---------|-----------------|
| Hierarchy Definition | Package → Content Section → Lesson |
| Lesson Components | Videos (multiple), written summary, short questions, homework, short quiz, downloadable file, mind map, revision content |
| Content Section | Canonical term (NOT "months"). Internal alias documented once. |
| Package Structure | Contains multiple Content Sections, each containing Lessons |

### 03 — Access Blueprint (Code System)

| Section | Required Content |
|---------|-----------------|
| Code Types | Lesson code, Package code, Term code, Promotional code (future), Referral code (future) |
| Code Behaviors | Single-use, code groups/batches, content-based logic, duration-based logic |
| Activation Flow | Entry → Validation → Confirmation → Selected date → Access grant |
| Stacking Rules | Per-type stacking matrix |
| Expiration Rules | Pre-usage expiration, post-activation duration limits |
| Conflict Resolution | What happens when multiple code types unlock overlapping content |

### 04 — Data Blueprint

| Section | Required Content |
|---------|-----------------|
| Student Data Fields | Full name, phone, parent phone, grade, study track, governorate, city/district, school |
| Engagement Data | Watch time, lesson completion, homework completion, exam performance, inactivity |
| History Data | Package history, code history, activation logs |
| Parent Data | Contact info, linked student(s) |

### 05 — User Roles Matrix

| Section | Required Content |
|---------|-----------------|
| Role Definitions | Student, Parent, Teacher, Assistant (4 sub-roles), Admin |
| Multi-Role Model | Users can hold multiple roles simultaneously |
| Permission Matrix | Table: Role × Feature → Access Level (full/read-only/none) |
| Assistant Sub-Roles | Academic assistant, Homework reviewer, Follow-up assistant, Support assistant |
| Role Extension Procedure | Process for adding new roles |

### 06 — Technical Architecture Document

| Section | Required Content |
|---------|-----------------|
| Frontend Stack | Next.js, TypeScript, Tailwind CSS, React Query, Zustand, Shadcn/UI, Framer Motion |
| Backend Stack | .NET Web API, C#, Clean Architecture, CQRS, Entity Framework Core |
| Database | PostgreSQL |
| Cache Layer | Redis — uses: caching, rate limiting, sessions, leaderboard, notification buffering |
| Background Jobs | BullMQ (Node.js) + Redis broker + .NET writes jobs |
| Video Strategy | YouTube initially, behind VideoProviderAbstraction |
| Communication Patterns | Frontend ↔ Backend API (REST), Backend ↔ Redis, Backend ↔ Node Worker (via Redis queues) |
| Provider Abstractions | Video, Notification, AI — all behind interfaces |

### 07 — Business Rules Document

| Section | Required Content |
|---------|-----------------|
| Watch Control | Max minutes, max replays, allowed speeds, skip policy, completion threshold |
| Exam Rules | MCQ, Essay, mixed; question bank; pass thresholds; instant grading; attempt tracking |
| Homework Rules | MCQ, Essay, mixed; due dates; submission states; review pipeline |
| Student Behavior | Classification: committed / average / at-risk |
| Gamification | Points, badges, levels, ranking, challenges — motivating not toxic |
| Progression | Exam/homework required before next item; pass threshold gating |

### 08 — UX Direction & Sitemap

| Section | Required Content |
|---------|-----------------|
| UX Principles | Simple, organized, motivating, clear path, controlled not oppressive |
| Registration Flow | Two-step: (1) name/phone/grade/track → (2) parent phone/governorate/city/school |
| Dashboard Design | Available packages, latest lesson, upcoming exams, progress, codes, notifications, resume |
| Sitemap | Public site, Student portal, Parent layer, Teacher panel, Assistant panel, Admin panel |
| Navigation Rules | Max 3 clicks from dashboard to lesson content |

### 09 — Data Model Draft

| Domain | Entities |
|--------|----------|
| User & Identity | User, Role, Permission, UserSession, Device, ParentContact |
| Student | StudentProfile, StudentStatus, StudentProgress, StudentLeaderboard, StudentNotificationPreference |
| Content | Program, Package, ContentSection, Lesson, LessonVideo, LessonSummary, LessonResource, MindMap, RevisionBlock |
| Assessment | Exam, ExamQuestion, QuestionBankItem, StudentExamAttempt, StudentAnswer, Homework, HomeworkSubmission |
| Access | CodeGroup, AccessCode, CodeActivation, StudentAccessGrant |
| Tracking | VideoWatchEvent, LessonProgress, EngagementMetric, WarningEvent, NotificationEvent, AuditLog |
| AI | AITask, AIAnalysisResult, EssayReviewResult, WeaknessInsight, RecommendationItem |

### 10 — System Blueprint (Deployment)

| Section | Required Content |
|---------|-----------------|
| Services | Frontend (Next.js), Backend API (.NET), Node Worker (BullMQ), PostgreSQL, Redis, Reverse Proxy, Monitoring |
| Environments | Development, Staging, Production |
| Secrets Management | Environment variables / secrets manager, never in source control |
| Performance Targets | API <500ms p95, Video page <3s, Code redemption <2s |

---

## Relationships Between Deliverables

```text
PRD (01)
 ├── Content Blueprint (02)
 │    └── feeds into → Data Model Draft (09) [Content domain entities]
 ├── Access Blueprint (03)
 │    └── feeds into → Data Model Draft (09) [Access domain entities]
 │    └── feeds into → Business Rules (07) [code activation rules]
 ├── Data Blueprint (04)
 │    └── feeds into → Data Model Draft (09) [Student domain entities]
 └── User Roles Matrix (05)
      └── feeds into → Technical Architecture (06) [RBAC design]

Technical Architecture (06)
 └── feeds into → System Blueprint (10) [deployment structure]

UX Direction (08)
 └── references → User Roles Matrix (05) [role-specific UI flows]
 └── references → Content Blueprint (02) [content navigation]
```

---

## Validation Rules

1. Every entity in Data Model Draft (09) MUST trace back to a requirement in at least one other deliverable.
2. Every code type in Access Blueprint (03) MUST have a corresponding rule in Business Rules (07).
3. Every role in User Roles Matrix (05) MUST have at least one section in UX Direction/Sitemap (08).
4. Technical Architecture (06) MUST reference all provider abstractions required by the constitution.
5. The canonical term "Content Section" MUST be used consistently across all deliverables.
