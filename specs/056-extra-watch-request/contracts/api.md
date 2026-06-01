# API Contracts

## Student APIs

### `POST /api/Student/video-session/{lessonVideoId}/request-extra`
Allows a student to request an extra view.
- **Request**: `{}`
- **Response**:
```json
{
  "success": true,
  "data": {
     "id": "guid",
     "status": 0
  }
}
```

### `GET /api/Student/video-session/{lessonVideoId}/request-status`
Allows checking if the user already has a pending or rejected status for this video.
- **Response**:
```json
{
  "success": true,
  "data": {
    "hasPendingRequest": true,
    "hasRejectedRequest": false,
    "requestStatus": 0
  }
}
```

## Admin APIs

### `GET /api/Admin/watch-requests`
Lists all pending or recently resolved requests. Can support pagination/filtering.
- **Response**:
```json
{
  "success": true,
  "data": [
    {
       "id": "...",
       "studentName": "...",
       "studentPhone": "...",
       "videoTitle": "...",
       "status": 0,
       "createdAt": "..."
    }
  ]
}
```

### `POST /api/Admin/watch-requests/{id}/approve`
Approves a pending request.
- **Request**: `{}`
- **Response**:
```json
{
  "success": true,
  "message": "Approved and unlocked"
}
```

### `POST /api/Admin/watch-requests/{id}/reject`
Rejects a pending request.
- **Request**: `{}`
- **Response**:
```json
{
  "success": true,
  "message": "Rejected"
}
```
