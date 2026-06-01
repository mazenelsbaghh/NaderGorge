# Cancel Job Contracts

## 1. Delete Job (Node.js API)
- **Path**: `DELETE /api/status/:id`
- **Host**: `http://localhost:3001` (Worker Express server)
- **Description**: Frontend directly targets the worker's status endpoint, changing the method to DELETE to signify cancellation.
- **Response**: `{ "success": true, "message": "Job cancelled" }`

## 2. Abort Job Tracking (.NET API)
- **Path**: `POST /api/v1/admin/videos/{videoId}/ai-cancel`
- **Host**: Backend (.NET)
- **Description**: After frontend successfully deletes from BullMQ, it notifies the backend to flip `IsProcessingAI` to `false` so the UI returns to idle status on reload.
- **Request**: `{}`
- **Response**: `200 OK`
