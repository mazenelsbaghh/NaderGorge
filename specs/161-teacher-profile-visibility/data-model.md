# Data Model: Teacher Profile & Content Visibility

## TeacherProfile

Existing entity extended with:

| Field | Type | Default | Meaning |
|---|---|---:|---|
| `IsVisibleToStudents` | boolean | `true` | Teacher can appear in student/visitor teacher discovery and public profile responses. |
| `IsContentVisibleToStudents` | boolean | `true` | Teacher-owned content can appear and be accessed by students/visitors. |

Existing `IsPublicProfileEnabled` remains a separate profile feature unless the implementation proves it is the current canonical public gate; the new feature must not silently change its established meaning. `ShowOnLanding` remains the landing-only control.

Relationships remain unchanged: `TeacherProfile.UserId` → `User.Id`; packages, exams, community posts, shared package links, and teacher subjects resolve ownership through `TeacherProfile` or their existing teacher foreign key.

## User

Admin update may change supported non-secret identity fields:

- `FullName`
- `PhoneNumber`/login identifier, subject to uniqueness and normalization rules
- `IsActive`/account status only if the existing teacher-management contract exposes it and the Admin explicitly changes it
- `PasswordHash` is write-only input; never part of read DTOs
- `PasswordResetVersion` and `SecurityStampVersion` advance when credentials/authorization require invalidation

## Visibility resolution

1. If a request is an Admin/teacher operational request covered by existing authorization, teacher/content admin access remains available.
2. For visitor/student discovery, teacher rows require `User.IsActive`, not deleted, and `TeacherProfile.IsVisibleToStudents`.
3. For teacher-owned public/student content, the ownership root must have `IsContentVisibleToStudents` and the content's own existing active/published state.
4. For protected access, the shared access check must require teacher-content visibility before honoring a matching historical grant.
5. A hidden teacher and hidden content are independent; no cascade write is performed.

## State transitions

```text
Visible ──Admin hide──> Hidden
Hidden  ──Admin show──> Visible
```

Both states are idempotent. A transition never deletes purchases, grants, financial records, academic records, content, or audit history.

## Audit data

Every successful update records `User`, `TeacherProfile`, and/or visibility changes with actor and target IDs, old/new non-secret values, correlation ID, and timestamp. Password hashes and submitted password values are excluded.

## Cache/realtime implications

Teacher profile changes emit existing staff scopes (`users`, `subjects`, and content-related invalidation as appropriate) and frontend invalidation keys for teacher lists/details, public teacher lists/details, content/package lists, and protected access queries. Public/student caches must not serve a hidden response after the mutation succeeds.
