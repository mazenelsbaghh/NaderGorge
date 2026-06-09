# Data Model Design: Media Production and Social Planner

## Enums

### 1. MediaStage.cs
Represents the production stages of a media asset:
- `Preparation = 0`: Content preparation, scripting, and outlines.
- `Filming = 1`: Video filming and raw footage recording.
- `Editing = 2`: Video editing and visual post-production.
- `Uploading = 3`: Video upload to storage/hosting.
- `Review = 4`: Review requested, pending supervisor approval.
- `Approved = 5`: Content approved and ready for release.
- `Published = 6`: Released and visible to target audience.

### 2. SocialPlatform.cs
Target social networks:
- `YouTube = 0`
- `Facebook = 1`
- `Instagram = 2`
- `TikTok = 3`
- `Telegram = 4`

### 3. SocialPlanStatus.cs
Lifecycle of a social planner post:
- `Draft = 0`
- `Scripting = 1`
- `Scheduled = 2`
- `Published = 3`

---

## Entities

### 1. MediaProductionPipeline (Table: `MediaProductionPipelines`)
Represents a video lesson or promotional asset in the production lifecycle.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `Title` | string | No | Max length: 250 characters |
| `Description`| string | Yes | Max length: 2000 characters |
| `Stage` | int (enum)| No | Map to `MediaStage` (default: 0) |
| `AssignedAgentId`| Guid | Yes | Foreign Key to `Users` (default: null) |
| `AssetFolderUrl` | string | Yes | URL to external drive (Google Drive etc.) |
| `EditingErrorCount`| int | No | Default: 0 |
| `PublishedAt` | DateTime | Yes | Populated only when stage is `Published` |
| `CreatedAt` | DateTime | No | Default: UtcNow |
| `UpdatedAt` | DateTime | Yes | Updated on modification |

### 2. SocialMediaPlan (Table: `SocialMediaPlans`)
Represents planned marketing posts linked optionally to production assets.

| Column | Type | Nullable | Constraints / Details |
|--------|------|----------|-----------------------|
| `Id` | Guid | No | Primary Key |
| `Title` | string | No | Max length: 250 characters |
| `Description`| string | Yes | Max length: 2000 characters |
| `Script` | string | Yes | Post caption or video script copy |
| `Platform` | int (enum)| No | Map to `SocialPlatform` |
| `Status` | int (enum)| No | Map to `SocialPlanStatus` (default: 0) |
| `ScheduledDate`| DateTime | No | Target publication date |
| `MediaProductionPipelineId`| Guid | Yes | Foreign Key to `MediaProductionPipelines` |
| `CreatedAt` | DateTime | No | Default: UtcNow |

---

## Schema Modifications to Existing Tables

### 1. TaskItem (Table: `TaskItems`)
To link tasks to media items for approval integration:

| Column | Type | Nullable | Details |
|--------|------|----------|---------|
| `MediaPipelineId` | Guid | Yes | Foreign Key to `MediaProductionPipelines` (OnDelete: SetNull) |

---

## Indexes & Constraints

1. **MediaProductionPipeline**:
   - Index on `AssignedAgentId` (for fast agent-level queue filtering).
   - Index on `Stage` (for Kanban board stage queries).
2. **SocialMediaPlan**:
   - Index on `ScheduledDate` (for calendar view and scheduling queries).
   - Index on `MediaProductionPipelineId` (for foreign key lookup).
