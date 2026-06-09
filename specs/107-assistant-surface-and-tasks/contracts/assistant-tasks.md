# API Contracts: Assistant Tasks endpoints

Endpoints mapped under `api/v1/assistant/tasks`.

## 1. Get My Tasks
Fetch list of tasks assigned to the current user.

* **URL**: `/api/v1/assistant/tasks/my`
* **Method**: `GET`
* **Headers**:
  * `Authorization: Bearer <token>`
* **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Success",
    "data": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "title": "Task Title",
        "description": "Task Description",
        "assigneeId": "c6a1e3a9-cfdf-4a69-9069-42b7e19d140e",
        "assigneeName": "Assistant Full Name",
        "createdById": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "creatorName": "Admin Full Name",
        "status": "InProgress",
        "priority": "Medium",
        "dueDate": "2026-06-15T00:00:00Z",
        "completedAt": null,
        "approvedById": null,
        "approvedByName": null,
        "createdAt": "2026-06-09T12:00:00Z",
        "updatedAt": "2026-06-09T13:00:00Z"
      }
    ]
  }
  ```

## 2. Get Task Details
Fetch full details and comments of a single task.

* **URL**: `/api/v1/assistant/tasks/my/{id}`
* **Method**: `GET`
* **Headers**:
  * `Authorization: Bearer <token>`
* **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Success",
    "data": {
      "task": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "title": "Task Title",
        "description": "Task Description",
        "assigneeId": "c6a1e3a9-cfdf-4a69-9069-42b7e19d140e",
        "assigneeName": "Assistant Full Name",
        "createdById": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "creatorName": "Admin Full Name",
        "status": "InProgress",
        "priority": "Medium",
        "dueDate": "2026-06-15T00:00:00Z",
        "completedAt": null,
        "approvedById": null,
        "approvedByName": null,
        "createdAt": "2026-06-09T12:00:00Z",
        "updatedAt": "2026-06-09T13:00:00Z"
      },
      "comments": [
        {
          "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
          "userId": "c6a1e3a9-cfdf-4a69-9069-42b7e19d140e",
          "userName": "Assistant Full Name",
          "content": "This is a comment",
          "attachmentUrl": null,
          "createdAt": "2026-06-09T14:00:00Z"
        }
      ]
    }
  }
  ```
* **Error Responses**:
  * `401 Unauthorized` if not authenticated.
  * `403 Forbidden` if user is not assignee, creator, admin, or supervisor.
  * `404 Not Found` if task doesn't exist.

## 3. Update Task Status
Transition task status.

* **URL**: `/api/v1/assistant/tasks/my/{id}/status`
* **Method**: `POST`
* **Headers**:
  * `Authorization: Bearer <token>`
  * `Content-Type: application/json`
* **Request Body**:
  ```json
  {
    "status": 1 // Enum: Pending=0, InProgress=1, Review=2, Completed=3
  }
  ```
* **Response**: `200 OK`
  ```json
  {
    "success": true,
    "message": "Task status updated to InProgress."
  }
  ```
* **Error Responses**:
  * `403 Forbidden` if user is not assignee/creator/supervisor/admin, OR if assignee attempts to transition directly to Completed, OR if attempting to change status of task in Review or Completed state without supervisor permissions.
