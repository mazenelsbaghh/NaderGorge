# Tasks: Remove Bunny and Telegram Video Providers

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`)
- [x] Phase 2: Technical Planning (`speckit-plan`)
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`)

---

## Technical Tasks

### Task 1 - Backend Provider Validations (Priority: High)
- [ ] T001 [MODIFY] [AdminContentCommands.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Admin/Commands/AdminContentCommands.cs):
  - In `CreateVideoCommandHandler.Handle`, add validation: if the `request.Provider` is not `"youtube"` and not `"vk"` (case-insensitive), return `ApiResponse<Guid>.Fail("Invalid provider. Supported: youtube, vk")`.
  - In `UpdateVideoCommandHandler.Handle`, add validation: if the `request.Provider` is not `"youtube"` and not `"vk"` (case-insensitive), return `ApiResponse.Fail("Invalid provider. Supported: youtube, vk")`.
- [ ] T002 [MODIFY] [CreateVideoSessionCommand.cs](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/backend/src/NaderGorge.Application/Features/Student/Commands/CreateVideoSessionCommand.cs):
  - In `CreateVideoSessionCommandHandler.Handle`, add validation: if the `video.Provider` is not `"youtube"` and not `"vk"` (case-insensitive), return `ApiResponse<VideoSessionDto>.Fail("Invalid video provider", new List<string> { "INVALID_PROVIDER" })`.

### Task 2 - Database Migration (Priority: High)
- [ ] T003 [RUN] Add a new C# EF Core Migration:
  - Command: `make migrate-add NAME=RemoveBunnyTelegramProviders`
  - In the generated migration file's `Up` method, add a SQL command to update `lesson_videos` records:
    ```csharp
    migrationBuilder.Sql("UPDATE lesson_videos SET \"Provider\" = 'YouTube', \"ProviderVideoId\" = '2LfJcOt7Zhs' WHERE LOWER(\"Provider\") = 'bunny' OR LOWER(\"Provider\") = 'telegram';");
    ```
  - Apply the migration locally: `make migrate`
  - Run the migration on the remote server database:
    `python3 scratch/ssh_cmd.py "cd /var/www/nadergorge && docker compose --profile migration run --rm migrator"`

### Task 3 - Frontend Admin Panel Changes (Priority: Medium)
- [ ] T004 [MODIFY] [AddVideoForm.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/AddVideoForm.tsx):
  - Remove `<option value="bunny">Bunny</option>` and `<option value="telegram">Telegram</option>` if present from the provider select options.
  - In url validation/resolution, check that automatic resolution only handles YouTube and VK patterns. Remove Bunny or Telegram resolution logic.
- [ ] T005 [MODIFY] [LessonVideoList.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/admin/LessonVideoList.tsx):
  - Clean up fallback strings to display `"YouTube"` or `"VK"` and remove Bunny/Telegram references.

### Task 4 - Frontend Video Player & Proxy Cleanup (Priority: High)
- [ ] T006 [DELETE] [stream-proxy route](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/app/api/video/stream-proxy/route.ts):
  - Delete this file since it is only used by the Telegram player.
- [ ] T007 [MODIFY] [SecureVideoPlayer.tsx](file:///Users/mazenelsbagh/mazen%20mac/apps/nader%20gorge/frontend/src/components/video/SecureVideoPlayer.tsx):
  - Remove the branch `if (session.provider?.toLowerCase() === 'telegram')` and its inner implementation (the native `<video>` element setup, proxy src creation, event listeners, telegram watermark, and applyDomShields wrapper).
  - Clean up `nativeVideoRef` and telegram control wrappers in `sendCommand`.
  - Fix default placeholder image 404 by downloading/generating a nice placeholder.

### Task 5 - System Build & Verification (Priority: High)
- [ ] T008 [RUN] Validate that both frontend and backend build cleanly:
  - Run `dotnet build` in `backend`
  - Run `npm run build` in `frontend`
- [ ] T009 [RUN] Deploy the changes to the production server and verify the player works for YouTube/VK.
