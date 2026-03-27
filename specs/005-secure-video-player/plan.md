# Implementation Plan: Secure Video Player

**Feature**: 005-secure-video-player
**Spec**: [spec.md](./spec.md)
**Date**: 2026-03-25
**Status**: Ready for Implementation

---

## Technical Context

| Aspect              | Current State                                         |
|---------------------|-------------------------------------------------------|
| **Backend**         | .NET 8 / C# / MediatR / EF Core / PostgreSQL         |
| **Frontend**        | Next.js 15 / React / TypeScript / Framer Motion       |
| **Auth**            | JWT-based with role claims (Admin, Student)           |
| **Video Storage**   | `LessonVideo` entity with `ProviderVideoId` (YouTube ID) |
| **Video Delivery**  | `IVideoProvider.GetEmbedUrl()` returns plaintext YouTube embed URL |
| **Watch Tracking**  | `VideoWatchEvent` entity with `WatchCount` and `IsLocked` |
| **Design Theme**    | Pharaonic (sand/gold/papyrus CSS variables)           |

---

## Architecture Overview

```
┌─────────────────────────────────┐
│         Frontend (Next.js)       │
│                                  │
│  ┌──────────────────────────┐   │
│  │  SecureVideoPlayer       │   │
│  │  ┌────────────────────┐  │   │
│  │  │ Custom Controls     │  │   │
│  │  │ (play, seek, vol)  │  │   │
│  │  ├────────────────────┤  │   │
│  │  │ YouTube IFrame      │  │   │
│  │  │ (injected via JS)   │  │   │
│  │  └────────────────────┘  │   │
│  └──────────────────────────┘   │
│              │                   │
│    POST /video-session           │
│              │                   │
└──────────────┼───────────────────┘
               │
┌──────────────┼───────────────────┐
│         Backend (.NET 8)         │
│              │                   │
│  ┌───────────▼──────────────┐   │
│  │ VideoSessionController    │   │
│  │ - Validate auth + access  │   │
│  │ - Check watch limit       │   │
│  │ - Encrypt video ID        │   │
│  │ - Return token + key      │   │
│  └──────────────────────────┘   │
│              │                   │
│  ┌───────────▼──────────────┐   │
│  │ VideoPlaybackSession      │   │
│  │ (new entity in DB)        │   │
│  └──────────────────────────┘   │
└──────────────────────────────────┘
```

---

## Implementation Phases

### Phase 1: Backend — Video Session System (Priority: Critical)

#### Task 1.1: Create VideoPlaybackSession Entity
- **File**: `Domain/Entities/VideoPlaybackSession.cs`
- **Fields**: Id, UserId, LessonVideoId, SessionToken, EncryptionKey, CreatedAt, ExpiresAt, IsConsumed, IpAddress
- **Add to**: `IAppDbContext` interface and `AppDbContext`

#### Task 1.2: Create EF Migration
- Run `dotnet ef migrations add AddVideoPlaybackSession`

#### Task 1.3: Create Encryption Service
- **File**: `Application/Common/IVideoEncryptionService.cs`
- **File**: `Infrastructure/Services/VideoEncryptionService.cs`
- Methods:
  - `EncryptVideoId(string videoId, string key) → string encryptedToken`
  - `GenerateSessionKey() → string key`
- Use AES-256-GCM for encryption with random IV

#### Task 1.4: Create Video Session Command/Query
- **File**: `Application/Features/Student/Commands/CreateVideoSessionCommand.cs`
- Input: LessonVideoId, UserId
- Validation:
  1. Check user has access to the lesson's package
  2. Check watch count < max watch count
  3. Check no unexpired active session exists for this video+user
- Output: SessionId, EncryptedToken, SessionKey, ExpiresAt, WatchInfo

#### Task 1.5: Create Video Session Controller
- **File**: `API/Controllers/VideoSessionController.cs`
- `POST /api/student/video-session` → CreateVideoSessionCommand
- `POST /api/student/video-session/{id}/consume` → ConsumeVideoSessionCommand
- Requires `[Authorize(Roles = "Student")]`

#### Task 1.6: Modify GetLessonDetailQuery
- **Remove** `embedUrl` from `VideoDto` response
- The `IVideoProvider.GetEmbedUrl()` call is no longer needed in this query
- Keep other fields: Id, Title, Provider, Order, Limit, Watched, IsLocked

---

### Phase 2: Frontend — Secure Video Player Component (Priority: Critical)

#### Task 2.1: Create Video Session Service
- **File**: `services/video-session-service.ts`
- Methods:
  - `createSession(lessonVideoId: string): Promise<VideoSession>`
  - `consumeSession(sessionId: string): Promise<void>`
- Types: `VideoSession { sessionId, token, key, expiresAt, provider, watchInfo, videoTitle }`

#### Task 2.2: Create Video Decryption Utility
- **File**: `utils/video-crypto.ts`
- Use Web Crypto API (SubtleCrypto) for AES-256-GCM decryption
- `decryptVideoId(encryptedToken: string, key: string): string`
- Clear the decrypted value from memory after use (overwrite variable)

#### Task 2.3: Create SecureVideoPlayer Component
- **File**: `components/video/SecureVideoPlayer.tsx`
- Responsibilities:
  1. Call video session service to get encrypted token
  2. Decrypt the video ID
  3. Dynamically inject YouTube IFrame via `document.createElement`
  4. Use Shadow DOM to isolate the player
  5. After load: clear decrypted data, obfuscate `src` attribute
  6. Display custom pharaonic-themed controls overlay
  7. Handle play/pause/seek/volume/fullscreen/speed via YouTube IFrame API

#### Task 2.4: Create Custom Player Controls
- **File**: `components/video/PlayerControls.tsx`
- Pharaonic-themed control bar:
  - Play/Pause (toggle icon)
  - Progress/seek bar (gold accent)
  - Volume slider
  - Speed selector (0.5x–2x dropdown)
  - Fullscreen toggle
  - Watch count indicator ("المشاهدة 1 من 3")
  - Lesson title display

#### Task 2.5: Create Anti-Inspection Shield
- **File**: `utils/dom-shield.ts`
- Functions:
  - `disableContextMenu(element)` — prevent right-click
  - `obfuscateIframeSrc(iframe)` — replace src with blob URL
  - `createMutationGuard(element)` — watch for DevTools modifications
  - `preventDrag(element)` — disable drag-and-drop

#### Task 2.6: Create Lesson Video Page
- **File**: `app/student/packages/[packageId]/lessons/[lessonId]/page.tsx`
- Layout:
  1. Video player (top, 16:9 aspect ratio)
  2. Lesson info panel (title, summary)
  3. Resources list (PDFs, etc.)
- Fetch lesson detail → render SecureVideoPlayer for each video

---

### Phase 3: Integration & Polish (Priority: High)

#### Task 3.1: Update Content Service
- Remove `embedUrl` from frontend lesson detail types
- Update any existing video display components to use SecureVideoPlayer

#### Task 3.2: Watch Tracking Integration
- After 30s of playback, call existing `POST /api/tracking/video-event`
- Update watch count display in real-time
- Show lock state when limit reached

#### Task 3.3: Responsive Design
- Ensure player works on mobile (touch controls)
- Tablet layout with appropriate sizing
- Desktop fullscreen support

#### Task 3.4: Error Handling & Edge Cases
- Session expired → show "انتهت الجلسة، اضغط للتحديث"
- Network error → retry with exponential backoff
- Watch limit reached → show lock message with admin contact
- Unauthorized access → redirect to login

---

## File Manifest

### New Files (Backend)
| File | Purpose |
|------|---------|
| `Domain/Entities/VideoPlaybackSession.cs` | New entity |
| `Application/Common/IVideoEncryptionService.cs` | Encryption interface |
| `Infrastructure/Services/VideoEncryptionService.cs` | AES-256 encryption |
| `Application/Features/Student/Commands/CreateVideoSessionCommand.cs` | Session creation |
| `Application/Features/Student/Commands/ConsumeVideoSessionCommand.cs` | Token consumption |
| `API/Controllers/VideoSessionController.cs` | API endpoints |

### New Files (Frontend)
| File | Purpose |
|------|---------|
| `services/video-session-service.ts` | API client for sessions |
| `utils/video-crypto.ts` | AES decryption |
| `utils/dom-shield.ts` | Anti-inspection utilities |
| `components/video/SecureVideoPlayer.tsx` | Main player component |
| `components/video/PlayerControls.tsx` | Custom control bar |
| `app/student/packages/[packageId]/lessons/[lessonId]/page.tsx` | Lesson video page |

### Modified Files
| File | Change |
|------|--------|
| `Domain/Interfaces/IAppDbContext.cs` | Add `DbSet<VideoPlaybackSession>` |
| `Infrastructure/Persistence/AppDbContext.cs` | Add `VideoPlaybackSessions` DbSet |
| `Application/Features/Content/Queries/GetLessonDetailQuery.cs` | Remove `embedUrl` from response |
| `components/content/SectionList.tsx` | Already links to lesson page ✅ |

---

## Security Considerations

1. **Token Lifetime**: 5 minutes — short enough to prevent sharing, long enough for slow connections
2. **Single-Use**: Each token can only be consumed once
3. **IP Binding**: Optional — log IP for audit, consider binding token to IP
4. **Rate Limiting**: Max 10 session requests per minute per user
5. **Encryption**: AES-256-GCM with random IV per session
6. **Audit Trail**: All session creations and consumptions are logged

---

## Testing Strategy

### Unit Tests
- VideoEncryptionService: encrypt/decrypt roundtrip
- CreateVideoSessionCommand: permission checks, watch limit validation
- ConsumeVideoSessionCommand: expired token, already consumed

### Integration Tests
- Full flow: create session → decrypt → consume
- Unauthorized access returns 403
- Watch limit enforcement

### E2E Tests
- Student plays video through custom player
- Video URL not visible in page source
- Watch count increments after playback
- Lock state enforced after max watches

---

## Dependencies

- **YouTube IFrame API**: Loaded as external script
- **Web Crypto API**: Built into modern browsers (no polyfill needed)
- **Existing Infrastructure**: Auth, `IAccessCheckService`, `VideoWatchEvent` tracking

---

## Estimated Effort

| Phase | Tasks | Estimate |
|-------|-------|----------|
| Phase 1: Backend | 6 tasks | 4-6 hours |
| Phase 2: Frontend | 6 tasks | 6-8 hours |
| Phase 3: Integration | 4 tasks | 3-4 hours |
| **Total** | **16 tasks** | **13-18 hours** |
