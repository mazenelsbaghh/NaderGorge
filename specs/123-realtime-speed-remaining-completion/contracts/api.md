# API Contracts: Real-time Speed Remaining Completion

This document details the new and modified API contracts for lesson detail partitioning and performance logging.

## 1. Split Lesson Detail Endpoints

### 1.1 Get Lesson Core Metadata

*   **URL**: `/api/v1/content/lessons/{lessonId}`
*   **Method**: `GET`
*   **Response Payload**:
    ```json
    {
      "id": "guid-value",
      "title": "Lesson Title",
      "description": "Lesson Description",
      "order": 1,
      "videos": [
        {
          "id": "guid-value",
          "title": "Video Title",
          "provider": "youtube",
          "providerVideoId": "abc123xyz",
          "order": 1,
          "maxWatchCount": 3,
          "isProcessingAI": false
        }
      ]
    }
    ```

### 1.2 Get Lesson Resources

*   **URL**: `/api/v1/content/lessons/{lessonId}/resources`
*   **Method**: `GET`
*   **Response Payload**:
    ```json
    [
      {
        "id": "guid-value",
        "title": "Resource Name",
        "fileUrl": "/downloads/resource-file.pdf",
        "fileSize": 1048576,
        "fileType": "pdf"
      }
    ]
    ```

### 1.3 Get Lesson Comments

*   **URL**: `/api/v1/content/lessons/{lessonId}/comments`
*   **Method**: `GET`
*   **Query Parameters**:
    *   `page`: Number (default: 1)
    *   `pageSize`: Number (default: 20)
*   **Response Payload**:
    ```json
    {
      "items": [
        {
          "id": "guid-value",
          "body": "Comment Body text...",
          "authorName": "Student Name",
          "createdAt": "2026-06-11T19:30:00Z"
        }
      ],
      "totalCount": 42,
      "page": 1,
      "pageSize": 20
    }
    ```

---

## 2. Web Vitals Reporting Endpoint

*   **URL**: `/api/v1/metrics/web-vitals`
*   **Method**: `POST`
*   **Headers**:
    *   `Content-Type`: `application/json`
*   **Request Payload**:
    ```json
    {
      "metricName": "LCP",
      "value": 1500.25,
      "rating": "good",
      "pageUrl": "/student/lessons/guid-value",
      "userAgent": "Mozilla/5.5..."
    }
    ```
*   **Response**: `200 OK` (no content) or `429 Too Many Requests`
