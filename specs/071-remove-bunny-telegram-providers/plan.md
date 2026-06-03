# Implementation Plan: Remove Bunny and Telegram Video Providers

**Branch**: `071-remove-bunny-telegram-providers` | **Date**: 2026-06-03 | **Spec**: [spec.md](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/specs/071-remove-bunny-telegram-providers/spec.md)

## Summary

The goal of this task is to completely remove all traces of Bunny and Telegram video providers from both the frontend and backend codebase, and ensure only YouTube and VK remain as supported video providers. Additionally, we need to migrate any existing videos using the "bunny" or "telegram" provider in the database to "YouTube" to prevent runtime 500/404 errors.

## Technical Context

- **Language/Version**: C# (.NET 9) Backend, TypeScript (Next.js 16) Frontend
- **Storage**: PostgreSQL (LessonVideo entity)
- **Testing**: Frontend Axios client and Backend MediatR tests
- **Target Platform**: Docker-deployed Linux server / local developer machines

## Proposed Changes

### Database Migration

We will create a new EF Core migration on the backend that:
- Updates any existing records in the `lesson_videos` table where the `Provider` is `bunny` or `telegram` (case-insensitive) to `youtube`.
- Updates their `ProviderVideoId` to a working fallback YouTube ID (e.g., `2LfJcOt7Zhs`) to prevent playback failures.

---

### Backend Components

#### [MODIFY] [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs)
- Update `CreateVideoCommandHandler.Handle` to validate that `request.Provider` is either `"youtube"` or `"vk"` (case-insensitive). Return `ApiResponse<Guid>.Fail("Invalid provider")` if validation fails.
- Update `UpdateVideoCommandHandler.Handle` to perform the same validation.

#### [MODIFY] [CreateVideoSessionCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs)
- Update `CreateVideoSessionCommandHandler.Handle` to validate that `video.Provider` is either `"youtube"` or `"vk"` (case-insensitive). Return `ApiResponse<VideoSessionDto>.Fail("Invalid video provider", new List<string> { "INVALID_PROVIDER" })` if validation fails.

---

### Frontend Components

#### [MODIFY] [AddVideoForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AddVideoForm.tsx)
- Remove `bunny` option from the dropdown list.
- Update automatic URL resolution to only handle YouTube and VK patterns.

#### [MODIFY] [SecureVideoPlayer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/video/SecureVideoPlayer.tsx)
- Remove the `telegram` video player implementation.
- Remove native video element reference and state handlers for `telegram`.
- Clean up watermark and DOM protection overrides specific to the telegram player.

#### [DELETE] [stream-proxy route](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/api/video/stream-proxy/route.ts)
- Delete the stream-proxy route, as it was only used for telegram direct streaming.

---

## Verification Plan

### Automated Tests
- Scaffold and run the EF Core database migrations.
- Run `npm run build` in the frontend and `dotnet build` in the backend.

### Manual Verification
- Verify in the admin panel that only YouTube and VK are available.
- Check that the video player successfully renders YouTube and VK videos.
- Verify that the video originally configured with Bunny is migrated to YouTube and plays correctly.
