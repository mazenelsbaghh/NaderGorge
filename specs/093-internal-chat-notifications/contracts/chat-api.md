# API Contracts: Internal Chat, Workrooms, and Real-Time Notifications

All endpoints require authentication (JWT Bearer Token). Only users with roles `Admin`, `Supervisor`, `Assistant`, or `Teacher` are allowed.

## HTTP endpoints

### 1. Fetch Rooms List
- **Method & URL**: `GET /api/chat/rooms`
- **Response**: `ApiResponse<List<ChatRoomDto>>`
  ```json
  {
    "success": true,
    "message": "",
    "data": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "name": "Production Team",
        "type": "Group",
        "taskItemId": null,
        "isArchived": false,
        "createdAt": "2026-06-09T00:00:00Z",
        "unreadCount": 3,
        "lastMessage": {
          "id": "ea49b294-0cf1-4567-bd1c-1c7bcf1ad7ef",
          "content": "Hi all!",
          "senderName": "Ahmed Ali",
          "createdAt": "2026-06-09T01:00:00Z"
        }
      }
    ]
  }
  ```

### 2. Fetch Room Messages (Paginated)
- **Method & URL**: `GET /api/chat/rooms/{roomId}/messages?page=1&pageSize=50`
- **Response**: `ApiResponse<List<ChatMessageDto>>`
  ```json
  {
    "success": true,
    "message": "",
    "data": [
      {
        "id": "ea49b294-0cf1-4567-bd1c-1c7bcf1ad7ef",
        "roomId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "senderUserId": "a67138de-8ef8-4cc3-92f7-2d12e694fb21",
        "senderName": "Ahmed Ali",
        "content": "Hi all!",
        "type": "Text",
        "mediaUrl": null,
        "mediaMetadata": null,
        "isPinned": false,
        "createdAt": "2026-06-09T01:00:00Z",
        "readBy": ["a67138de-8ef8-4cc3-92f7-2d12e694fb21"]
      }
    ]
  }
  ```

### 3. Create a Chat Room
- **Method & URL**: `POST /api/chat/rooms`
- **Request Body**:
  ```json
  {
    "name": "Physics Prep Team",
    "type": "Group",
    "participantIds": [
      "a67138de-8ef8-4cc3-92f7-2d12e694fb21",
      "b2138dee-2ea9-42b7-bdc1-7d12f123abcd"
    ]
  }
  ```
- **Response**: `ApiResponse<Guid>` (returns Room ID)

### 4. Archive a Room
- **Method & URL**: `POST /api/chat/rooms/{roomId}/archive`
- **Response**: `ApiResponse`

### 5. Pin/Unpin Message
- **Method & URL**: `POST /api/chat/messages/{messageId}/pin`
- **Response**: `ApiResponse`

### 6. Mark Messages as Read
- **Method & URL**: `POST /api/chat/rooms/{roomId}/read`
- **Response**: `ApiResponse`

---

## SignalR Chat Hub Client Events (incoming to server)
- `SendMessage(Guid roomId, string content, string? mediaUrl = null, string? mediaMetadata = null)`
- `Typing(Guid roomId)` - Broadcasts to other users that the current user is typing.

## SignalR Chat Hub Server Events (outgoing to client)
- `ReceiveMessage(ChatMessageDto message)`
- `UserTyping(Guid roomId, Guid userId, string userName)`
- `RoomRead(Guid roomId, Guid userId)` - Updates client read states.
- `MessagePinned(Guid roomId, Guid messageId, bool isPinned)`
- `RoomArchived(Guid roomId, bool isArchived)`
