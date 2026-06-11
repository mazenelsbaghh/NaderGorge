# Data Model: Teacher Image WebP Conversion

There are no schema modifications required for this feature, as both the teacher profile image and additional photos are already stored as string URLs.

## Affected Entities

### 1. `TeacherProfile` (NaderGorge.Domain)
- **Attribute**: `ProfileImageUrl` (string, optional)
- **Role**: Refers to the relative URL of the teacher's profile picture, e.g., `/uploads/teacher/unique-id_filename.webp`.

### 2. `TeacherPhoto` (NaderGorge.Domain)
- **Attribute**: `FileUrl` (string, required)
- **Role**: Refers to the relative URL of the teacher's AI analysis photo, e.g., `/uploads/teacher/unique-id_filename.webp`.

## Storage Location
- **Path**: `/wwwroot/uploads/teacher/` on the backend directory.
- **Nginx Mapping**: In production, Nginx serves this path statically under `assets.massar-academy.net/uploads/teacher/`.
