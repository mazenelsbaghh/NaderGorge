# API Contract: Active Teacher Photo Retrieval

## 1. Retrieve Active Photo (Admin Panel)

Exposed on `AdminController` to retrieve the active photo URL for any teacher by their user ID.

- **URL**: `/api/admin/teachers/{teacherId}/active-photo`
- **Method**: `GET`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response (Success - 200 OK)**:
  ```json
  {
    "success": true,
    "message": "",
    "data": {
      "url": "/uploads/content/teacher/abcd1234.webp"
    }
  }
  ```
- **Response (No Photo Found - 200 OK)**:
  ```json
  {
    "success": true,
    "message": "",
    "data": {
      "url": null
    }
  }
  ```
- **Response (Unauthorized - 401/403)**:
  ```json
  {
    "success": false,
    "message": "Unauthorized"
  }
  ```

---

## 2. Retrieve My Active Photo (Teacher Portal)

Exposed on `TeacherController` for the logged-in teacher to retrieve their own active reference photo.

- **URL**: `/api/teacher/profile/active-photo`
- **Method**: `GET`
- **Headers**:
  - `Authorization: Bearer <token>`
- **Response (Success - 200 OK)**:
  ```json
  {
    "success": true,
    "message": "",
    "data": {
      "url": "/uploads/content/teacher/abcd1234.webp"
    }
  }
  ```
