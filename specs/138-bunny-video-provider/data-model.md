# Data Model: Bunny Video Provider

## Existing Entity Changes

### LessonVideo

Existing provider-backed lesson video entity.

**Changes**:
- `Provider` accepts `YouTube`, `vk`, and `bunny` case-insensitively.
- `ProviderVideoId` for Bunny stores the Bunny video GUID returned by Bunny Stream.
- No Bunny cost fields are added to `LessonVideo`.

**Relationships**:
- `LessonVideo` has zero or one `BunnyVideoAsset`.

## New Entities

### BunnyVideoAsset

Stores Bunny-specific video, upload, processing, and attribution metadata.

**Fields**:
- `Id: Guid`
- `LessonVideoId: Guid`
- `TeacherId: Guid`
- `PackageId: Guid`
- `LessonId: Guid`
- `UploadedByUserId: Guid`
- `BunnyLibraryId: long`
- `BunnyVideoGuid: string`
- `BunnyCollectionId: string?`
- `Title: string`
- `UploadMethod: string` (`TusFile`, `UrlFetch`, `ManualLink`)
- `Status: string` (`Created`, `Uploading`, `Uploaded`, `Processing`, `Ready`, `Failed`, `Deleted`, `Unknown`)
- `OriginalFileName: string?`
- `SourceUrlHash: string?`
- `FileSizeBytes: long?`
- `DurationSeconds: int?`
- `StorageBytes: long?`
- `BandwidthBytes: long?`
- `BunnyEncodeProgress: int?`
- `LastStatusSyncedAtUtc: DateTime?`
- `LastUsageSyncedAtUtc: DateTime?`
- `ErrorMessage: string?`
- `CreatedAtUtc: DateTime`
- `UpdatedAtUtc: DateTime?`

**Relationships**:
- Required `LessonVideo`.
- Required `TeacherProfile`.
- Required `Package`.
- Required `Lesson`.
- Required upload actor `User`.
- Many `BunnyUsageSnapshot` rows.

**Indexes/Constraints**:
- Unique index on `BunnyVideoGuid`.
- Unique index on `LessonVideoId`.
- Index on `(TeacherId, PackageId, LessonId)`.
- Index on `(Status, LastStatusSyncedAtUtc)`.

**Validation Rules**:
- `LessonVideo.Provider` must be Bunny when a `BunnyVideoAsset` is created.
- Admin upload must validate selected lesson belongs to selected package and selected teacher.
- Teacher upload must validate the teacher owns/manages the selected lesson.
- A Bunny asset is not considered playable until `BunnyVideoGuid` exists and status is at least `Uploaded`/`Processing`; UI should prefer `Ready` for student playback when processing status is known.

### BunnyUsageSnapshot

Stores monthly or dated usage/cost measurements for a Bunny video.

**Fields**:
- `Id: Guid`
- `BunnyVideoAssetId: Guid`
- `TeacherId: Guid`
- `PackageId: Guid`
- `LessonId: Guid`
- `PeriodStartUtc: DateTime`
- `PeriodEndUtc: DateTime`
- `StorageBytes: long`
- `BandwidthBytes: long`
- `IsBandwidthEstimated: bool`
- `BandwidthSource: string` (`PerVideoApi`, `LibraryTrafficAllocatedByWatchTime`, `Manual`, `Unavailable`)
- `StorageRateUsdPerGb: decimal`
- `BandwidthRateUsdPerGb: decimal`
- `StorageCostUsd: decimal`
- `BandwidthCostUsd: decimal`
- `TotalCostUsd: decimal`
- `BunnyStorageCalculatedAtUtc: DateTime?`
- `SyncedAtUtc: DateTime`
- `SyncedByUserId: Guid?`
- `Notes: string?`

**Relationships**:
- Required `BunnyVideoAsset`.
- Denormalized teacher/package/lesson IDs for fast monthly reporting and historical attribution.

**Indexes/Constraints**:
- Unique index on `(BunnyVideoAssetId, PeriodStartUtc, PeriodEndUtc)`.
- Index on `(TeacherId, PeriodStartUtc, PeriodEndUtc)`.
- Index on `(PackageId, PeriodStartUtc, PeriodEndUtc)`.

**Calculation Rules**:
- `StorageCostUsd = bytesToGb(StorageBytes) * StorageRateUsdPerGb`.
- `BandwidthCostUsd = bytesToGb(BandwidthBytes) * BandwidthRateUsdPerGb`.
- `TotalCostUsd = StorageCostUsd + BandwidthCostUsd`.
- Rates are copied from platform settings at sync time and never mutated retroactively.

### Platform Settings

Existing key/value settings store.

**New keys**:
- `BunnyStreamStorageRateUsdPerGb`: default `0.01`
- `BunnyStreamBandwidthRateUsdPerGb`: default `0.005`

**Configuration, not database settings**:
- `BunnyStream:LibraryId`
- `BunnyStream:ApiKey`
- `BunnyStream:CdnHostname` or player host context if needed
- `BunnyStream:TusUploadExpiryMinutes`

**Security Rule**: Bunny API key and signing inputs are environment/config secrets, not returned by settings APIs.

## Read Models

### BunnyUploadSessionDto

Returned to frontend after creating a TUS upload session.

**Fields**:
- `lessonVideoId`
- `bunnyVideoAssetId`
- `bunnyVideoGuid`
- `libraryId`
- `tusEndpoint`
- `authorizationSignature`
- `authorizationExpire`
- `uploadHeaders`
- `status`

**Excluded**:
- Bunny API key.
- Cost fields.

### BunnyCostReportDto

Admin-only report model.

**Fields**:
- `periodStart`
- `periodEnd`
- `platformTotalCostUsd`
- `platformStorageBytes`
- `platformBandwidthBytes`
- `videos[]`
- `teachers[]`
- `packages[]`
- `snapshotCount`
- `estimatedBandwidthCount`

## State Transitions

```text
Created -> Uploading -> Uploaded -> Processing -> Ready
Created -> Failed
Uploading -> Failed
Processing -> Failed
Ready -> Deleted
Ready -> Unknown
```

URL fetch may start at `Processing` after Bunny accepts fetch, but must remain non-playable until a Bunny video GUID is resolved.
