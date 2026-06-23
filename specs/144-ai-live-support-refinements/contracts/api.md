# API Contracts: Live Support AI Refinements

## 1. Enable AI Policy
- **Method**: `POST`
- **Path**: `/api/live-support/admin/ai/enable`
- **Headers**:
  - `Authorization: Bearer <JWT>`
- **Response**: `200 OK`
  - **Body**:
    ```json
    {
      "success": true,
      "data": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "versionNumber": 1,
        "status": "Published",
        "isEnabled": true,
        "systemInstructions": "...",
        "readableDataKeys": [],
        "actionKeys": [],
        "lookupKeys": [],
        "verificationQuestionKeys": [],
        "verificationRequiredCorrect": 2,
        "verificationMaxAttempts": 3,
        "pendingActionExpirySeconds": 300,
        "inactivityMinutes": 30,
        "inactivityWarningGraceSeconds": 120,
        "version": 2,
        "publishedAt": "2026-06-23T16:00:00Z"
      },
      "errors": []
    }
    ```

## 2. Get Performance Statistics
- **Method**: `GET`
- **Path**: `/api/live-support/admin/ai/stats`
- **Query Parameters**:
  - `period` (string, optional): `last-24h` | `last-7d` | `last-30d` | `lifetime`. Defaults to `last-24h`.
- **Headers**:
  - `Authorization: Bearer <JWT>`
- **Response**: `200 OK`
  - **Body**:
    ```json
    {
      "success": true,
      "data": {
        "activeConversations": 12,
        "resolvedIssues": 45,
        "handoffs": 8,
        "totalMessagesSent": 284,
        "successfulActions": 15
      },
      "errors": []
    }
    ```
