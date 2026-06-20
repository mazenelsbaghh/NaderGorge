# Data Model: Teacher Photo Refinement and Bunny Stream

## Relational Schema Analysis

No database schema migrations are required for this feature. We will reuse the existing entity relationships.

### Existing Entities

#### `TeacherPhoto`
Represents the reference photo uploaded for a teacher to be converted into a character by Gemini.
- `TeacherId` (Guid): Foreign key referencing the `User` entity (representing the teacher's user account).
- `FileUrl` (string): Relative URL path (e.g. `/uploads/content/teacher/filename.webp`) to the WebP image.
- `IsActive` (bool): True if the photo is active.
- `UploadedAt` (DateTime): The timestamp when the photo was uploaded.

#### `TeacherProfile`
Represents the teacher's profile details.
- `UserId` (Guid): References the `User` entity.
- Links to `Package` entities.

#### `Package`
- `TeacherId` (Guid): References `TeacherProfile` (which in turn maps to a `User` via `UserId`).

#### `Lesson`
- `ContentSectionId` (Guid): References `ContentSection`.

#### `ContentSection`
- `TermId` (Guid): References `Term`.

#### `Term`
- `PackageId` (Guid): References `Package`.

#### `LessonVideo`
- `LessonId` (Guid): References `Lesson`.
- `Provider` (string): The video provider (e.g. `"youtube"`, `"bunny"`, `"vk"`).
- `ProviderVideoId` (string): Stores the video ID or GUID.

---

## Entity Relationship Path

To find the teacher's User ID (`TeacherId` on `TeacherPhoto`) from a `LessonVideo`, we traverse the following path:
`LessonVideo` -> `Lesson` -> `ContentSection` -> `Term` -> `Package` -> `Teacher` (`TeacherProfile`) -> `UserId`.

### LINQ Query Path
```csharp
var teacherUserId = await _db.LessonVideos
    .Where(v => v.Id == videoId)
    .Select(v => (Guid?)v.Lesson.ContentSection.Term.Package.Teacher.UserId)
    .FirstOrDefaultAsync(ct);
```
This resolves the teacher's `User.Id`, which is then used to filter the `TeacherPhotos` table:
```csharp
var teacherPhotoUrl = await _db.TeacherPhotos
    .Where(tp => tp.IsActive && (teacherUserId == null || tp.TeacherId == teacherUserId))
    .OrderByDescending(tp => tp.UploadedAt)
    .Select(tp => tp.FileUrl)
    .FirstOrDefaultAsync(ct);
```
