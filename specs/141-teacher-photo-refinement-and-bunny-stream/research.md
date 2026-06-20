# Technical Research: Teacher Photo Refinement and Bunny Stream

## 1. Teacher Photo Retrieval

### Current State
- The backend has `UploadTeacherPhotoCommand` which saves reference photos to `TeacherPhotos` table.
- There is no query endpoint to get the currently active photo for a specific teacher.
- The `TeacherPhoto` entity matches `TeacherId` to `User.Id` of the teacher.
- When generating chapters or mindmaps, the backend queries `TeacherPhotos` globally and takes the latest active photo across ALL teachers, which is a bug.

### Decision
- Add `GetActiveTeacherPhotoQuery` returning `ApiResponse<ActiveTeacherPhotoDto>` with the `FileUrl` of the active photo for a given `TeacherId`.
- Add GET endpoints:
  - `GET /api/admin/teachers/{teacherId}/active-photo` (AdminController, requires content.manage permission).
  - `GET /api/teacher/profile/active-photo` (TeacherController, requires teacher authentication).
- Modify the command handlers (`AnalyzeVideoAICommandHandler`, `GenerateChapterMindmapsCommandHandler`, `RegenerateChapterMindmapCommandHandler`) to retrieve the `UserId` of the teacher who owns the lesson/video and filter `TeacherPhotos` by that `TeacherId`.

### Alternatives considered
- Adding `ActiveTeacherPhotoUrl` directly on `TeacherProfile` or `User` model.
  *Rejected*: This would require a database schema migration. Retrieving from `TeacherPhotos` table by sorting by `UploadedAt` is simple and fits the existing schema without migrations.

---

## 2. Bunny Stream Video Audio Extraction

### Current State
- When `video.Provider` is `"bunny"`, `sourceUrl` passed to the worker is the Bunny video GUID.
- The worker's `extractAudioFromVideo` currently only handles YouTube URLs (or raw IDs) and has Telegram / Cobalt / local yt-dlp flows.
- Running yt-dlp directly on a GUID fails because it's not a valid URL.

### Decision
- Detect if `sourceUrl` matches the pattern of a Bunny video GUID (alphanumeric with hyphens, length 32 to 36).
- If it is a Bunny GUID, construct the iframe embed URL:
  `https://iframe.mediadelivery.net/embed/${process.env.BUNNY_STREAM_LIBRARY_ID}/${sourceUrl}`
- Configure the worker's environment in `docker-compose.yml` to receive `BUNNY_STREAM_LIBRARY_ID` and `BUNNY_STREAM_API_KEY`.
- Pass `--referer "https://admin.massar-academy.net/"` to `yt-dlp` command arguments when downloading Bunny Stream videos.

### Alternatives considered
- Fetching direct MP4 fallback URL via Bunny API (`GET /library/{libraryId}/videos/{videoId}`).
  *Rejected*: This requires the MP4 fallback option to be manually enabled on the Bunny Stream Library. Using the iframe embed URL via `yt-dlp` works out-of-the-box for all libraries because the player HLS stream is always accessible with the correct referrer.
