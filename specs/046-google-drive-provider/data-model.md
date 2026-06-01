# Data Model: Google Drive Video Provider

No database schema migrations are necessary for this feature, as the existing data model accommodates it natively.

## Entity Updates

### `LessonVideo`

The `LessonVideo` entity already stores standard video provider details. We will utilize the following fields:

- `Provider` (string): Will accept `"google_drive"` in addition to `"youtube"` and `"telegram"`.
- `ProviderVideoId` (string): Will securely store the extracted Google Drive File ID (e.g., `1BxiMVs0XRA...`).

## Application State Updates

### Frontend Forms (Admin)

- The Video Form must be updated to accept a `google_drive` choice.
- Validation logic must be added to parse either a raw 33-character alphanumeric File ID or a full `https://drive.google.com/file/d/...` URL, extracting just the File ID before saving.

### Backend Validation (DTOs)

- The API `CreateVideoCommand` and `UpdateVideoCommand` (or equivalent DTOs) FluentValidation rules must allow `google_drive` as a valid provider type enum/string. 
