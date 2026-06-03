# Research: Removing Bunny and Telegram Video Providers

## Finding References to Telegram and Bunny

### Backend:
- No classes under `Providers` exist for Bunny or Telegram.
- `Program.cs` registers `YouTubeVideoProvider` and `VkVideoProvider`.
- The database contains one record with provider `bunny` ("بانييي" with a mediadelivery.net URL).

### Frontend:
- `AddVideoForm.tsx` has `bunny` dropdown option and resolves mediadelivery.net URLs to `bunny` provider.
- `SecureVideoPlayer.tsx` implements a custom player for `telegram` using `<video>` and `/api/video/stream-proxy`.
- `/app/api/video/stream-proxy/route.ts` proxies telegram streaming chunks.
- `LessonVideoList.tsx` has a fallback string `"Bunny"` for display.

## Resolution Approach
- Update DB records via migrations.
- Remove Bunny selection and URL parsing in admin forms.
- Remove Telegram player from `SecureVideoPlayer.tsx`.
- Delete the stream-proxy route.
- Add validations in the backend command handlers.
