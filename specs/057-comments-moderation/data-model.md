# Data Model: Lesson Comments Moderation

## Entities

### `LessonComment`
Represents a student-authored comment attached to a lesson and subject to moderation before public visibility.

**Fields**:
- `Id` (Guid, PK)
- `LessonId` (Guid, FK to `Lesson`)
- `AuthorUserId` (Guid, FK to `User`)
- `Body` (string, required, bounded length)
- `Status` (Enum/Int): `Pending = 0`, `Approved = 1`, `Rejected = 2`
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime, nullable if immutable after creation)
- `ReviewedAt` (DateTime, nullable)
- `ReviewedByUserId` (Guid, FK to `User`, nullable)

**Relationships**:
- Belongs to one `Lesson`
- Belongs to one author `User`
- Optionally references one reviewer `User`

### `LessonCommentModerationView`
Read-model concept used in the teacher/admin lesson cockpit to display comments together with lesson and author context.

**Fields**:
- `CommentId`
- `LessonId`
- `LessonTitle`
- `StudentId`
- `StudentName`
- `CommentBody`
- `Status`
- `CreatedAt`
- `ReviewedAt`
- `ReviewedByName`

**Relationships**:
- Derived from `LessonComment`, `Lesson`, and `User`

## State Transitions

- **Pending -> Approved**: Comment becomes publicly visible under the lesson page. `ReviewedAt` and `ReviewedByUserId` are populated.
- **Pending -> Rejected**: Comment remains hidden from the public lesson page. `ReviewedAt` and `ReviewedByUserId` are populated.
- **Approved / Rejected -> same state**: Idempotent re-submission of the same moderation action should not create duplicate side effects.

## Validation Rules

- A comment cannot be created unless the author has access to the lesson.
- `Body` must contain non-whitespace text.
- `Body` must respect a bounded maximum length suitable for lesson discussion content.
- Only `Pending` comments can be newly approved or rejected in v1.
- Public lesson comment queries must return only `Approved` comments.
- Moderation queries must include all states needed by the teacher view, with at minimum `Pending`, `Approved`, and `Rejected`.

## DTOs

### `LessonCommentDto`
- `Id`
- `LessonId`
- `AuthorName`
- `Body`
- `Status`
- `CreatedAt`
- `IsOwnComment`

Used for student-facing reads where approved comments are visible publicly and a student may also need feedback about the status of comments they authored.

### `CreateLessonCommentRequest`
- `Body`

### `CreateLessonCommentResponse`
- `Id`
- `Status`
- `CreatedAt`
- `Message`

### `ModerationLessonCommentDto`
- `Id`
- `LessonId`
- `LessonTitle`
- `StudentId`
- `StudentName`
- `Body`
- `Status`
- `CreatedAt`
- `ReviewedAt`
- `ReviewedByName`

### `ModerateLessonCommentResponse`
- `Id`
- `Status`
- `ReviewedAt`
- `ReviewedByUserId`
