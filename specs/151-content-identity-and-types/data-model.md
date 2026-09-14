# Data Model: Content Identity and Types

## Internal Code Convention

| Entity | Prefix | Canonical value | Maximum length |
|---|---|---|---:|
| Lesson | `LES-` | `LES-{Id:N}` | 36 |
| LessonVideo | `VID-` | `VID-{Id:N}` | 36 |
| Exam | `EXM-` | `EXM-{Id:N}` | 36 |

Prefixes are disjoint and GUID primary keys are unique. Each table has a unique index on `InternalCode`; the combination guarantees global uniqueness across supported kinds. Codes are assigned once and cannot be updated after persistence.

## Lesson Changes

| Field | Type | Required | Validation/Behavior |
|---|---|---:|---|
| `InternalCode` | string | yes | Max 40; unique index; generated from `Id`; immutable after insert |

No relationships or foreign keys change.

## LessonVideo Changes

| Field | Type | Required | Validation/Behavior |
|---|---|---:|---|
| `InternalCode` | string | yes | Max 40; unique index; generated from `Id`; immutable after insert |
| `VideoTypeId` | Guid | yes | FK to `VideoType.Id`; replacement target must be active |
| `VideoType` | navigation | yes | Many videos to one type |

`VideoTag` remains nullable and deprecated for compatibility. New create/update operations do not write it.

## Exam Changes

| Field | Type | Required | Validation/Behavior |
|---|---|---:|---|
| `InternalCode` | string | yes | Max 40; unique index; generated from `Id`; immutable after insert |

No question, attempt, lesson, or video association changes.

## VideoType

| Field | Type | Required | Validation/Behavior |
|---|---|---:|---|
| `Id` | Guid | yes | Existing `BaseEntity` identity |
| `Name` | string | yes | Trimmed; 2-80 characters |
| `NormalizedName` | string | yes | Invariant normalized name; unique index; never accepted directly from clients |
| `SortOrder` | int | yes | 0-10,000; list order is ascending then name |
| `IsActive` | bool | yes | Active rows are selectable for new/replacement assignments |
| `CreatedAt` | DateTime UTC | yes | Existing `BaseEntity` field |
| `UpdatedAt` | DateTime UTC | no | Updated on rename/reorder/status change |
| `Videos` | collection | no | Assigned `LessonVideo` rows |

### Lifecycle

```text
Create(active or inactive) -> Rename/Reorder -> Activate <-> Deactivate -> Delete only when unused
```

- Assignment count greater than zero blocks deletion.
- Deactivation never clears or changes assignments.
- An existing inactive assignment may survive unrelated video edits.
- Any explicit replacement must target an active row.

## Seed And Legacy Mapping

| Seed name | Active | Sort order | Legacy matches |
|---|---:|---:|---|
| `شرح` | yes | 10 | normalized Arabic/English equivalents of explanation/lesson |
| `واجب` | yes | 20 | homework/assignment equivalents |
| `مراجعة` | yes | 30 | review/revision equivalents |
| `امتحان` | yes | 40 | exam/quiz equivalents |
| `غير مصنف` | no | 999 | null, empty, and every unmatched legacy `VideoTag` |

The migration is deterministic and does not create one catalog row per legacy free-text value.

## Persistence Rules

1. Added lessons, videos, and exams with empty codes receive their canonical code before save.
2. Modified persisted codes cause save rejection; content request DTOs contain no code field.
3. Unique indexes protect same-kind concurrency; disjoint prefixes protect cross-kind collisions.
4. Video create requires an existing active type.
5. Video update preserves its current type when unchanged, including inactive types; a replacement type must be active.
6. Deleting a type uses `Restrict` at the FK and an application count check for a clear error.

## Migration Order

1. Create `video_types` with normalized-name unique index.
2. Insert five deterministic seed rows.
3. Add nullable `InternalCode` columns and nullable `VideoTypeId`.
4. Backfill codes from primary keys.
5. Map known tags and assign fallback to all remaining videos.
6. Verify zero nulls and duplicates.
7. Alter fields to required.
8. Add three unique internal-code indexes and `lesson_videos.VideoTypeId` index/FK with `Restrict` delete behavior.
