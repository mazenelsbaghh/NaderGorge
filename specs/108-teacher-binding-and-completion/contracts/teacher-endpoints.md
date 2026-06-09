# API Contract: Teacher Portal Endpoints

This document outlines the API contracts for the endpoints exposed to the teacher surface.

## Endpoints Summary

### 1. GET /api/v1/teacher/dashboard/stats
- **Description**: Fetch stats for the logged-in teacher.
- **Roles**: Teacher
- **Response**: `ApiResponse<TeacherDashboardStatsDto>`
  ```json
  {
    "success": true,
    "message": "Stats retrieved successfully.",
    "data": {
      "activeStudentsCount": 142,
      "packagesCount": 5,
      "examsCount": 12,
      "pendingEssaysCount": 3
    }
  }
  ```

### 2. GET /api/v1/teacher/students
- **Description**: List students enrolled in the teacher's packages.
- **Roles**: Teacher
- **Response**: `ApiResponse<List<TeacherStudentDto>>`
  ```json
  {
    "success": true,
    "data": [
      {
        "id": "guid",
        "fullName": "محمد أحمد",
        "phoneNumber": "01011112222",
        "activatedPackageName": "فيزياء - الصف الثالث الثانوي",
        "activatedAt": "2026-06-09T12:00:00Z"
      }
    ]
  }
  ```

### 3. GET /api/v1/teacher/essays
- **Description**: List pending essay submissions awaiting teacher grading.
- **Roles**: Teacher
- **Response**: `ApiResponse<List<PendingEssayDto>>`
  ```json
  {
    "success": true,
    "data": [
      {
        "id": "guid",
        "studentName": "علي حسن",
        "questionText": "اشرح آلية عمل المحرك الكهربائي.",
        "examTitle": "امتحان الفيزياء الباب الثاني",
        "submittedAt": "2026-06-09T14:30:00Z",
        "status": "WaitTeacher"
      }
    ]
  }
  ```

### 4. POST /api/v1/teacher/essays/{id}/grade
- **Description**: Submit score and feedback for an essay submission.
- **Roles**: Teacher
- **Request Body**:
  ```json
  {
    "score": 8.5,
    "feedback": "إجابة ممتازة، ركز في تفاصيل فرق الجهد."
  }
  ```
- **Response**: `ApiResponse<bool>`

### 5. GET /api/v1/teacher/profile
- **Description**: Retrieve the teacher's public profile data.
- **Roles**: Teacher
- **Response**: `ApiResponse<TeacherProfileDto>`

### 6. PUT /api/v1/teacher/profile
- **Description**: Update the teacher's profile details.
- **Roles**: Teacher
- **Request Body**:
  ```json
  {
    "bio": "مدرس أول مادة الفيزياء للثانوية العامة خبرة ١٥ عاماً.",
    "specialization": "الفيزياء",
    "contactInfo": "physics-teacher@massar.net",
    "profileImageUrl": "https://massar.net/images/teacher-avatar.jpg"
  }
  ```
- **Response**: `ApiResponse<bool>`
