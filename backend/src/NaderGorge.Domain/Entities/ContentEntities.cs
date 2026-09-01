using NaderGorge.Domain.Common;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Domain.Entities;

/// <summary>
/// Package represents the academic year.
/// Contains Terms directly (no separate Year entity).
/// </summary>
public class Package : BaseEntity, IArchivableContent
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }
    public PackageContentMode ContentMode { get; set; } = PackageContentMode.TermWithSections;
    public bool AllowFullPackagePurchase { get; set; } = true;
    public AiOutputLanguage AiOutputLanguage { get; set; } = AiOutputLanguage.Auto;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    public string TargetGrade { get; set; } = string.Empty;

    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public ICollection<Term> Terms { get; set; } = new List<Term>();
}

public class ContentSection : BaseEntity, IArchivableContent
{
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Order { get; set; }
    public decimal Price { get; set; }
    public bool IsSystemContainer { get; set; }
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Guid TermId { get; set; }
    public Term Term { get; set; } = null!;

    public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
}

public class Lesson : BaseEntity, IArchivableContent
{
    public string InternalCode { get; private set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public int Order { get; set; }
    public decimal Price { get; set; }
    /// <summary>
    /// Optional Cairo calendar date shown to students while the lesson homework is
    /// still being prepared. This is an announcement only; it never grants access.
    /// </summary>
    public DateOnly? HomeworkComingSoonOn { get; set; }
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Guid ContentSectionId { get; set; }
    public ContentSection ContentSection { get; set; } = null!;

    // Optional Exam associated with the lesson
    public Guid? ExamId { get; set; }

    public ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
    public ICollection<LessonResource> Resources { get; set; } = new List<LessonResource>();
    public ICollection<LessonComment> Comments { get; set; } = new List<LessonComment>();
}

public class LessonVideo : BaseEntity, IArchivableContent
{
    public string InternalCode { get; private set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    // e.g., YouTube, Vimeo, custom
    public string Provider { get; set; } = string.Empty;
    public string ProviderVideoId { get; set; } = string.Empty;

    /// <summary>
    /// Monotonically advances only when the playable source changes. Pending Bunny
    /// replacements capture this value so an older candidate cannot overwrite a
    /// newer admin source edit when it eventually becomes ready.
    /// </summary>
    public int SourceRevision { get; set; }

    public int Order { get; set; }

    public int MaxWatchCount { get; set; } = 3; // Hard-lock limit

    /// <summary>Admin-assigned type/tag for the video</summary>
    public string? VideoTag { get; set; }

    public Guid VideoTypeId { get; set; }
    public VideoType VideoType { get; set; } = null!;

    public string? SubtitleUrl { get; set; }
    public bool IsProcessingAI { get; set; } = false;
    public bool IsProcessingMindmaps { get; set; } = false;
    public Guid? CurrentAiAnalysisRunId { get; set; }
    public Guid? CurrentMindmapGenerationRunId { get; set; }
    public bool IsActive { get; set; } = true;
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    /// <summary>
    /// The Bunny Stream library that owns this video. This is intentionally stored on
    /// the lesson video (rather than only on uploaded assets) so manually linked Bunny
    /// videos also retain an unambiguous playback library.
    /// </summary>
    public Guid? BunnyStreamLibraryId { get; set; }
    public BunnyStreamLibrary? BunnyStreamLibrary { get; set; }

    // Optional Exam associated directly with this video specific
    public Guid? ExamId { get; set; }
    public Exam? Exam { get; set; }

    public ICollection<VideoChapter> VideoChapters { get; set; } = new List<VideoChapter>();

    /// <summary>
    /// Bunny assets ever associated with this logical lesson video. Exactly one
    /// asset can be the current playback source; completed replacements retain
    /// their former assets here so historical usage and cost evidence is not lost.
    /// </summary>
    public ICollection<BunnyVideoAsset> BunnyVideoAssets { get; set; } = new List<BunnyVideoAsset>();
}

public class BunnyStreamLibrary : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public long ExternalLibraryId { get; set; }
    public byte[]? ApiKeyCiphertext { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastValidatedAtUtc { get; set; }

    public ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
}

public static class BunnyStreamLibrarySeedIds
{
    public static readonly Guid First = Guid.Parse("a5d123ac-0b9f-4f69-9d15-740733000001");
    public static readonly Guid Second = Guid.Parse("a5d123ac-0b9f-4f69-9d15-740737000002");
    public static readonly Guid Massar = Guid.Parse("a5d123ac-0b9f-4f69-9d15-740801000003");
}

public class VideoType : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<LessonVideo> Videos { get; set; } = new List<LessonVideo>();
}

public class BunnyVideoAsset : BaseEntity
{
    public Guid LessonVideoId { get; set; }
    public LessonVideo LessonVideo { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public TeacherProfile Teacher { get; set; } = null!;

    public Guid PackageId { get; set; }
    public Package Package { get; set; } = null!;

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public Guid UploadedByUserId { get; set; }
    public User UploadedByUser { get; set; } = null!;

    public long BunnyLibraryId { get; set; }
    public string BunnyVideoGuid { get; set; } = string.Empty;
    public string? BunnyCollectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string UploadMethod { get; set; } = string.Empty;
    public string Status { get; set; } = "Created";
    public string? OriginalFileName { get; set; }
    public string? SourceUrlHash { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? DurationSeconds { get; set; }
    public long? StorageBytes { get; set; }
    public long? BandwidthBytes { get; set; }
    public int? BunnyEncodeProgress { get; set; }
    public DateTime? LastStatusSyncedAtUtc { get; set; }
    public DateTime? LastUsageSyncedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public bool ActivateWhenReady { get; set; }

    /// <summary>
    /// Current assets control Bunny playback readiness. Pending replacements do
    /// not affect the existing lesson video until Bunny reports them ready;
    /// retired assets remain for immutable finance history.
    /// </summary>
    public BunnyVideoAssetSourceState SourceState { get; set; } = BunnyVideoAssetSourceState.Current;

    /// <summary>
    /// The configured library record selected for this asset. It is stored on
    /// pending replacements before the lesson video itself is switched.
    /// </summary>
    public Guid? BunnyStreamLibraryRecordId { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    public Guid? RetiredByUserId { get; set; }

    /// <summary>
    /// Records that a later successful source change superseded this terminal
    /// replacement outcome. The asset and its financial history remain intact,
    /// but the cockpit should no longer surface the stale failure as active.
    /// </summary>
    public DateTime? OutcomeSupersededAtUtc { get; set; }

    // Target metadata is populated only while SourceState is PendingReplacement.
    public int? TargetOrder { get; set; }
    public int? TargetMaxWatchCount { get; set; }
    public Guid? TargetVideoTypeId { get; set; }
    public bool? TargetIsActive { get; set; }

    /// <summary>
    /// SourceRevision of the logical video when this pending replacement was
    /// created. A candidate is promoted only if that source is still current.
    /// </summary>
    public int? TargetSourceRevision { get; set; }

    public ICollection<BunnyUsageSnapshot> UsageSnapshots { get; set; } = new List<BunnyUsageSnapshot>();
}

public class BunnyUsageSnapshot : BaseEntity
{
    public Guid BunnyVideoAssetId { get; set; }
    public BunnyVideoAsset BunnyVideoAsset { get; set; } = null!;

    public Guid TeacherId { get; set; }
    public Guid PackageId { get; set; }
    public Guid LessonId { get; set; }

    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public long StorageBytes { get; set; }
    public long BandwidthBytes { get; set; }
    public bool IsBandwidthEstimated { get; set; }
    public string BandwidthSource { get; set; } = "Unavailable";
    public decimal StorageRateUsdPerGb { get; set; }
    public decimal BandwidthRateUsdPerGb { get; set; }
    public decimal StorageCostUsd { get; set; }
    public decimal BandwidthCostUsd { get; set; }
    public decimal TotalCostUsd { get; set; }
    public DateTime? BunnyStorageCalculatedAtUtc { get; set; }
    public DateTime SyncedAtUtc { get; set; }
    public Guid? SyncedByUserId { get; set; }
    public User? SyncedByUser { get; set; }
    public string? Notes { get; set; }
}

public class LessonResource : BaseEntity, IArchivableContent
{
    public string Title { get; set; } = string.Empty;

    // URL or file path
    public string FileUrl { get; set; } = string.Empty;

    // e.g., PDF, Image
    public string ResourceType { get; set; } = string.Empty;
    public ContentArchiveMode ArchiveMode { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;
}

public class LessonComment : BaseEntity
{
    public Guid LessonId { get; set; }
    public Lesson Lesson { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public User AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public LessonCommentStatus Status { get; set; } = LessonCommentStatus.Pending;

    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
