# API Contracts: Admin Student Profile

This document outlines the REST API endpoints the frontend will call to fetch profile data and trigger actions.

## 1. Fetch Comprehensive Profile
Provides the 360-degree `StudentProfileExtendedDto`.

**Endpoint**: `GET /api/admin/users/students/{userId}/profile`
**Auth**: Admin required (JWT containing `RoleType.Admin`)

**Response**:
```json
{
  "id": "e8a2a...,"
  "fullName": "Student Name",
  "email": "student@example.com",
  "phone": "+201...",
  "parentPhone": "+201...",
  "grade": "سنة أولى",
  "schoolName": "Sample School",
  "isActive": true,
  "createdAt": "2026-03-25T14:48:00Z",
  "gamification": {
    "totalPoints": 1500,
    "globalRank": 14,
    "level": 4,
    "title": "مبتكر",
    "recentBadges": ["First Assessment", "Perfect Score"]
  },
  "packages": [
    { "id": "p1", "name": "Basic Physics", "enrolledAt": "2026-03-26", "expiresAt": null, "progress": 45 }
  ],
  "devices": [
    { "id": "d1", "deviceName": "Chrome / Windows 11", "lastActiveAt": "2026-03-26T10:00:00Z", "isActive": true }
  ],
  "overrides": [
    { "id": "o1", "videoTitle": "Newton Laws", "originalLimit": 3, "newLimit": 5, "currentViews": 3, "overrideBy": "Admin Name" }
  ],
  "auditTrail": [
    { "id": "log1", "adminName": "Mazen Elsbagh", "action": "ADD_VIEWS", "date": "2026-03-26T15:00:00Z" }
  ]
}
```

## 2. Override Video Views
**Endpoint**: `POST /api/admin/users/students/{userId}/overrides`

**Request Body**:
```json
{
  "videoId": "uuid-v4",
  "addedViews": 2,
  "reason": "Technical issue"
}
```

**Response**: 204 No Content

## 3. Disconnect Devices
**Endpoint**: `DELETE /api/admin/users/students/{userId}/devices/{deviceId}`
To disconnect all: `DELETE /api/admin/users/students/{userId}/devices`

**Response**: 204 No Content

## 4. Gamification Adjustment
**Endpoint**: `POST /api/admin/users/students/{userId}/gamification/adjust`

**Request Body**:
```json
{
  "points": 50, // Negative values decrease points
  "reason": "Excellent answer in class"
}
```

**Response**: 204 No Content

## 5. Account Suspension Toggle
**Endpoint**: `PATCH /api/admin/users/students/{userId}/status`

**Request Body**:
```json
{
  "isActive": false,
  "reason": "Late Payment"
}
```

**Response**: 204 No Content
