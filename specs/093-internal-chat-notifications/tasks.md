# Tasks: Internal Chat, Workrooms, and Real-Time Notifications

## Spec Kit Preparation Workflow

- [x] Phase 1: Feature Specification (`speckit-specify`) completed
- [x] Phase 2: Technical Planning (`speckit-plan`) completed
- [x] Phase 3: Detailed Task Breakdown (`speckit-tasks`) completed

---

## Technical Implementation Checklist

### 1. Database & Domain Models (Backend)

- [x] **Task 1.1**: Add chat enums in `backend/src/NaderGorge.Domain/Enums/`:
  - `ChatRoomType.cs`: `Individual = 0`, `Group = 1`, `Workroom = 2`
  - `ChatMessageType.cs`: `Text = 0`, `Attachment = 1`, `System = 2`
- [x] **Task 1.2**: Create `ChatRoom.cs` entity in `backend/src/NaderGorge.Domain/Entities/` with properties:
  - `Id`, `Name` (nullable), `Type` (`ChatRoomType`), `TaskItemId` (nullable FK), `IsArchived`, `CreatedAt`, `CreatedByUserId`, navigation properties `ChatParticipants` and `ChatMessages`.
- [x] **Task 1.3**: Create `ChatParticipant.cs` entity in `backend/src/NaderGorge.Domain/Entities/`:
  - `ChatRoomId` (FK), `UserId` (FK), `JoinedAt`, `LastReadMessageId` (nullable FK). Configure composite key `(ChatRoomId, UserId)`.
- [x] **Task 1.4**: Create `ChatMessage.cs` entity in `backend/src/NaderGorge.Domain/Entities/`:
  - `Id`, `ChatRoomId` (FK), `SenderUserId` (FK), `Content`, `Type` (`ChatMessageType`), `MediaUrl` (nullable), `MediaMetadata` (nullable string/JSONB), `IsPinned`, `CreatedAt`.
- [x] **Task 1.5**: Create `ChatMessageReadState.cs` entity in `backend/src/NaderGorge.Domain/Entities/`:
  - `MessageId` (FK), `UserId` (FK), `ReadAt`. Configure composite key `(MessageId, UserId)`.
- [x] **Task 1.6**: Register DbSet properties for new entities in `IAppDbContext.cs`.
- [x] **Task 1.7**: Register and configure fluent mappings for new entities in `backend/src/NaderGorge.Infrastructure/Data/AppDbContext.cs`:
  - Setup composite keys, Indexes for foreign keys (`ChatRoomId`, `SenderUserId`, `CreatedAt`), cascade delete configuration for all FK relationships.
- [x] **Task 1.8**: Generate EF Core migration and apply to local database:
  - Run `dotnet ef migrations add AddChatEntities --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`
  - Run `dotnet ef database update --project src/NaderGorge.Infrastructure --startup-project src/NaderGorge.API`

### 2. MediatR Commands & Queries (Backend)

- [x] **Task 2.1**: Implement MediatR commands in `backend/src/NaderGorge.Application/Features/Internal/Commands/`:
  - `CreateChatRoomCommand.cs`: Handles creating a Group or Individual room. Verifies direct chat room uniqueness.
  - `SendChatMessageCommand.cs`: Saves a message. Parses mentions (`@username`), writes notifications to `NotificationEvents` table.
  - `MarkRoomReadCommand.cs`: Updates participant's `LastReadMessageId` and inserts read states.
  - `TogglePinMessageCommand.cs`: Toggles pin state of a message in a room.
  - `ArchiveChatRoomCommand.cs`: Sets `IsArchived` to `true` on the room (restricted to Admin/Supervisor roles).
- [x] **Task 2.2**: Implement MediatR queries in `backend/src/NaderGorge.Application/Features/Internal/Queries/`:
  - `GetChatRoomsQuery.cs`: Retrieves the list of active chat rooms for the current user, including last message preview, unread counts, and task details if workroom.
  - `GetChatRoomMessagesQuery.cs`: Retrieves messages history for a specific room (paginated).
- [x] **Task 2.3**: Implement operations task-linked automatic room generation in `backend/src/NaderGorge.Application/Features/Operations/Commands/CreateTaskCommandHandler.cs`:
  - Hook into task creation to raise a domain event, or directly invoke chat creation to spawn a `Workroom` automatically.

### 3. SignalR Hub & API Controllers (Backend)

- [x] **Task 3.1**: Create `ChatHub.cs` in `backend/src/NaderGorge.API/Hubs/`:
  - Extract JWT token from Query String to authenticate the connecting user.
  - Group management: On connection, check user's rooms and add them to SignalR group IDs (`Room_{roomId}`).
  - Methods: `SendMessage` (calls `SendChatMessageCommand` MediatR handler and broadcasts to group), `Typing` (broadcasts user typing state).
- [x] **Task 3.2**: Register SignalR Hub in `backend/src/NaderGorge.API/Program.cs`:
  - Add `builder.Services.AddSignalR();`
  - Configure routing: `app.MapHub<ChatHub>("/hubs/chat");`
  - Add query-token extraction inside JWT Bearer Options configuration.
- [x] **Task 3.3**: Create `InternalChatController.cs` in `backend/src/NaderGorge.API/Controllers/`:
  - Expose endpoints for `GET /api/chat/rooms`, `GET /api/chat/rooms/{roomId}/messages`, `POST /api/chat/rooms`, `POST /api/chat/rooms/{roomId}/archive`, `POST /api/chat/messages/{messageId}/pin`, and `POST /api/chat/rooms/{roomId}/read`.
  - Apply `[Authorize]` attributes to ensure only authorized staff roles (Admin, Supervisor, Assistant, Teacher) can execute.

### 4. Background Worker (Node.js)

- [x] **Task 4.1**: Update `worker/src/jobs/notification-sender.ts` to support rendering and sending chat mentions notifications if external gateways or email formats apply.

### 5. Frontend Integration & UI/UX

- [x] **Task 5.1**: Create `frontend/src/services/chat-service.ts` with methods to request HTTP API endpoints:
  - `getRooms()`, `getRoomMessages(roomId, page, pageSize)`, `createRoom(payload)`, `archiveRoom(roomId)`, `togglePinMessage(messageId)`, `markRoomRead(roomId)`.
- [x] **Task 5.2**: Create `frontend/src/hooks/useSignalR.ts` hook:
  - Establish a connection to `/hubs/chat` using `@microsoft/signalr`.
  - Expose sending actions and register listener events: `ReceiveMessage`, `UserTyping`, `RoomRead`, `MessagePinned`, `RoomArchived`.
- [x] **Task 5.3**: Build UI Components under `frontend/src/components/chat/`:
  - `ChatSidebar.tsx` (Shared Component): List of rooms with unread badges, typing indicators, search input.
  - `ChatWindow.tsx` (Shared Component): Scrollable message bubble feed, attachment previews, mention dropdowns (`@`), pinned message bar.
  - `PinArea.tsx` (Shared Component): View of pinned messages in the current room.
- [x] **Task 5.4**: Implement pages:
  - `frontend/src/app/admin/chat/page.tsx`
  - `frontend/src/app/assistant/chat/page.tsx`
  - `frontend/src/app/teacher/chat/page.tsx`
  - Integrate these pages using `AdminShellChrome` (for Admin/Assistant) and the teacher layout.
  - Enforce access guard redirection for students.

### 6. Verification & Quality Gates

- [x] **Task 6.1**: Run backend access control tests:
  - Write test class `InternalChatSecurityTests.cs` in `backend/tests/NaderGorge.Application.Tests/Internal/`.
  - Assert that student user queries for rooms or messages throw `UnauthorizedAccessException` or return fail codes.
- [x] **Task 6.2**: Execute `clean-code-guard` command against all modified and created files. Verify 100% compliance.
- [x] **Task 6.3**: Execute `test-guard` against changed test files.
- [x] **Task 6.4**: Run project compilation and build validations:
  - Run `dotnet build` on the backend (verify 0 warnings/errors).
  - Run `npm run build` on the frontend (verify 0 lint warnings/errors).
