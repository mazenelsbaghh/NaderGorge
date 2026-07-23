# Nader Gorge Production Database Schema Specification

Auto-generated from EF Core DbContext snapshot. This documents all columns, data types, and nullability.

## Table: `ExtraWatchRequests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `RequestReason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ResolvedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `ParentDeviceTokens`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DeviceToken` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Platform` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

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
| `HasRegisteredView` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `text` | `YES` | ✅ Sync |
| `IsConsumed` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsSuperseded` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LastProgressAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LastProgressSequence` | `long` | `bigint` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SessionToken` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `academic_subject_eligibilities`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EducationStage` | `int` | `integer` | `NO` | ✅ Sync |
| `GradeLevel` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `SubjectId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

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
| `ActorSnapshot` | `string` | `text` | `YES` | ✅ Sync |
| `ActorType` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `CorrelationId` | `string` | `character varying(64)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EntityId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `EntityType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IpAddress` | `string` | `character varying(45)` | `YES` | ✅ Sync |
| `NewValues` | `string` | `text` | `YES` | ✅ Sync |
| `OldValues` | `string` | `text` | `YES` | ✅ Sync |
| `PerformedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `RequestId` | `string` | `character varying(100)` | `YES` | ✅ Sync |
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
| `AccountingRecordedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `AccountingTiming` | `int` | `integer` | `NO` | ✅ Sync |
| `BalanceAmount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `CodeType` | `int` | `integer` | `NO` | ✅ Sync |
| `ContentSectionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DiscountPercentage` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExpireActivatedAccess` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IncludeFutureVideos` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PublicExamProductId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `QrDataGenerated` | `bool` | `boolean` | `NO` | ✅ Sync |
| `RevenueAllocationMode` | `int?` | `integer` | `YES` | ✅ Sync |
| `RevenueAllocationValue` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `RevenueOwner` | `int?` | `integer` | `YES` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TermId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TotalCodes` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VideoTypeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

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
| `ParentCommentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
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
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
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

## Table: `digital_wallets`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentBalance` | `decimal` | `numeric(18,2)` | `NO` | ✅ Sync |
| `DailyLimit` | `decimal` | `numeric(18,2)` | `NO` | ✅ Sync |
| `DeviceStatus` | `string` | `text` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Label` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `LastSeenAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `MonthlyLimit` | `decimal` | `numeric(18,2)` | `NO` | ✅ Sync |
| `PairingToken` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `PhoneNumber` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `SmsSenderFilters` | `string` | `text` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `discount_stacking_policies`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsDefault` | `bool` | `boolean` | `NO` | ✅ Sync |
| `MaxDiscountAmount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `MaxDiscountPercentage` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `Mode` | `int` | `integer` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(120)` | `NO` | ✅ Sync |
| `NormalizedName` | `string` | `character varying(120)` | `NO` | ✅ Sync |
| `PriorityJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `employee_profiles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BasicSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeNumber` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `EmploymentStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `HireDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StandardStartTime` | `TimeSpan` | `interval` | `NO` | ✅ Sync |
| `TargetDailyHours` | `int` | `integer` | `NO` | ✅ Sync |
| `TerminationDate` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `WorkMode` | `int` | `integer` | `NO` | ✅ Sync |

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
| `InternalCode` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
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

## Table: `gift_issuances`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IssuedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LessonId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `LessonVideoId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `MaxUses` | `int?` | `integer` | `YES` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `RequestId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `gift_recipients`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GiftIssuanceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OutcomeCode` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `OutcomeMessage` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `RevocationReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `RevokedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `RevokedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UsesConsumed` | `int` | `integer` | `NO` | ✅ Sync |

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
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsMandatory` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsRandomized` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PassingScoreThreshold` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `Title` | `string` | `character varying(255)` | `NO` | ✅ Sync |
| `TotalScore` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |

## Table: `hr_approval_definition_steps`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovalDefinitionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ApproverKind` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EscalationPermission` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Permission` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `SlaMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `SpecificUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_approval_definitions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `RequestType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_approval_delegations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DelegateUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EndsAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PrincipalUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `Scope` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `StartsAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_approval_instances`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovalDefinitionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentStepOrder` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RequestId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RequestType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `RequesterEmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_approval_step_instances`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActingUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ApprovalDefinitionStepId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ApprovalInstanceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecidedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `DecisionReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `DelegationId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `DueAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EscalationLevel` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `OriginalApproverUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_asset_custodies`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `AssignedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `AssignedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `AssignedCondition` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ClosedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ExceptionApprovedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExceptionReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ReturnCondition` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `ReturnedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_assets`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `SerialNumber` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Value` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |

## Table: `hr_attendance_attempts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Accepted` | `bool` | `boolean` | `NO` | ✅ Sync |
| `AttendancePolicyId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AttendanceSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecisionCode` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EventType` | `int` | `integer` | `NO` | ✅ Sync |
| `EvidenceJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IdempotencyKey` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `OccurredAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_attendance_breaks`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttendanceSessionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EndedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_attendance_corrections`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AppliedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `AppliedJson` | `string` | `jsonb` | `YES` | ✅ Sync |
| `AttendanceSessionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `BeforeJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecisionReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EvidenceReference` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `HrDecisionByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ManagerDecisionByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ProposedClockedInAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ProposedClockedOutAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_attendance_policies`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Kind` | `int` | `integer` | `NO` | ✅ Sync |
| `Latitude` | `decimal?` | `numeric(9,6)` | `YES` | ✅ Sync |
| `Longitude` | `decimal?` | `numeric(9,6)` | `YES` | ✅ Sync |
| `MaximumAccuracyMeters` | `int` | `integer` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `RadiusMeters` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_attendance_policy_assignments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttendancePolicyId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `EmployeeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ShiftTemplateId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_attendance_policy_exceptions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllowRemote` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ApprovedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EndsAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OverridePolicyId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `StartsAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_attendance_sessions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ClockedInAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ClockedOutAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EarlyLeaveMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LateMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `OvertimeMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `ShiftAssignmentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `WorkDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `WorkedMinutes` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_candidate_interviews`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CandidateId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Feedback` | `string` | `character varying(5000)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InterviewerUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ScheduledAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Score` | `decimal?` | `decimal(5,2)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_candidate_offers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AcceptedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `BaseSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CandidateId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Currency` | `string` | `character varying(3)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OfferNumber` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `ProposedStartDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_candidates`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CvAssetReference` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `Email` | `string` | `character varying(320)` | `YES` | ✅ Sync |
| `EmployeeProfileId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `FullName` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PhoneNumber` | `string` | `character varying(30)` | `NO` | ✅ Sync |
| `RequisitionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Stage` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_case_evidence`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AddedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `AssetReference` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ContentHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeCaseId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_case_responses`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttachmentReference` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeCaseId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Response` | `string` | `character varying(10000)` | `NO` | ✅ Sync |
| `SubmittedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_cost_centers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_disciplinary_actions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeCaseId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `FinancialAmount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayrollLineItemId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_employee_cases`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CaseNumber` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(10000)` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsConfidential` | `bool` | `boolean` | `NO` | ✅ Sync |
| `OpenedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_employee_compensations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BaseSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Currency` | `string` | `character varying(3)` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_employee_document_versions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssetReference` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ContentHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeDocumentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MimeType` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `SizeBytes` | `long` | `bigint` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UploadedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_employee_documents`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Category` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ExpiresOn` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsArchived` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IssuedOn` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `LegalHold` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `RetainUntil` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_employee_lifecycle_tasks`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssignedToUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CompletionNote` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DueAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Phase` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_employee_payrolls`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BaseSalarySnapshot` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Currency` | `string` | `character varying(3)` | `NO` | ✅ Sync |
| `Deductions` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EmployeeNameSnapshot` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `EmployeeNumberSnapshot` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `Gross` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Net` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PayrollRunId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_employment_assignments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ChangeReason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `CostCenterId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `JobGradeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `JobPositionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ManagerEmployeeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `OrganizationUnitId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WorkLocationId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

## Table: `hr_employment_contracts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BaseSalary` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ContractNumber` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Currency` | `string` | `character varying(3)` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EndDate` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ProbationEndDate` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `StartDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TermsJson` | `string` | `jsonb` | `YES` | ✅ Sync |
| `TermsVersion` | `int` | `integer` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_financial_installments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `AppliedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DueDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `FinancialRequestId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayrollLineItemId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Sequence` | `int` | `integer` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_financial_requests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ApprovalInstanceId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AttachmentReference` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OutstandingBalance` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `RequestedInstallments` | `int` | `integer` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_idempotency_records`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActorUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Key` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `RequestHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `ResponseJson` | `string` | `character varying(8000)` | `YES` | ✅ Sync |
| `ResultEntityId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Scope` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_job_grades`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Rank` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_job_positions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_leave_balances`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Carried` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Granted` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LeaveTypeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reserved` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Used` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `Year` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_leave_ledger_entries`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActorUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Amount` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EntryType` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LeaveBalanceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `SourceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_leave_policies`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllowNegativeBalance` | `bool` | `boolean` | `NO` | ✅ Sync |
| `AnnualEntitlement` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LeaveTypeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MaximumCarryover` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WorkCalendarId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `hr_leave_requests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovalInstanceId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AttachmentReference` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DayFraction` | `decimal` | `decimal(4,2)` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EndDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LeaveTypeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `ReservedAmount` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |
| `StartDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `Workdays` | `decimal` | `decimal(10,2)` | `NO` | ✅ Sync |

## Table: `hr_leave_types`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllowsHalfDay` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsPaid` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `RequiresAttachment` | `bool` | `boolean` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_migration_batches`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Module` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `ReconciledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ReportJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `RequestHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `SourceCount` | `int` | `integer` | `NO` | ✅ Sync |
| `SourceHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `SourceSystem` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `SourceTotal` | `decimal` | `decimal(24,4)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetCount` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetHash` | `string` | `character varying(128)` | `YES` | ✅ Sync |
| `TargetTotal` | `decimal` | `decimal(24,4)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_migration_conflicts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DetailsJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MigrationBatchId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ResolutionReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `ResolvedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SourceId` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_migration_record_maps`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(24,4)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MigrationBatchId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `SourceId` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `TargetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_module_rollouts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ChangedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ChangedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Module` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `ReadTarget` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `ReconciliationBatchId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WriteTarget` | `string` | `character varying(20)` | `NO` | ✅ Sync |

## Table: `hr_offboarding_processes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BlockersJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CompletedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InitiatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LastWorkingDate` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_organization_units`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ManagerEmployeeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `ParentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_pay_components`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Classification` | `int` | `integer` | `NO` | ✅ Sync |
| `Code` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsInsurable` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsTaxable` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_payroll_input_sources`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeePayrollId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayrollLineItemId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_payroll_line_items`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeePayrollId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Explanation` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InputsJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `IsAdjustment` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PayComponentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RuleVersionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SourceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_payroll_rules`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `Expression` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `PayComponentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Priority` | `int` | `integer` | `NO` | ✅ Sync |
| `Rate` | `decimal` | `decimal(18,4)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_payroll_runs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ClosedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CutoffAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FinanceReviewedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `FinanceReviewedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `GmApprovedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `GmApprovedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PaidAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PaidByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PeriodEnd` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `PeriodStart` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `PreparedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PreparedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ReconciliationHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `RunNumber` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `SourceDataVersion` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TotalDeductions` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `TotalGross` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `TotalNet` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_payroll_settlement_adjustments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OriginalPayrollLineItemId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `SettlementPayrollRunId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_payslips`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssetReference` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ContentHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeePayrollId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `GeneratedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_performance_cycles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EndsOn` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `StartsOn` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_performance_goals`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `PerformanceCycleId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Weight` | `decimal` | `decimal(5,2)` | `NO` | ✅ Sync |

## Table: `hr_performance_reviews`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AppealReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `AppealResolution` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ManagerUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PerformanceCycleId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ScoresJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `WeightedScore` | `decimal` | `decimal(5,2)` | `NO` | ✅ Sync |

## Table: `hr_requisitions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Openings` | `int` | `integer` | `NO` | ✅ Sync |
| `OrganizationUnitId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RequestedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Requirements` | `string` | `character varying(10000)` | `NO` | ✅ Sync |
| `RequisitionNumber` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `State` | `int` | `integer` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_shift_assignments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EffectiveFrom` | `DateOnly` | `date` | `NO` | ✅ Sync |
| `EffectiveTo` | `DateOnly?` | `date` | `YES` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PublishedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `ReplacesAssignmentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ShiftTemplateId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_shift_segments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DayOfWeek` | `int?` | `integer` | `YES` | ✅ Sync |
| `EndsAt` | `TimeSpan` | `interval` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Sequence` | `int` | `integer` | `NO` | ✅ Sync |
| `ShiftTemplateId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartsAt` | `TimeSpan` | `interval` | `NO` | ✅ Sync |
| `UnpaidBreakMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WorkDateRule` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_shift_swap_requests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecisionReason` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `HrDecisionByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ManagerDecisionByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `RequesterAssignmentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RequesterEmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetAssignmentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetEmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_shift_templates`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GraceMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `MinimumBreakMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `Mode` | `int` | `integer` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `OvertimeAfterMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `WorkCalendarId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `hr_trusted_attendance_devices`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ApprovedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `TokenHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_work_calendars`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `HolidaysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `TimeZoneId` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WorkingDaysMask` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `hr_work_locations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Address` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `Code` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GeofenceRadiusMeters` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Latitude` | `decimal?` | `numeric(9,6)` | `YES` | ✅ Sync |
| `Longitude` | `decimal?` | `numeric(9,6)` | `YES` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `hr_workday_classifications`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EmployeeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Kind` | `int` | `integer` | `NO` | ✅ Sync |
| `SourceId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SourceType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `int` | `integer` | `NO` | ✅ Sync |
| `WorkDate` | `DateOnly` | `date` | `NO` | ✅ Sync |

## Table: `incoming_sms_logs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Body` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DeduplicationHash` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsMatched` | `bool` | `boolean` | `NO` | ✅ Sync |
| `MatchedRechargeRequestId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ParsedAmount` | `decimal?` | `numeric(18,2)` | `YES` | ✅ Sync |
| `ParsedSenderPhone` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `ReceivedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Sender` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WalletId` | `Guid` | `uuid` | `NO` | ✅ Sync |

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
| `InternalCode` | `string` | `character varying(40)` | `NO` | ✅ Sync |
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
| `VideoTypeId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `lessons`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ContentSectionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExamId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InternalCode` | `string` | `character varying(40)` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `Price` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `Summary` | `string` | `text` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_action_executions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActionKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `AuditLogId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FailureCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IdempotencyKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `PayloadHash` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `SafeRequestJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `SafeResultJson` | `string` | `jsonb` | `YES` | ✅ Sync |
| `StaffUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_ai_conversation_states`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AutoCloseAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DisableRequestedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HandedOffAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HandoffReasonCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `HandoffSafeSummary` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `InactivityWarningSentAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LastEventSequence` | `long` | `bigint` | `NO` | ✅ Sync |
| `LastParticipantActivityAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `LastRecoveryAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Mode` | `int` | `integer` | `NO` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ResolutionCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `ResolvedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `SafeSummaryJson` | `string` | `jsonb` | `YES` | ✅ Sync |
| `VerifiedStudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_ai_knowledge_entries`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Title` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_ai_knowledge_revisions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Content` | `string` | `character varying(50000)` | `NO` | ✅ Sync |
| `ContentHash` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `EntryId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPublished` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PublishedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RevisionNumber` | `int` | `integer` | `NO` | ✅ Sync |
| `SearchText` | `string` | `character varying(50000)` | `NO` | ✅ Sync |
| `SourceLabel` | `string` | `character varying(300)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ValidFrom` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ValidUntil` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_ai_pending_actions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActionExecutionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ActionKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `CallbackDecisionHash` | `string` | `character varying(64)` | `YES` | ✅ Sync |
| `CancelledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConfirmationNonceHash` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `ConfirmedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConfirmedByGuestSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ConfirmedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecisionKind` | `int` | `integer` | `NO` | ✅ Sync |
| `EncryptedPayload` | `byte[]` | `bytea` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `FailureCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IdempotencyKey` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayloadHash` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SafeProposalJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `StateFingerprint` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TurnId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_ai_policy_knowledge_revisions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `KnowledgeRevisionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |

## Table: `live_support_ai_policy_versions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActionKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InactivityMinutes` | `int` | `integer` | `NO` | ✅ Sync |
| `InactivityWarningGraceSeconds` | `int` | `integer` | `NO` | ✅ Sync |
| `IsEnabled` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LookupKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `PendingActionExpirySeconds` | `int` | `integer` | `NO` | ✅ Sync |
| `PublishedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PublishedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ReadableDataKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `SystemInstructions` | `string` | `character varying(20000)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VerificationMaxAttempts` | `int` | `integer` | `NO` | ✅ Sync |
| `VerificationQuestionKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `VerificationRequiredCorrect` | `int` | `integer` | `NO` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |
| `VersionNumber` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_ai_turns`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CallbackAttemptCount` | `int` | `integer` | `NO` | ✅ Sync |
| `CallbackStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ContextCategoryKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DecisionHash` | `string` | `character varying(64)` | `YES` | ✅ Sync |
| `DecisionType` | `int?` | `integer` | `YES` | ✅ Sync |
| `ExpectedConversationVersion` | `long` | `bigint` | `NO` | ✅ Sync |
| `FailureCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `InputTokenCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `KnowledgeRevisionIdsJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `LastSafeCallbackErrorCode` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `LatencyMs` | `int?` | `integer` | `YES` | ✅ Sync |
| `Model` | `string` | `character varying(150)` | `YES` | ✅ Sync |
| `NextCallbackAttemptAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `OutputMessageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `OutputTokenCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Provider` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `ProviderCompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ProviderResponseId` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `QueuedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `SafeFailureDetail` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `SourceMessageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_ai_verification_attempts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttemptNumber` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OutcomeCodesJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `QuestionKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `SessionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SubmittedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_ai_verification_policy_questions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ComparisonMode` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Order` | `int` | `integer` | `NO` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PromptText` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `QuestionKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `SourceFieldKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_ai_verification_sessions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttemptCount` | `int` | `integer` | `NO` | ✅ Sync |
| `CandidateStudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CompletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CorrectCount` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentQuestionIndex` | `int` | `integer` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LastAttemptAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LockedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LookupKey` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `LookupValueHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `MaxAttempts` | `int` | `integer` | `NO` | ✅ Sync |
| `PolicyVersionId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `RequiredCorrect` | `int` | `integer` | `NO` | ✅ Sync |
| `SelectedQuestionKeysJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VerifiedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_assignments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssignedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AssignmentSequence` | `int` | `integer` | `NO` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `EndReason` | `int?` | `integer` | `YES` | ✅ Sync |
| `EndedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StaffUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `TransferReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_attachments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ContentType` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsBlocked` | `bool` | `boolean` | `NO` | ✅ Sync |
| `OriginalFileName` | `string` | `character varying(255)` | `NO` | ✅ Sync |
| `Sha256` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `SizeBytes` | `long` | `bigint` | `NO` | ✅ Sync |
| `StoragePath` | `string` | `character varying(2048)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UploadedByIdentity` | `string` | `character varying(150)` | `NO` | ✅ Sync |

## Table: `live_support_conversations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AssignedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CloseReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `ClosedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ClosedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CurrentOwnerUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `FirstStaffResponseAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `GuestSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LastMessageAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `LinkedStudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ParticipantType` | `int` | `integer` | `NO` | ✅ Sync |
| `PreviousConversationId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `QueuedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Subject` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActorGuestSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ActorUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OccurredAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `RelatedEntityId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RelatedEntityType` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `SafeMetadataJson` | `string` | `jsonb` | `YES` | ✅ Sync |
| `Sequence` | `long` | `bigint` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_guest_sessions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedIpHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `DisplayName` | `string` | `character varying(120)` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `LastSeenAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `PhoneNumber` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `RevokedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `SecurityStampHash` | `string` | `character varying(128)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserAgentSummary` | `string` | `character varying(300)` | `YES` | ✅ Sync |

## Table: `live_support_messages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AttachmentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ClientMessageId` | `string` | `character varying(100)` | `NO` | ✅ Sync |
| `Content` | `string` | `character varying(4000)` | `NO` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SenderGuestSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SenderType` | `int` | `integer` | `NO` | ✅ Sync |
| `SenderUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SentAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_queue_entries`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DequeueReason` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `DequeuedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `EnteredAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Sequence` | `long` | `bigint` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_ratings`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Comment` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Stars` | `int` | `integer` | `NO` | ✅ Sync |
| `SubmittedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `SubmittedByGuestSessionId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `SubmittedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_schedule_windows`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DayOfWeek` | `int` | `integer` | `NO` | ✅ Sync |
| `EndLocalTime` | `TimeOnly` | `time without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `StaffConfigId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StartLocalTime` | `TimeOnly` | `time without time zone` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `live_support_staff_configs`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ConfiguredByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsEnabled` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LastAssignedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `MaxActiveConversations` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `live_support_student_link_history`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ChangedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ChangedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ConversationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `NewStudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PreviousStudentUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Reason` | `string` | `character varying(500)` | `NO` | ✅ Sync |
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
| `AcademicScopeOwnerId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `AcademicScopeOwnerType` | `int?` | `integer` | `YES` | ✅ Sync |
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

## Table: `printable_code_batches`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Behavior` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreditAmount` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `DisableReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `DiscountType` | `int?` | `integer` | `YES` | ✅ Sync |
| `DiscountValue` | `decimal?` | `decimal(18,2)` | `YES` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(160)` | `NO` | ✅ Sync |
| `OwnerType` | `int` | `integer` | `NO` | ✅ Sync |
| `StackingPolicyId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `StartsAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TemplateId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TotalCodes` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UsedCount` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `printable_code_redemptions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AppliedAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PrintableCodeId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PurchaseOperationId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RequestId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `printable_code_templates`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BackgroundColor` | `string` | `character varying(32)` | `YES` | ✅ Sync |
| `BackgroundImageUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `HeightMm` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LayoutJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(160)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `WidthMm` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |

## Table: `printable_sales_codes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `BatchId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CodeHash` | `string` | `character varying(256)` | `NO` | ✅ Sync |
| `CodePlaintext` | `string` | `character varying(80)` | `YES` | ✅ Sync |
| `ConsumedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ConsumedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `QrPayload` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `SerialNumber` | `long` | `bigint` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UsageLimit` | `int` | `integer` | `NO` | ✅ Sync |
| `UsedCount` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `promotional_balance_allocations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AvailableAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ConsumedAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `ExpiredAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `GiftRecipientId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MaxPurchaseCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `OriginalAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PurchaseCount` | `int` | `integer` | `NO` | ✅ Sync |
| `RevokedAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `promotional_balance_usages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllocationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ContentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ContentType` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GiftRecipientId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PurchaseOperationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `public_exam_products`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AvailableFrom` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `AvailableUntil` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DisableReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `DisabledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `DisabledByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ExamId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `GradeLevel` | `string` | `character varying(80)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsPaid` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsPlatformWide` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsPublished` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Price` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Slug` | `string` | `character varying(160)` | `NO` | ✅ Sync |
| `SubjectId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

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

## Table: `recharge_requests`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `numeric(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `MatchedSmsLogId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `ReservationExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ResolvedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ResolvedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `ScreenshotUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `SenderPhoneNumber` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `WalletId` | `Guid` | `uuid` | `NO` | ✅ Sync |

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

## Table: `report_definitions`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ConfigurationJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Domain` | `string` | `character varying(64)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(120)` | `NO` | ✅ Sync |
| `OwnerUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SchemaVersion` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `xmin` | `uint` | `xid` | `NO` | ❌ Missing Column |

## Table: `roles`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllowedDomain` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `AllowedNavbarItemsJson` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(50)` | `NO` | ✅ Sync |
| `PermissionsJson` | `string` | `character varying(4000)` | `YES` | ✅ Sync |
| `Type` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `sales_coupon_usages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CouponId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DiscountAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `GrossAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PurchaseOperationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `sales_coupons`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Code` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `DisableReason` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `DiscountType` | `int` | `integer` | `NO` | ✅ Sync |
| `DiscountValue` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ExpiresAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `GlobalUsageLimit` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(160)` | `NO` | ✅ Sync |
| `NormalizedCode` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `OwnerType` | `int` | `integer` | `NO` | ✅ Sync |
| `PerStudentUsageLimit` | `int?` | `integer` | `YES` | ✅ Sync |
| `StackingPolicyId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `StartsAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TargetId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UsedCount` | `int` | `integer` | `NO` | ✅ Sync |

## Table: `sales_financial_effects`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CouponDiscountAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DetailsJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `GrossAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PaidAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PlatformShareImpact` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PrintableCodeDiscountAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PromotionalAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PurchaseOperationId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `StudentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TeacherShareImpact` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `sales_rules`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `GradeLevel` | `string` | `character varying(80)` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `SubjectId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TargetId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `VideoTypeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

## Table: `shared_teacher_package_items`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ContentId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ContentType` | `int` | `integer` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsIncluded` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Price` | `decimal` | `decimal(18,4)` | `NO` | ✅ Sync |
| `SharedTeacherPackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SubjectId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `shared_teacher_package_teachers`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllocationMode` | `int` | `integer` | `NO` | ✅ Sync |
| `AllocationValue` | `decimal` | `decimal(18,4)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `DisplayOrder` | `int` | `integer` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SharedTeacherPackageId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SubjectId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `shared_teacher_packages`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AvailableFrom` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `AvailableUntil` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Description` | `string` | `character varying(2000)` | `NO` | ✅ Sync |
| `DistributionMode` | `int` | `integer` | `NO` | ✅ Sync |
| `EducationStage` | `int?` | `integer` | `YES` | ✅ Sync |
| `GradeLevel` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `ImageUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `IsPublished` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Price` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Slug` | `string` | `character varying(160)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UpdatedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

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
| `GiftRecipientId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `GrantType` | `int` | `integer` | `NO` | ✅ Sync |
| `GrantedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LessonId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `LessonVideoId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `MaxUses` | `int?` | `integer` | `YES` | ✅ Sync |
| `PackageId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PublicExamProductId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TermId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UsesConsumed` | `int` | `integer` | `NO` | ✅ Sync |
| `VideoTypeId` | `Guid?` | `uuid` | `YES` | ✅ Sync |

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
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

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

## Table: `student_facing_academic_scopes`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `EducationStage` | `int?` | `integer` | `YES` | ✅ Sync |
| `GradeLevel` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OwnerId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `OwnerType` | `int` | `integer` | `NO` | ✅ Sync |
| `ScopeLevel` | `int` | `integer` | `NO` | ✅ Sync |
| `SubjectId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

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
| `HasSeenTrackingCodePopup` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsFatherAlive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsMotherAlive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LightThemePaletteId` | `string` | `character varying(100)` | `YES` | ✅ Sync |
| `MotherDateOfBirth` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `MotherPhone` | `string` | `text` | `YES` | ✅ Sync |
| `Nationality` | `string` | `text` | `YES` | ✅ Sync |
| `ParentPhone` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `ParentTrackingCode` | `string` | `character varying(6)` | `YES` | ✅ Sync |
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
| `ReservedBalance` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TotalEarnings` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `Version` | `long` | `bigint` | `NO` | ✅ Sync |

## Table: `teacher_financial_allocations`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AllocationMode` | `int` | `integer` | `NO` | ✅ Sync |
| `AllocationValue` | `decimal` | `decimal(18,4)` | `NO` | ✅ Sync |
| `CodeSerialNumber` | `long?` | `bigint` | `YES` | ✅ Sync |
| `ContentNameSnapshot` | `string` | `character varying(300)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `GrossBasisAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PayoutId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `PayoutStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `PlatformShareAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ReviewStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentNameSnapshot` | `string` | `character varying(200)` | `YES` | ✅ Sync |
| `StudentPhoneSnapshot` | `string` | `character varying(20)` | `YES` | ✅ Sync |
| `TeacherFinancialEventId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TeacherShareAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_financial_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Currency` | `string` | `character varying(3)` | `NO` | ✅ Sync |
| `DetailsJson` | `string` | `jsonb` | `NO` | ✅ Sync |
| `DiscountAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `GrossAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IdempotencyKey` | `string` | `character varying(240)` | `NO` | ✅ Sync |
| `OccurredAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `PaidAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PayoutStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `PlatformShareAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `PromotionalAmount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ReviewStatus` | `int` | `integer` | `NO` | ✅ Sync |
| `SourceId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `SourceType` | `int` | `integer` | `NO` | ✅ Sync |
| `StudentId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `TargetId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TargetType` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_payout_adjustments`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Reason` | `string` | `character varying(1000)` | `NO` | ✅ Sync |
| `RelatedFinancialEventId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RelatedPayoutId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `teacher_payouts`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `AdminNote` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Amount` | `decimal` | `decimal(18,2)` | `NO` | ✅ Sync |
| `ApprovedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `ApprovedByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `HandledAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `HandledByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PaidAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `PaidByUserId` | `Guid?` | `uuid` | `YES` | ✅ Sync |
| `RejectionReason` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `Status` | `int` | `integer` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `TransferReference` | `string` | `character varying(200)` | `YES` | ✅ Sync |
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
| `IntroVideoUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `IsContentVisibleToStudents` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsPublicProfileEnabled` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsVisibleToStudents` | `bool` | `boolean` | `NO` | ✅ Sync |
| `ProfileImageUrl` | `string` | `character varying(1000)` | `YES` | ✅ Sync |
| `PublicBio` | `string` | `character varying(2000)` | `YES` | ✅ Sync |
| `PublicSlug` | `string` | `character varying(160)` | `YES` | ✅ Sync |
| `RatingAverage` | `decimal` | `numeric(5,2)` | `NO` | ✅ Sync |
| `RatingCount` | `int` | `integer` | `NO` | ✅ Sync |
| `ShowOnLanding` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Specialization` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `TelegramUrl` | `string` | `text` | `YES` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `YouTubeUrl` | `string` | `text` | `YES` | ✅ Sync |

## Table: `teacher_staff_members`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CreatedByTeacherUserId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Notes` | `string` | `character varying(500)` | `YES` | ✅ Sync |
| `PermissionKeys` | `string` | `character varying(500)` | `NO` | ✅ Sync |
| `TeacherId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `UserId` | `Guid` | `uuid` | `NO` | ✅ Sync |

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
| `DeletedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |
| `FullName` | `string` | `character varying(200)` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsDeleted` | `bool` | `boolean` | `NO` | ✅ Sync |
| `IsProfileComplete` | `bool` | `boolean` | `NO` | ✅ Sync |
| `PasswordHash` | `string` | `text` | `NO` | ✅ Sync |
| `PasswordResetVersion` | `int` | `integer` | `NO` | ✅ Sync |
| `PhoneNumber` | `string` | `character varying(20)` | `NO` | ✅ Sync |
| `SecurityStampVersion` | `int` | `integer` | `NO` | ✅ Sync |
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

## Table: `video_types`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsActive` | `bool` | `boolean` | `NO` | ✅ Sync |
| `Name` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `NormalizedName` | `string` | `character varying(80)` | `NO` | ✅ Sync |
| `SortOrder` | `int` | `integer` | `NO` | ✅ Sync |
| `UpdatedAt` | `DateTime?` | `timestamp without time zone` | `YES` | ✅ Sync |

## Table: `video_watch_events`

| Column Name | C# Type | Database Type | Nullable? | Status |
| --- | --- | --- | --- | --- |
| `ActualWatchedSeconds` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `CreatedAt` | `DateTime` | `timestamp without time zone` | `NO` | ✅ Sync |
| `CustomMaxWatchCount` | `int?` | `integer` | `YES` | ✅ Sync |
| `Id` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `IsLocked` | `bool` | `boolean` | `NO` | ✅ Sync |
| `LastPlaybackRate` | `decimal` | `numeric` | `NO` | ✅ Sync |
| `LessonVideoId` | `Guid` | `uuid` | `NO` | ✅ Sync |
| `PlaybackRateBreakdownJson` | `string` | `text` | `NO` | ✅ Sync |
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

