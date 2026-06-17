# Nader Gorge Production Database Schema Specification

Auto-generated from EF Core DbContext snapshot. This documents all columns, data types, and nullability.

## Table: `ExtraWatchRequests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `ResolvedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `PlatformSettings`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Key` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Value` | `string` | `text` | `NO` | ✅ Sync |

## Table: `StudentNotes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AdminId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Content` | `string` | `text` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPinned` | `bool` | `boolean` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `VideoPlaybackSessions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EncryptionKey` | `string` | `text` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `text` | `YES` | ✅ Sync |
| `IsConsumed` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SessionToken` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `access_code_activation_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AccessCodeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ActivatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CommissionEarned` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CommissionRate` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Price` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `access_codes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CodeGroupId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CodeHash` | `string` | `text` | `NO` | ✅ Sync |
| `CodePlaintext` | `string` | `text` | `NO` | ✅ Sync |
| `ConsumedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConsumedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsConsumed` | `bool` | `boolean` | `NO` | ✅ Sync |
| `QrCodeUrl` | `string` | `text` | `YES` | ✅ Sync |
| `SerialNumber` | `long` | `bigint` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `assistant_tasks`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssignedAssistantId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ReferenceEntityId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TaskType` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `attendance_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ClockIn` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ClockOut` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Date` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `character varying(45)` | `NO` | ✅ Sync |
| `LateMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserAgent` | `string` | `character varying(500)` | `NO` | ✅ Sync |

## Table: `audit_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Action` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `CorrelationId` | `string` | `character varying(64)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EntityId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `EntityType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `character varying(45)` | `YES` | ✅ Sync |
| `NewValues` | `string` | `text` | `YES` | ✅ Sync |
| `OldValues` | `string` | `text` | `YES` | ✅ Sync |
| `PerformedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `balance_transactions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `BalanceAfter` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PerformedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ReferenceId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `StudentBalanceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TransactionType` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `bunny_usage_snapshots`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BandwidthBytes` | `long` | `bigint` | `NO` | ✅ Sync |
| `BandwidthCostUsd` | `decimal` | `numeric(18,6)` | `NO` | ✅ Sync |
| `BandwidthRateUsdPerGb` | `decimal` | `numeric(18,6)` | `NO` | ✅ Sync |
| `BandwidthSource` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `BunnyStorageCalculatedAtUtc` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `BunnyVideoAssetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsBandwidthEstimated` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Notes` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `PackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PeriodEndUtc` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `PeriodStartUtc` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `StorageBytes` | `long` | `bigint` | `NO` | ✅ Sync |
| `StorageCostUsd` | `decimal` | `numeric(18,6)` | `NO` | ✅ Sync |
| `StorageRateUsdPerGb` | `decimal` | `numeric(18,6)` | `NO` | ✅ Sync |
| `SyncedAtUtc` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `SyncedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TotalCostUsd` | `decimal` | `numeric(18,6)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `bunny_video_assets`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BandwidthBytes` | `long?` | `bigint` | `YES` | ✅ Sync |
| `BunnyCollectionId` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `BunnyEncodeProgress` | `int?` | `integer` | `YES` | ✅ Sync |
| `BunnyLibraryId` | `long` | `bigint` | `NO` | ✅ Sync |
| `BunnyVideoGuid` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DurationSeconds` | `int?` | `integer` | `YES` | ✅ Sync |
| `ErrorMessage` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `FileSizeBytes` | `long?` | `bigint` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LastStatusSyncedAtUtc` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LastUsageSyncedAtUtc` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OriginalFileName` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `PackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceUrlHash` | `string` | `character varying(128)` | `YES` | ✅ Sync |
| `Status` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `StorageBytes` | `long?` | `bigint` | `YES` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UploadMethod` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `UploadedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `chat_message_read_states`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `MessageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ReadAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `chat_messages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ChatRoomId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Content` | `string` | `character varying(4000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPinned` | `bool` | `boolean` | `NO` | ✅ Sync |
| `MediaMetadata` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `MediaUrl` | `string` | `character varying(2048)` | `YES` | ✅ Sync |
| `SenderUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `chat_participants`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ChatRoomId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `JoinedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `LastReadMessageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `chat_rooms`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsArchived` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `TaskItemId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `code_groups`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BalanceAmount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `CodeType` | `int` | `integer` | `NO` | ✅ Sync |
| `ContentSectionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DiscountPercentage` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `QrDataGenerated` | `bool` | `boolean` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TermId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TotalCodes` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `code_video_targets`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CodeGroupId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `community_post_comments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AuthorUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Body` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PostId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `ReviewedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ReviewedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `community_post_likes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PostId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `community_post_poll_options`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PostId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Text` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `community_post_poll_votes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PollOptionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PostId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `community_posts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AuthorUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Body` | `string` | `character varying(4000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPoll` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ReviewedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ReviewedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `content_sections`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Price` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `TermId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `crm_call_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AgentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CallDate` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `NextFollowUpDate` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Notes` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `Outcome` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `crm_student_statuses`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssignedAgentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `LastCalledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `NextFollowUpDate` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Notes` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `Priority` | `int` | `integer` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `custom_forms`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CoverImageUrl` | `string` | `text` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `FieldsJson` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Slug` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `StartsAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VisitCount` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `devices`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BrowserName` | `string` | `text` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DeviceFingerprint` | `string` | `text` | `NO` | ✅ Sync |
| `DeviceName` | `string` | `text` | `YES` | ✅ Sync |
| `DeviceType` | `string` | `text` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `text` | `YES` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LastUsedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `OsName` | `string` | `text` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `employee_profiles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BasicSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StandardStartTime` | `TimeSpan` | `interval` | `NO` | ✅ Sync |
| `TargetDailyHours` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `employee_vacations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EndDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `HandledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HandledBy` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `StartDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `essay_submissions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AiFeedback` | `string` | `text` | `YES` | ✅ Sync |
| `AiInitialScore` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `AnswerText` | `string` | `text` | `NO` | ✅ Sync |
| `AudioUrl` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GradedByTeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `QuestionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentExamAttemptId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherFeedback` | `string` | `text` | `YES` | ✅ Sync |
| `TeacherFinalScore` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `exam_questions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Points` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `QuestionBankItemId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `exams`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByTeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Description` | `string` | `text` | `NO` | ✅ Sync |
| `DisplayQuestionCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `DurationMinutes` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsMandatory` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsRandomized` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PassingScore` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `TotalScore` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `form_submissions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AdminNotes` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CustomFormId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `SubmittedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `SubmittedDataJson` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `gamification_action_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EventType` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PointsAwarded` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `homework_answers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `HomeworkSubmissionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ProvidedAnswer` | `string` | `text` | `NO` | ✅ Sync |
| `QuestionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ScoreReceived` | `int?` | `integer` | `YES` | ✅ Sync |

## Table: `homework_questions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AudioUrl` | `string` | `text` | `YES` | ✅ Sync |
| `BaseText` | `string` | `text` | `YES` | ✅ Sync |
| `BodyText` | `string` | `text` | `NO` | ✅ Sync |
| `CorrectAnswerKey` | `string` | `text` | `YES` | ✅ Sync |
| `HintText` | `string` | `text` | `YES` | ✅ Sync |
| `HomeworkId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `MistakeEndIndex` | `int?` | `integer` | `YES` | ✅ Sync |
| `MistakeStartIndex` | `int?` | `integer` | `YES` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `PointsActive` | `int` | `integer` | `NO` | ✅ Sync |
| `PossibleAnswers` | `string[]` | `text[]` | `NO` | ✅ Sync |
| `QuestionType` | `int` | `integer` | `NO` | ✅ Sync |
| `WrittenCorrection` | `string` | `text` | `YES` | ✅ Sync |

## Table: `homework_submissions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssistantNotes` | `string` | `text` | `YES` | ✅ Sync |
| `AssistantReviewerId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Evaluation` | `string` | `text` | `YES` | ✅ Sync |
| `GradedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HomeworkId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OverallScore` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SubmittedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `homeworks`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `text` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsMandatory` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsRandomized` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PassingScoreThreshold` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `Title` | `string` | `character varying(255)` | `NO` | ✅ Sync |
| `TotalScore` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |

## Table: `lesson_comments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AuthorUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Body` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ReviewedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ReviewedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `lesson_progress`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsCompleted` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsManuallyUnlocked` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `lesson_resources`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FileUrl` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ResourceType` | `string` | `text` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `lesson_videos`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsProcessingAI` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsProcessingMindmaps` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MaxWatchCount` | `int` | `integer` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Provider` | `string` | `text` | `NO` | ✅ Sync |
| `ProviderVideoId` | `string` | `text` | `NO` | ✅ Sync |
| `SubtitleUrl` | `string` | `text` | `YES` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VideoTag` | `string` | `text` | `YES` | ✅ Sync |

## Table: `lessons`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ContentSectionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Price` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `Summary` | `string` | `text` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `media_production_pipelines`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssetFolderUrl` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `AssignedAgentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `EditingErrorCount` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Stage` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(250)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `notification_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Body` | `string` | `text` | `NO` | ✅ Sync |
| `ChannelType` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ReadAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `text` | `NO` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `outbox_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsDeadLetter` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LastError` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `PayloadJson` | `string` | `text` | `NO` | ✅ Sync |
| `ProcessedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `RetryCount` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetGroup` | `string` | `character varying(150)` | `YES` | ✅ Sync |
| `TargetUserId` | `string` | `character varying(150)` | `YES` | ✅ Sync |
| `Type` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `package_code_page_profiles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActivationDescription` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `ActivationTitle` | `string` | `character varying(120)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `HeroDescription` | `string` | `character varying(600)` | `YES` | ✅ Sync |
| `HeroEyebrow` | `string` | `character varying(80)` | `YES` | ✅ Sync |
| `HeroTitle` | `string` | `character varying(140)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OfferDescription` | `string` | `character varying(600)` | `YES` | ✅ Sync |
| `OfferTitle` | `string` | `character varying(120)` | `YES` | ✅ Sync |
| `PackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `SupportDescription` | `string` | `character varying(400)` | `YES` | ✅ Sync |
| `SupportTitle` | `string` | `character varying(120)` | `YES` | ✅ Sync |
| `ThemeAccentKey` | `string` | `character varying(60)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UpdatedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

## Table: `packages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Price` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `SubjectId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetGrade` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `payroll_adjustments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayrollRecordId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `payroll_records`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ApprovedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `BasicSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeProfileId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Month` | `int` | `integer` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Year` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `question_bank_items`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AudioUrl` | `string` | `text` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByTeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DefaultPoints` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `HintText` | `string` | `text` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `SubjectId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Tags` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `Text` | `string` | `text` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WrittenCorrection` | `string` | `text` | `YES` | ✅ Sync |

## Table: `question_options`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsCorrect` | `bool` | `boolean` | `NO` | ✅ Sync |
| `QuestionBankItemId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Text` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `refresh_tokens`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DeviceFingerprint` | `string` | `text` | `YES` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsRevoked` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Token` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `roles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `PermissionsJson` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `social_media_plans`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MediaProductionPipelineId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Platform` | `int` | `integer` | `NO` | ✅ Sync |
| `ScheduledDate` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Script` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(250)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `student_access_grants`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AccessCodeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CancellationReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `CancelledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CancelledByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ContentSectionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `GrantType` | `int` | `integer` | `NO` | ✅ Sync |
| `GrantedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `LessonVideoId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TermId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `student_answers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamQuestionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `HintUsed` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsCorrect` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PointsAwarded` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `SelectedOptionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `StudentExamAttemptId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SubmittedText` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `student_badges`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BadgeName` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UnlockedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |

## Table: `student_balances`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentBalance` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `student_exam_attempts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Evaluation` | `string` | `text` | `YES` | ✅ Sync |
| `ExamId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPassed` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsTimeExpired` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ScoreAchieved` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `student_gamifications`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CurrentStreakCount` | `int` | `integer` | `NO` | ✅ Sync |
| `LastTaskCompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LevelName` | `string` | `text` | `NO` | ✅ Sync |
| `LongestStreakCount` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TotalPoints` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `student_profiles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Address` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `AvatarSlug` | `string` | `text` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentMode` | `string` | `character varying(10)` | `NO` | ✅ Sync |
| `DarkThemePaletteId` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `DateOfBirth` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `District` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `EducationStage` | `int` | `integer` | `NO` | ✅ Sync |
| `FatherDateOfBirth` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Gender` | `int` | `integer` | `NO` | ✅ Sync |
| `Governorate` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `GradeLevel` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsFatherAlive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsMotherAlive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LightThemePaletteId` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `MotherDateOfBirth` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `MotherPhone` | `string` | `text` | `YES` | ✅ Sync |
| `Nationality` | `string` | `text` | `YES` | ✅ Sync |
| `ParentPhone` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `SchoolName` | `string` | `text` | `YES` | ✅ Sync |
| `SchoolType` | `int?` | `integer` | `YES` | ✅ Sync |
| `SecondaryParentPhone` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `SecondaryPhone` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `StudentCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `StudyTrack` | `int?` | `integer` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `student_status_trackers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ConsecutiveFailedExams` | `int` | `integer` | `NO` | ✅ Sync |
| `ConsecutiveMissedHomeworks` | `int` | `integer` | `NO` | ✅ Sync |
| `CurrentStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `LastActiveAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LastEvaluatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `subjects`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `NormalizedName` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `task_comments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttachmentUrl` | `string` | `character varying(2048)` | `YES` | ✅ Sync |
| `Content` | `string` | `character varying(4000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TaskId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `task_items`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovedById` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AssigneeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedById` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(4000)` | `NO` | ✅ Sync |
| `DueDate` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MediaPipelineId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Priority` | `int` | `integer` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(255)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_accounts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CommissionRate` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentBalance` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TotalEarnings` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_payouts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `HandledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HandledByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_photos`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FileUrl` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UploadedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |

## Table: `teacher_profiles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssistantPhoneNumbers` | `string` | `text` | `YES` | ✅ Sync |
| `Bio` | `string` | `text` | `NO` | ✅ Sync |
| `CommissionRate` | `decimal` | `numeric(18,2)` | `NO` | ✅ Sync |
| `ContactInfo` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FacebookUrl` | `string` | `text` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ProfileImageUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `Specialization` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `TelegramUrl` | `string` | `text` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `YouTubeUrl` | `string` | `text` | `YES` | ✅ Sync |

## Table: `teacher_subjects`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `SubjectId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `terms`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `PackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Price` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `user_roles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `RoleId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `users`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FullName` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsProfileComplete` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PasswordHash` | `string` | `text` | `NO` | ✅ Sync |
| `PasswordResetVersion` | `int` | `integer` | `NO` | ✅ Sync |
| `PhoneNumber` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `SuspensionReason` | `string` | `text` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `video_chapters`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EndTime` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MindmapImageUrl` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `StartTime` | `int` | `integer` | `NO` | ✅ Sync |
| `SummaryText` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `video_overrides`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AddedViews` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `NewLimit` | `int` | `integer` | `NO` | ✅ Sync |
| `OriginalLimit` | `int` | `integer` | `NO` | ✅ Sync |
| `PerformedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `video_watch_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CustomMaxWatchCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsLocked` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TimeWatchedInSeconds` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `WatchCount` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `warning_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsResolved` | `bool` | `boolean` | `NO` | ✅ Sync |
| `OccurrenceKey` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `ResolutionNotes` | `string` | `text` | `YES` | ✅ Sync |
| `ResolvedByAssistantId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Severity` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TriggerReason` | `string` | `text` | `NO` | ✅ Sync |

## Table: `web_vitals_metrics`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MetricName` | `string` | `character varying(32)` | `NO` | ✅ Sync |
| `PageUrl` | `string` | `character varying(512)` | `NO` | ✅ Sync |
| `Rating` | `string` | `character varying(32)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserAgent` | `string` | `character varying(512)` | `NO` | ✅ Sync |
| `Value` | `double` | `double precision` | `NO` | ✅ Sync |

