# Quickstart: Teacher Photo Refinement and Bunny Stream

## 1. Environment Setup

Ensure the following variables are defined in your `.env` file at the project root:

```bash
BUNNY_STREAM_LIBRARY_ID=your_bunny_library_id
BUNNY_STREAM_API_KEY=your_bunny_api_key
```

## 2. Docker Services Execution

To apply the updated environment variables to the backend and worker containers:

```bash
# Rebuild and start the containers
make up
```

## 3. Manual Verification Steps

### Teacher Active Photo Verification
1. Log in to the Admin Panel (`admin.massar-academy.net`).
2. Go to `/admin/teachers`.
3. Edit a teacher who already has an AI reference photo. Verify that the reference photo preview loads automatically.
4. Go to `/admin/ai-monitor`. Select a teacher from the dropdown list. Verify that their active reference photo loads automatically. Upload a new photo and save; verify it is saved specifically for that teacher.

### Bunny Stream Audio Extraction Verification
1. Trigger AI Analysis on a lesson video that uses provider `"bunny"`.
2. Inspect the worker container logs:
   ```bash
   docker compose logs -f worker
   ```
3. Verify that the worker downloads the video via `yt-dlp` using the constructed iframe URL and Referer `https://admin.massar-academy.net/`.
4. Verify that a `.mp3` audio track is successfully generated in the worker's `.tmp` directory.
