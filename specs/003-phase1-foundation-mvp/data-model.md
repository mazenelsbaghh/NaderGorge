# Data Model: Phase 1 — Foundation and MVP Launch

**Date**: 2026-03-22 | **Spec**: [spec.md](spec.md)

> This is the relational schema design for Phase 1 MVP. All tables use UUID primary keys, `created_at`/`updated_at` timestamps, and soft-delete (`deleted_at`) where applicable.

---

## Identity & Authentication Domain

### users
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| phone | VARCHAR(20) | UNIQUE, NOT NULL | Primary login identifier |
| password_hash | VARCHAR(255) | NOT NULL | bcrypt/argon2 |
| full_name | VARCHAR(150) | NOT NULL | |
| is_active | BOOLEAN | DEFAULT true | Admin can disable |
| registration_step | SMALLINT | DEFAULT 1 | 1 = basic, 2 = complete |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### roles
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| name | VARCHAR(50) | UNIQUE, NOT NULL | Admin, Teacher, Assistant, Student |
| description | VARCHAR(255) | | |

### user_roles
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| user_id | UUID | FK → users, PK | Composite PK |
| role_id | UUID | FK → roles, PK | Composite PK |
| assigned_at | TIMESTAMPTZ | NOT NULL | |
| assigned_by | UUID | FK → users | Who granted the role |

### devices
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| user_id | UUID | FK → users, NOT NULL | |
| fingerprint | VARCHAR(512) | NOT NULL | Client-generated device hash |
| friendly_name | VARCHAR(100) | | e.g., "Chrome on Windows" |
| last_used_at | TIMESTAMPTZ | | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| **UNIQUE** | | (user_id, fingerprint) | No duplicate devices per user |

### refresh_tokens
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| user_id | UUID | FK → users, NOT NULL | |
| device_id | UUID | FK → devices | |
| token_hash | VARCHAR(512) | NOT NULL | Hashed refresh token |
| expires_at | TIMESTAMPTZ | NOT NULL | |
| revoked_at | TIMESTAMPTZ | | NULL if active |
| created_at | TIMESTAMPTZ | NOT NULL | |

---

## Student Domain

### student_profiles
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| user_id | UUID | FK → users, UNIQUE | 1:1 with users |
| grade | VARCHAR(20) | NOT NULL | e.g., "3rd Secondary" |
| track | VARCHAR(30) | | e.g., "Scientific", "Literary" |
| parent_phone | VARCHAR(20) | | Step 2 data |
| governorate | VARCHAR(50) | | Step 2 data |
| city_district | VARCHAR(80) | | Step 2 data |
| school_name | VARCHAR(120) | | Step 2 data |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

---

## Content Domain

### programs
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| name | VARCHAR(150) | NOT NULL | e.g., "Full Year Platform" |
| description | TEXT | | |
| is_published | BOOLEAN | DEFAULT false | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### packages
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| program_id | UUID | FK → programs | |
| name | VARCHAR(150) | NOT NULL | e.g., "Term 1 Tracker" |
| description | TEXT | | |
| sort_order | INT | DEFAULT 0 | |
| is_published | BOOLEAN | DEFAULT false | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### content_sections
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| package_id | UUID | FK → packages, NOT NULL | |
| name | VARCHAR(150) | NOT NULL | Canonical term; alias "month" |
| description | TEXT | | |
| sort_order | INT | DEFAULT 0 | |
| is_published | BOOLEAN | DEFAULT false | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### lessons
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| content_section_id | UUID | FK → content_sections, NOT NULL | |
| title | VARCHAR(200) | NOT NULL | |
| summary_text | TEXT | | Written text summary |
| sort_order | INT | DEFAULT 0 | |
| is_published | BOOLEAN | DEFAULT false | |
| is_gated | BOOLEAN | DEFAULT true | Teacher-controlled gate toggle |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### lesson_videos
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| lesson_id | UUID | FK → lessons, NOT NULL | |
| provider_type | VARCHAR(30) | NOT NULL | e.g., "youtube" |
| external_video_id | VARCHAR(100) | NOT NULL | YouTube video ID |
| title | VARCHAR(200) | NOT NULL | |
| duration_seconds | INT | NOT NULL | |
| max_watch_minutes | INT | | Nullable = unlimited |
| max_replays | INT | | Nullable = unlimited |
| sort_order | INT | DEFAULT 0 | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### lesson_resources
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| lesson_id | UUID | FK → lessons, NOT NULL | |
| title | VARCHAR(200) | NOT NULL | |
| file_url | VARCHAR(500) | NOT NULL | PDF/file download URL |
| file_type | VARCHAR(30) | | e.g., "pdf", "docx" |
| sort_order | INT | DEFAULT 0 | |
| created_at | TIMESTAMPTZ | NOT NULL | |

---

## Assessment Domain

### exams
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| lesson_id | UUID | FK → lessons | Ties exam to a lesson |
| title | VARCHAR(200) | NOT NULL | |
| pass_threshold | DECIMAL(5,2) | NOT NULL | Percentage, set by Teacher/Assistant |
| time_limit_minutes | INT | | Nullable = no time limit |
| is_published | BOOLEAN | DEFAULT false | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### question_bank_items
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| question_text | TEXT | NOT NULL | |
| question_type | VARCHAR(20) | NOT NULL | "MCQ", "TRUE_FALSE" |
| difficulty | VARCHAR(20) | | "easy", "medium", "hard" |
| topic_tag | VARCHAR(100) | | |
| content_section_id | UUID | FK → content_sections | |
| created_by | UUID | FK → users | |
| created_at | TIMESTAMPTZ | NOT NULL | |
| updated_at | TIMESTAMPTZ | NOT NULL | |

### question_options
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| question_id | UUID | FK → question_bank_items, NOT NULL | |
| option_text | TEXT | NOT NULL | |
| is_correct | BOOLEAN | DEFAULT false | |
| sort_order | INT | DEFAULT 0 | |

### exam_questions
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| exam_id | UUID | FK → exams, PK | Composite PK |
| question_id | UUID | FK → question_bank_items, PK | Composite PK |
| sort_order | INT | DEFAULT 0 | |
| points | DECIMAL(5,2) | DEFAULT 1 | |

### student_exam_attempts
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| exam_id | UUID | FK → exams, NOT NULL | |
| student_id | UUID | FK → users, NOT NULL | |
| score | DECIMAL(5,2) | | Percentage |
| passed | BOOLEAN | | |
| started_at | TIMESTAMPTZ | NOT NULL | |
| submitted_at | TIMESTAMPTZ | | |
| created_at | TIMESTAMPTZ | NOT NULL | |

### student_answers
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| attempt_id | UUID | FK → student_exam_attempts, NOT NULL | |
| question_id | UUID | FK → question_bank_items, NOT NULL | |
| selected_option_id | UUID | FK → question_options | |
| is_correct | BOOLEAN | | Computed on submission |

---

## Access Domain

### code_groups
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| name | VARCHAR(150) | NOT NULL | e.g., "Center X Sept Batch" |
| code_type | VARCHAR(20) | NOT NULL | "PACKAGE", "LESSON" |
| target_id | UUID | NOT NULL | FK to packages.id or lessons.id |
| total_count | INT | NOT NULL | How many codes in this batch |
| validity_days | INT | | Days from activation to expiry |
| generated_by | UUID | FK → users | |
| created_at | TIMESTAMPTZ | NOT NULL | |

### access_codes
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| code_group_id | UUID | FK → code_groups, NOT NULL | |
| code_hash | VARCHAR(512) | UNIQUE, NOT NULL | Hashed alphanumeric code |
| code_display | VARCHAR(20) | | Plaintext for export (encrypted at rest) |
| status | VARCHAR(20) | DEFAULT 'AVAILABLE' | AVAILABLE, ACTIVATED, EXPIRED, REVOKED |
| activated_by | UUID | FK → users | Student who used it |
| activated_at | TIMESTAMPTZ | | |
| expires_at | TIMESTAMPTZ | | Calculated on activation |
| created_at | TIMESTAMPTZ | NOT NULL | |

### student_access_grants
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| student_id | UUID | FK → users, NOT NULL | |
| access_code_id | UUID | FK → access_codes | |
| grant_type | VARCHAR(20) | NOT NULL | "PACKAGE", "LESSON" |
| target_id | UUID | NOT NULL | FK to packages.id or lessons.id |
| granted_at | TIMESTAMPTZ | NOT NULL | |
| expires_at | TIMESTAMPTZ | | |
| is_active | BOOLEAN | DEFAULT true | |

---

## Tracking Domain

### video_watch_events
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| student_id | UUID | FK → users, NOT NULL | |
| lesson_video_id | UUID | FK → lesson_videos, NOT NULL | |
| event_type | VARCHAR(30) | NOT NULL | STARTED, HEARTBEAT, COMPLETED, PAUSED |
| watch_seconds | INT | | Cumulative seconds watched this event |
| playback_speed | DECIMAL(3,1) | | e.g., 1.0, 1.5, 2.0 |
| replay_number | INT | DEFAULT 1 | Which replay this is |
| created_at | TIMESTAMPTZ | NOT NULL | |

### lesson_progress
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| student_id | UUID | FK → users, NOT NULL | |
| lesson_id | UUID | FK → lessons, NOT NULL | |
| status | VARCHAR(20) | DEFAULT 'NOT_STARTED' | NOT_STARTED, IN_PROGRESS, BLOCKED, COMPLETED |
| video_completed | BOOLEAN | DEFAULT false | |
| exam_passed | BOOLEAN | DEFAULT false | |
| manually_unlocked | BOOLEAN | DEFAULT false | Teacher/Admin override |
| unlocked_by | UUID | FK → users | Who performed manual unlock |
| updated_at | TIMESTAMPTZ | NOT NULL | |
| **UNIQUE** | | (student_id, lesson_id) | One record per student per lesson |

---

## Audit Domain

### audit_logs
| Column | Type | Constraints | Notes |
|--------|------|-------------|-------|
| id | UUID | PK | |
| actor_id | UUID | FK → users | Who performed the action |
| action | VARCHAR(50) | NOT NULL | e.g., CODE_GENERATED, CODE_REDEEMED, CONTENT_EDITED, ROLE_CHANGED, MANUAL_UNLOCK, DEVICE_REMOVED |
| entity_type | VARCHAR(50) | | e.g., "AccessCode", "Lesson", "User" |
| entity_id | UUID | | ID of the affected entity |
| metadata | JSONB | | Extra context (old value, new value, etc.) |
| ip_address | VARCHAR(45) | | |
| created_at | TIMESTAMPTZ | NOT NULL | |

---

## Entity Relationship Summary

```
users 1──N user_roles N──1 roles
users 1──1 student_profiles
users 1──N devices
users 1──N refresh_tokens
programs 1──N packages 1──N content_sections 1──N lessons
lessons 1──N lesson_videos
lessons 1──N lesson_resources
lessons 1──N exams
exams N──N question_bank_items (via exam_questions)
question_bank_items 1──N question_options
exams 1──N student_exam_attempts 1──N student_answers
code_groups 1──N access_codes
access_codes 1──1 student_access_grants
users 1──N student_access_grants
users 1──N video_watch_events
users 1──N lesson_progress
users 1──N audit_logs
```
