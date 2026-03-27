# Data Model: Admin Student Profile

This document outlines the structured data types (DTOs) and any new database schemas required to support the comprehensive 360-degree view of a student.

## 1. Domain Entities / DTOs

### 1.1 `StudentProfileExtendedDto`
Provides a holistic view of the student without N+1 queries.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Student identifier. |
| `FullName` | `string` | First and Last name. |
| `Email` | `string` | Contact email. |
| `Phone` | `string` | Primary phone number. |
| `ParentPhone` | `string?` | Parent's contact number (if logged). |
| `Grade` | `string?` | Educational grade. |
| `SchoolName` | `string?` | Student's school. |
| `IsActive` | `bool` | Account suspension status. |
| `CreatedAt` | `DateTime` | Join date. |
| `Gamification` | `GamificationSummaryDto` | Current rank and total points. |
| `Packages` | `List<StudentPackageDto>` | List of active/expired packages. |
| `Devices` | `List<StudentDeviceDto>` | List of historical and active sessions/devices. |
| `Overrides` | `List<VideoOverrideDto>` | Specifically granted permissions bridging content watch limits. |

### 1.2 `StudentDeviceDto`
Describes a known session token or hardware footprint.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Device record ID. |
| `DeviceName` | `string` | Parsed User Agent (e.g. iPhone 14, Windows PC). |
| `IpAddress` | `string?` | Last IP associated. |
| `LastActiveAt` | `DateTime` | Timestamp of last access. |
| `IsActive` | `bool` | Whether the auth token is still valid. |

### 1.3 `VideoOverrideDto`
A record extending or removing the watch limit for a specific student on a specific video.

| Property | Type | Description |
|---|---|---|
| `Id` | `Guid` | Override ID. |
| `VideoId` | `Guid` | Associated video. |
| `VideoTitle` | `string` | Lesson/Video name for context. |
| `OriginalLimit` | `int` | Standard limit from `Content.Videos`. |
| `NewLimit` | `int` | Student-specific adjusted limit. |
| `CurrentViews` | `int` | Student's current watches of that video. |
| `OverrideBy` | `string` | Name of the Admin who granted this. |
| `CreatedAt` | `DateTime` | Timestamp of the grant. |

### 1.4 `AdminAuditLog` (Domain Entity)
A new infrastructure table meant to capture significant system mutations (e.g. view overrides, account suspension, gamification manipulation).

| Column | Type | Description |
|---|---|---|
| `Id` | UUID (PK) | Audit entry ID. |
| `AdmintId` | UUID (FK) | Reference to `Users` where Role=Admin. |
| `TargetUserId` | UUID (FK) | Reference to `Users` where Role=Student. |
| `Action` | string | Description or Enum (e.g., "ADD_VIEWS", "DEACTIVATE_ACCOUNT"). |
| `Metadata` | JSONB | Details (e.g. `{"videoId": "...", "addedViews": 2}`). |
| `CreatedAt` | Timestamp | Standard timestamp. |
