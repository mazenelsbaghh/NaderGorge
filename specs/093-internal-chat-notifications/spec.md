# Feature Specification: Internal Chat, Workrooms, and Real-Time Notifications

**Feature Branch**: `093-internal-chat-notifications`  
**Created**: 2026-06-09  
**Status**: Draft  
**Input**: User description: "Phase 5 - Internal Chat, Workrooms, and Real-Time Notifications"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 1-to-1 and Group Chat (Priority: P1)

Staff, admins, and teachers need to initiate direct (1-to-1) or group chats to coordinate daily operations.

**Why this priority**: Direct coordination is the core component of internal communication.
**Independent Test**: Create a chat room between Admin and Assistant, send a message from Admin, and verify the Assistant receives it instantly.

**Acceptance Scenarios**:
1. **Given** two authenticated staff members, **When** one selects the other and starts a direct chat, **Then** a private chat room is established, and they can send/receive messages in real time.
2. **Given** a group chat room, **When** a participant sends a text message, **Then** all online participants in that group receive the message instantly via SignalR without page refresh, and offline participants see unread badges when they log in.

---

### User Story 2 - Task-Based Workrooms (Priority: P1)

Staff need dedicated, context-specific workrooms automatically linked to operations tasks (from Phase 3) to collaborate on task completion.

**Why this priority**: Operations tasks are the primary work unit; having contextual chat keeps communication organized.
**Independent Test**: Navigate to an operations task, click "Open Task Chat", and send messages that are visible only to task participants.

**Acceptance Scenarios**:
1. **Given** an existing operations task, **When** a supervisor or assignee opens the task detail page, **Then** a dedicated task workroom chat is displayed.
2. **Given** a task workroom chat, **When** a user is added to or removed from the task assignees, **Then** their access to the task workroom is updated automatically.

---

### User Story 3 - Mentions and Real-Time Notifications (Priority: P2)

Users need to mention other team members using `@username` to draw their attention, triggering real-time web push/in-app notifications.

**Why this priority**: Notifications keep the team updated on critical actions.
**Independent Test**: Send a message containing `@username` and verify that the target user receives an instant notification popup.

**Acceptance Scenarios**:
1. **Given** a participant in a chat room, **When** they type `@` followed by a participant's username and send the message, **Then** the mentioned user receives a real-time notification with a link to the chat room.
2. **Given** a user with the student role, **When** they attempt to access any internal chat endpoint or URL, **Then** they are blocked and receive an unauthorized error (chat is staff-only).

---

### User Story 4 - Message Pinning and Room Archiving (Priority: P2)

Admins and supervisors need to pin important announcements in a room, and archive rooms when operations/tasks are completed.

**Why this priority**: Prevents information overload and cleans up finished conversations.
**Independent Test**: Pin a message in a room and check that it stays at the top; archive a room and check that non-admins can no longer send messages.

**Acceptance Scenarios**:
1. **Given** a participant in a chat room, **When** they choose to pin a message, **Then** the message is highlighted and pinned to the top of the chat panel for all participants.
2. **Given** a chat room, **When** an admin archives it, **Then** it becomes read-only for all normal staff, and a banner states "This room is archived".

### Edge Cases

- **User Access Revocation**: If a user is removed from a task or deactivated, any active SignalR connections they hold MUST be disconnected, and further requests to fetch chat messages MUST be rejected with a 403 Forbidden.
- **Handling Large File Attachments**: If a user uploads a file/image attachment, metadata MUST be validated before storing, and client-side limits MUST enforce a maximum upload size of 10MB.
- **Offline Message Queueing**: If a client temporarily loses internet connection, the UI MUST show a "Connecting..." indicator, disable the input field, and reconnect automatically to SignalR when online.

### Manual QA & Docker Acceptance *(mandatory)*

- **Manual QA Role/Flow 1**: Admin logs in, navigates to `/admin/chat`, creates a group room "Production", adds an Assistant user, sends a message. Assistant logs in in another browser, navigates to `/admin/chat` (or `/assistant/chat`), and sees the message instantly.
- **Manual QA Role/Flow 2**: Assistant logs in, opens an operations task, types a chat message. Admin opens the same task and views the message.
- **Manual QA Negative Check**: Student logs in, navigates to `/admin/chat` or `/teacher/chat`, and is redirected to the student dashboard. Any direct API calls to `/api/chat` from a student account return `403 Forbidden`.
- **Docker Acceptance**: Run `make up` and `make migrate`. Run `curl -f http://localhost:5245/api/health` to confirm the backend is healthy. Connect to the WebSocket endpoint at `http://localhost:5245/hubs/chat` using a test tool to verify handshake.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST restrict internal chat access to users with Admin, Supervisor, Assistant, or Teacher roles. Students MUST be denied access.
- **FR-002**: System MUST support three types of chat rooms: `Individual` (1-to-1), `Group` (ad-hoc group chat), and `Workroom` (linked to an operations task).
- **FR-003**: System MUST automatically create a `Workroom` for every newly created operations task, adding the creator, assignees, and supervisors as participants.
- **FR-004**: System MUST push messages to all online room participants in real time via a SignalR hub `/hubs/chat` using WebSockets protocol.
- **FR-005**: System MUST support text messages and attachments (images, files, audio) represented by `MediaUrl` and metadata.
- **FR-006**: System MUST track message read states: when a user opens a room, all messages sent prior are marked as read for this user, and read receipts are sent to other online users.
- **FR-007**: System MUST parse `@username` in messages and raise a `NotificationEvent` in the database, triggering a real-time SignalR notification to the mentioned user.
- **FR-008**: System MUST allow pinning messages within a room, displaying the pinned message at the top of the chat panel.
- **FR-009**: System MUST support archiving rooms. Archived rooms can be viewed but do not accept new messages from normal staff (only Admins/Supervisors can post or unarchive).

### Key Entities *(include if feature involves data)*

- **ChatRoom**: Represents a chat room. Attributes: `Id`, `Name` (nullable for direct chats), `Type` (`Individual`, `Group`, `Workroom`), `TaskItemId` (nullable, links to task), `IsArchived`, `CreatedAt`, `CreatedByUserId`.
- **ChatParticipant**: Maps users to rooms. Attributes: `ChatRoomId`, `UserId`, `JoinedAt`, `LastReadMessageId` (nullable).
- **ChatMessage**: Stores individual messages. Attributes: `Id`, `ChatRoomId`, `SenderUserId`, `Content` (text), `Type` (`Text`, `Attachment`, `System`), `MediaUrl` (nullable), `MediaMetadata` (JSON, nullable), `IsPinned`, `CreatedAt`.
- **ChatMessageReadState**: Represents read receipts. Attributes: `MessageId`, `UserId`, `ReadAt`.
- **NotificationEvent**: Stores system notifications (already exists, but expanded with chat types). Attributes: `Id`, `UserId`, `Type` (e.g. ChatMention, TaskAssigned), `Message` (text), `LinkUrl` (nullable), `IsRead`, `CreatedAt`.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Real-time message delivery latency is under 500ms for online users on stable networks.
- **SC-002**: Read receipts update other participants' screens in under 1 second of opening a room.
- **SC-003**: 100% of unauthorized student requests to the chat endpoints are blocked and return `403 Forbidden` errors.
- **SC-004**: The system supports at least 200 concurrent active WebSocket connections without memory leaks or server CPU spikes.

## Assumptions

- We assume the existing SignalR configurations or CORS policies allow connecting to `/hubs/chat` from the frontend domain.
- File storage for chat attachments will use the existing local storage or S3 bucket integration already set up for video chapters and mindmaps.
- Push notifications are limited to in-app real-time notifications via SignalR; push alerts (e.g., FCM/WebPush API) are out of scope for this phase.
