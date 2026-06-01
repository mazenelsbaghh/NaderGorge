# Quickstart: Google Drive Custom Player

## 1. Environment Variable Setup

To enable robust streaming of large Google Drive videos (bypassing the virus scan wall) without consuming server bandwidth, you can optionally provide a Google API Key with Drive API enabled.

Add the following to your `frontend/.env.local`:

```bash
# Optional but highly recommended for large Google Drive Videos
GOOGLE_DRIVE_API_KEY="AIzaSyYourApiKeyHere..."
```

**Note**: If not provided, the system will fall back to using `https://drive.google.com/uc?export=download&id=...` which works well for smaller videos.

## 2. Testing the Google Drive Player

1. Navigate to the Admin Dashboard > Add Lesson Video.
2. Select "Google Drive" and paste a Google Drive link.
3. Open a student account and navigate to the lesson.
4. Verify that the video loads within the **Custom UI** (custom play button, progress bar) instead of the native Google Drive UI.
5. Verify that scrubbing and toggling full-screen works seamlessly.
6. Open browser DevTools -> Network. You should see a `302 Redirect` from `/api/video/drive-proxy` to `googleapis.com` or `drive.google.com`. The source in the DOM (`<video src="...">`) should remain obfuscated.
