using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;
using Npgsql;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class LiveSupportQueryBudgetTests
{
    private const string EvidenceOutputVariable = "LIVE_SUPPORT_QUERY_BUDGET_EVIDENCE_OUTPUT";
    private const string EvidenceRootVariable = "LIVE_SUPPORT_QUERY_BUDGET_EVIDENCE_ROOT";
    private const string SourceManifestVariable = "LIVE_SUPPORT_QUERY_BUDGET_SOURCE_MANIFEST";
    private const string DatabaseAuthorizationVariable = "LIVE_SUPPORT_QUERY_BUDGET_DATABASE_AUTHORIZATION";
    private const string RequiredDatabaseAuthorization = "DELETE-DISPOSABLE-LIVE-SUPPORT-QUERY-BUDGET-DATABASE";
    private const string RequiredDatabasePrefix = "massar_live_support_query_budget_disposable_";
    private static readonly string[] ForbiddenDatabaseNameFragments =
        ["prod", "shared", "default", "postgres", "template"];
    private static readonly int[] RepresentativeRowCounts = [1, 20, 100];
    private static readonly int MaximumDatabaseCommands = ReadMaximumDatabaseCommands();

    [Fact]
    public async Task RepresentativeRows_KeepDashboardHistoryAndTimelineWithinFixedBudgets()
    {
        var authorizedDatabase = RequireAuthorizedDisposableDatabase();
        await using var fixture = new PostgresLiveSupportFixture();
        var measurements = new List<QueryBudgetMeasurement>();
        foreach (var rowCount in RepresentativeRowCounts)
            measurements.Add(await MeasureAsync(fixture, rowCount));

        var database = await ReadDatabaseEvidenceAsync(fixture, authorizedDatabase);
        Assert.All(measurements, AssertMeasurementWithinBudget);
        Assert.InRange(MaximumObserved(measurements), 1, MaximumDatabaseCommands);
        await WriteEvidenceIfRequestedAsync(measurements, database);
    }

    [Fact]
    public void EvidenceProjection_WithFixedMeasurements_IsDeterministicAndRedacted()
    {
        var measurements = RepresentativeRowCounts
            .Select(rowCount => new QueryBudgetMeasurement(
                rowCount,
                new QueryObservation(rowCount, rowCount),
                new QueryObservation(rowCount + 1, rowCount),
                new QueryObservation(rowCount + 2, rowCount * 2)))
            .ToArray();
        var source = new SourceBinding(
            "src-" + new string('a', 40),
            new string('b', 40),
            new string('a', 64),
            true,
            "massar-release-snapshot-sha256-v2",
            new string('c', 64));
        var database = new DatabaseEvidence(
            RequiredDatabasePrefix + "guard",
            new string('d', 64),
            "16.4",
            160004);

        var first = SerializeEvidence(BuildEvidence(source, database, measurements));
        var second = SerializeEvidence(BuildEvidence(source, database, measurements));

        Assert.Equal(first, second);
        Assert.DoesNotContain("phone", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("studentId", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("conversationId", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", first, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("host", first, StringComparison.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(first);
        Assert.Equal(
            ["schemaVersion", "evidenceType", "source", "database", "rowCounts", "measurements", "workflows"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            102,
            document.RootElement.GetProperty("workflows")
                .GetProperty("live-support-admin")
                .GetProperty("maximumDatabaseCommandsObserved")
                .GetInt32());
    }

    [Fact]
    public void SourceBinding_WithMismatchedReleaseIdentity_IsRejected()
    {
        var source = new SourceBinding(
            "src-" + new string('c', 40),
            new string('b', 40),
            new string('a', 64),
            true,
            "massar-release-snapshot-sha256-v2",
            new string('d', 64));

        Assert.Throws<InvalidDataException>(() => ValidateSourceBinding(source));
    }

    [Fact]
    public void SourceManifest_WithValidBinding_RecordsManifestSha256()
    {
        var directory = Directory.CreateTempSubdirectory("live-support-source-manifest-");
        try
        {
            var manifestPath = Path.Combine(directory.FullName, "manifest.json");
            var manifest = $$"""
                {
                  "releaseId": "src-{{new string('a', 40)}}",
                  "gitCommit": "{{new string('b', 40)}}",
                  "sourceStateSha256": "{{new string('a', 64)}}",
                  "dirtySourceSnapshot": true,
                  "sourceDigestAlgorithm": "massar-release-snapshot-sha256-v2"
                }
                """;
            File.WriteAllText(manifestPath, manifest, new UTF8Encoding(false));

            var source = ReadSourceBinding(RequireRegularFile(manifestPath, "source manifest"));

            var expectedSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(manifestPath))).ToLowerInvariant();
            Assert.Equal(expectedSha256, source.ManifestSha256);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("massar_production")]
    [InlineData("shared")]
    [InlineData("default")]
    [InlineData("postgres")]
    [InlineData("massar_live_support_query_budget_disposable_prod_copy")]
    public void DatabaseAuthorization_WithSharedOrProductionName_IsRejected(string databaseName)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValidateDisposableDatabaseAuthorization(RequiredDatabaseAuthorization, databaseName));
    }

    [Fact]
    public void DatabaseAuthorization_WithoutExplicitDestructiveConsent_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ValidateDisposableDatabaseAuthorization("yes", RequiredDatabasePrefix + "guard"));
    }

    [Fact]
    public void EvidenceOutput_OutsideReleaseEvidenceRoot_IsRejected()
    {
        var evidenceRoot = Directory.CreateTempSubdirectory("live-support-query-budget-");
        try
        {
            var valid = Path.Combine(evidenceRoot.FullName, "raw.json");
            Assert.Equal(Path.GetFullPath(valid), ValidateEvidenceOutputPath(evidenceRoot.FullName, valid));
            Assert.Throws<InvalidDataException>(() =>
                ValidateEvidenceOutputPath(evidenceRoot.FullName, Path.Combine(evidenceRoot.FullName, "..", "raw.json")));
        }
        finally
        {
            evidenceRoot.Delete(recursive: true);
        }
    }

    private static AuthorizedDatabase RequireAuthorizedDisposableDatabase()
    {
        var authorization = Environment.GetEnvironmentVariable(DatabaseAuthorizationVariable);
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings__DefaultConnection is required for PostgreSQL integration evidence.");

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) || string.IsNullOrWhiteSpace(builder.Host))
            throw new InvalidOperationException("Evidence connection must name an explicit PostgreSQL host and database.");
        var databaseName = builder.Database;
        ValidateDisposableDatabaseAuthorization(authorization, databaseName);
        var identityInput = $"{builder.Host.ToLowerInvariant()}:{builder.Port}/{databaseName.ToLowerInvariant()}";
        return new AuthorizedDatabase(
            databaseName,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identityInput))).ToLowerInvariant());
    }

    private static void ValidateDisposableDatabaseAuthorization(
        string? authorization,
        string databaseName)
    {
        if (!string.Equals(authorization, RequiredDatabaseAuthorization, StringComparison.Ordinal))
            throw new InvalidOperationException($"{DatabaseAuthorizationVariable} does not authorize destructive fixture reset.");

        var normalized = databaseName.Trim().ToLowerInvariant();
        if (!normalized.StartsWith(RequiredDatabasePrefix, StringComparison.Ordinal) ||
            ForbiddenDatabaseNameFragments.Any(normalized.Contains))
        {
            throw new InvalidOperationException(
                $"Evidence database must use the disposable prefix {RequiredDatabasePrefix} and must not be shared or production-like.");
        }
    }

    private static async Task<DatabaseEvidence> ReadDatabaseEvidenceAsync(
        PostgresLiveSupportFixture fixture,
        AuthorizedDatabase authorizedDatabase)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT current_database(), current_setting('server_version'), " +
            "current_setting('server_version_num')::integer";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var databaseName = reader.GetString(0);
        Assert.Equal(authorizedDatabase.DatabaseName, databaseName);
        return new DatabaseEvidence(
            databaseName,
            authorizedDatabase.IdentitySha256,
            reader.GetString(1),
            reader.GetInt32(2));
    }

    private static void AssertMeasurementWithinBudget(QueryBudgetMeasurement measurement)
    {
        Assert.All(
            new[] { measurement.Dashboard, measurement.History, measurement.Timeline },
            observation => Assert.InRange(observation.DatabaseCommands, 1, MaximumDatabaseCommands));
    }

    private static async Task<QueryBudgetMeasurement> MeasureAsync(
        PostgresLiveSupportFixture fixture,
        int rowCount)
    {
        await fixture.ResetAsync();
        var seeded = await SeedAsync(fixture.Db, rowCount);
        var counter = new CommandCounterInterceptor();
        await using var db = CreateMeasuredContext(fixture.ConnectionString, counter);
        var service = new LiveSupportService(db, new EnabledSettings());

        var dashboard = await MeasureDashboardAsync(service, counter, rowCount);
        var history = await MeasureHistoryAsync(service, counter, seeded, rowCount);
        var timeline = await MeasureTimelineAsync(service, counter, seeded, rowCount);
        return new QueryBudgetMeasurement(rowCount, dashboard, history, timeline);
    }

    private static async Task<QueryObservation> MeasureDashboardAsync(
        LiveSupportService service,
        CommandCounterInterceptor counter,
        int rowCount)
    {
        counter.Reset();
        var dashboard = await service.GetAdminDashboardAsync(CancellationToken.None);
        Assert.Equal(rowCount, dashboard.Conversations.Count);
        return new QueryObservation(counter.CommandCount, dashboard.Conversations.Count);
    }

    private static async Task<QueryObservation> MeasureHistoryAsync(
        LiveSupportService service,
        CommandCounterInterceptor counter,
        SeededSupportData seeded,
        int rowCount)
    {
        counter.Reset();
        var history = await service.GetStudentSupportHistoryAsync(
            seeded.StaffId,
            true,
            seeded.ConversationId,
            CancellationToken.None);
        Assert.Equal(rowCount, history.Count);
        return new QueryObservation(counter.CommandCount, history.Count);
    }

    private static async Task<QueryObservation> MeasureTimelineAsync(
        LiveSupportService service,
        CommandCounterInterceptor counter,
        SeededSupportData seeded,
        int rowCount)
    {
        counter.Reset();
        var timeline = await service.GetAdminTimelineAsync(
            seeded.ConversationId,
            CancellationToken.None);
        Assert.True(timeline.Items.Count >= rowCount);
        return new QueryObservation(counter.CommandCount, timeline.Items.Count);
    }

    private static async Task WriteEvidenceIfRequestedAsync(
        IReadOnlyList<QueryBudgetMeasurement> measurements,
        DatabaseEvidence database)
    {
        var requestedOutput = Environment.GetEnvironmentVariable(EvidenceOutputVariable);
        if (string.IsNullOrWhiteSpace(requestedOutput))
            return;

        var evidenceRoot = Environment.GetEnvironmentVariable(EvidenceRootVariable);
        if (string.IsNullOrWhiteSpace(evidenceRoot))
            throw new InvalidOperationException($"{EvidenceRootVariable} is required when evidence output is enabled.");
        var sourceManifest = Environment.GetEnvironmentVariable(SourceManifestVariable);
        if (string.IsNullOrWhiteSpace(sourceManifest))
            throw new InvalidOperationException($"{SourceManifestVariable} is required when evidence output is enabled.");

        var source = ReadSourceBinding(RequireRegularFile(sourceManifest, "source manifest"));
        var outputPath = ValidateEvidenceOutputPath(evidenceRoot, requestedOutput);
        var evidenceJson = SerializeEvidence(BuildEvidence(source, database, measurements));
        await WriteNewRegularFileAsync(outputPath, evidenceJson);
    }

    private static QueryBudgetEvidence BuildEvidence(
        SourceBinding source,
        DatabaseEvidence database,
        IReadOnlyList<QueryBudgetMeasurement> measurements)
    {
        var ordered = measurements.OrderBy(measurement => measurement.RowCount).ToArray();
        Assert.Equal(RepresentativeRowCounts, ordered.Select(measurement => measurement.RowCount));
        return new QueryBudgetEvidence(
            1,
            "live-support-query-budget",
            source,
            database,
            RepresentativeRowCounts,
            ordered,
            new WorkflowEvidence(new LiveSupportWorkflowEvidence(MaximumObserved(ordered))));
    }

    private static int MaximumObserved(IReadOnlyList<QueryBudgetMeasurement> measurements) =>
        measurements.Max(measurement => new[]
        {
            measurement.Dashboard.DatabaseCommands,
            measurement.History.DatabaseCommands,
            measurement.Timeline.DatabaseCommands
        }.Max());

    private static string SerializeEvidence(QueryBudgetEvidence evidence) =>
        JsonSerializer.Serialize(
            evidence,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

    private static SourceBinding ReadSourceBinding(string manifestPath)
    {
        var manifestBytes = File.ReadAllBytes(manifestPath);
        using var document = JsonDocument.Parse(manifestBytes);
        var root = document.RootElement;
        var binding = new SourceBinding(
            RequiredString(root, "releaseId"),
            RequiredString(root, "gitCommit"),
            RequiredString(root, "sourceStateSha256"),
            root.GetProperty("dirtySourceSnapshot").GetBoolean(),
            RequiredString(root, "sourceDigestAlgorithm"),
            Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant());
        ValidateSourceBinding(binding);
        return binding;
    }

    private static void ValidateSourceBinding(SourceBinding binding)
    {
        if (!IsLowerHex(binding.GitCommit, 40) ||
            !IsLowerHex(binding.SourceStateSha256, 64) ||
            !IsLowerHex(binding.ManifestSha256, 64))
            throw new InvalidDataException("Source manifest contains an invalid Git or source digest.");
        if (binding.SourceDigestAlgorithm != "massar-release-snapshot-sha256-v2")
            throw new InvalidDataException("Source manifest uses an unsupported source digest algorithm.");

        var expectedRelease = binding.DirtySourceSnapshot
            ? $"src-{binding.SourceStateSha256[..40]}"
            : $"git-{binding.GitCommit}";
        if (!string.Equals(binding.ReleaseId, expectedRelease, StringComparison.Ordinal))
            throw new InvalidDataException("Source manifest release identity does not match its source binding.");
    }

    private static string RequiredString(JsonElement root, string propertyName)
    {
        var property = root.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(property.GetString()))
            throw new InvalidDataException($"Source manifest field {propertyName} must be a non-empty string.");
        return property.GetString()!;
    }

    private static bool IsLowerHex(string candidate, int requiredLength) =>
        candidate.Length == requiredLength &&
        candidate.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string RequireRegularFile(string requestedPath, string label)
    {
        var path = Path.GetFullPath(requestedPath);
        var file = new FileInfo(path);
        if (!file.Exists || file.LinkTarget is not null || file.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new InvalidDataException($"{label} must be a regular non-symlink file.");
        return path;
    }

    private static string ValidateEvidenceOutputPath(string requestedRoot, string requestedOutput)
    {
        var root = Path.GetFullPath(requestedRoot);
        var rootDirectory = new DirectoryInfo(root);
        if (!rootDirectory.Exists || rootDirectory.LinkTarget is not null ||
            rootDirectory.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new InvalidDataException("Evidence root must be an existing regular non-symlink directory.");
        }

        var output = Path.GetFullPath(requestedOutput);
        var relative = Path.GetRelativePath(root, output);
        if (relative == "." || Path.IsPathRooted(relative) ||
            relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Evidence output must remain beneath the release evidence root.");
        }

        RequireRegularDirectoryChain(root, Path.GetDirectoryName(output)!);
        return output;
    }

    private static void RequireRegularDirectoryChain(string root, string outputParent)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        for (var directory = new DirectoryInfo(outputParent);; directory = directory.Parent!)
        {
            if (!directory.Exists || directory.LinkTarget is not null ||
                directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidDataException("Evidence output parent chain must contain only regular directories.");
            }
            if (string.Equals(directory.FullName, root, comparison))
                return;
            if (directory.Parent is null)
                throw new InvalidDataException("Evidence output parent is outside the release evidence root.");
        }
    }

    private static async Task WriteNewRegularFileAsync(string requestedPath, string evidenceJson)
    {
        var path = Path.GetFullPath(requestedPath);
        var parent = Path.GetDirectoryName(path);
        if (parent is null || !Directory.Exists(parent))
            throw new DirectoryNotFoundException("Evidence output directory must already exist.");

        var existing = new FileInfo(path);
        if (existing.Exists || existing.LinkTarget is not null || Directory.Exists(path))
            throw new IOException("Evidence output must not already exist or be a symlink.");

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(evidenceJson);
        await writer.WriteLineAsync();
        await writer.FlushAsync();
    }

    private static int ReadMaximumDatabaseCommands()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var path = Path.Combine(directory.FullName, "frontend", "performance-budgets.json");
                if (File.Exists(path))
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(path));
                    var maximum = document.RootElement
                        .GetProperty("workflows")
                        .GetProperty("live-support-admin")
                        .GetProperty("maximumDatabaseCommands")
                        .GetInt32();
                    Assert.InRange(maximum, 1, 100);
                    return maximum;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            "frontend/performance-budgets.json is required for query-budget verification.");
    }

    private static AppDbContext CreateMeasuredContext(
        string connectionString,
        DbCommandInterceptor interceptor) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString)
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(interceptor)
                .Options);

    private static async Task<SeededSupportData> SeedAsync(
        AppDbContext db,
        int rowCount)
    {
        var staff = NewUser($"budget-staff-{Guid.NewGuid():N}");
        var student = NewUser($"budget-student-{Guid.NewGuid():N}");
        db.Users.AddRange(staff, student);
        db.LiveSupportStaffConfigs.Add(new LiveSupportStaffConfig
        {
            UserId = staff.Id,
            IsEnabled = true,
            MaxActiveConversations = 1,
            ConfiguredByUserId = staff.Id,
            Version = 1
        });

        var conversations = Enumerable.Range(0, rowCount)
            .Select(index => NewConversation(student.Id, staff.Id, index))
            .ToArray();
        db.LiveSupportConversations.AddRange(conversations);
        for (var index = 0; index < conversations.Length; index++)
        {
            AddConversationRows(db, conversations[index], student.Id, staff.Id, index);
        }
        AddTimelineRows(db, conversations[0], student.Id, rowCount);

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new SeededSupportData(staff.Id, conversations[0].Id);
    }

    private static void AddConversationRows(
        AppDbContext db,
        LiveSupportConversation conversation,
        Guid studentId,
        Guid staffId,
        int index)
    {
        db.LiveSupportAssignments.Add(NewAssignment(conversation, staffId));
        db.LiveSupportMessages.Add(NewMessage(conversation, studentId, index));
        db.LiveSupportEvents.Add(NewEvent(conversation, studentId, index));
    }

    private static LiveSupportAssignment NewAssignment(
        LiveSupportConversation conversation,
        Guid staffId) =>
        new()
        {
            ConversationId = conversation.Id,
            StaffUserId = staffId,
            StartedAt = conversation.AssignedAt!.Value,
            EndedAt = conversation.ClosedAt,
            EndReason = conversation.ClosedAt.HasValue
                ? LiveSupportAssignmentEndReason.Closed
                : null,
            AssignmentSequence = 1
        };

    private static void AddTimelineRows(
        AppDbContext db,
        LiveSupportConversation conversation,
        Guid studentId,
        int rowCount)
    {
        for (var index = 1; index < rowCount; index++)
        {
            db.LiveSupportMessages.Add(NewMessage(conversation, studentId, 10_000 + index));
            db.LiveSupportEvents.Add(NewEvent(conversation, studentId, 10_000 + index));
        }
    }

    private static LiveSupportConversation NewConversation(
        Guid studentId,
        Guid staffId,
        int index)
    {
        var createdAt = DateTime.UtcNow.AddMinutes(-index - 1);
        var isOpen = index == 0;
        DateTime? closedAt = isOpen ? null : createdAt.AddSeconds(3);
        return new LiveSupportConversation
        {
            ParticipantType = LiveSupportParticipantType.Student,
            StudentUserId = studentId,
            LinkedStudentUserId = studentId,
            CurrentOwnerUserId = isOpen ? staffId : null,
            Status = isOpen
                ? LiveSupportConversationStatus.Active
                : LiveSupportConversationStatus.Closed,
            AssignedAt = createdAt.AddSeconds(1),
            LastMessageAt = createdAt.AddSeconds(2),
            ClosedAt = closedAt,
            ClosedByUserId = isOpen ? null : staffId,
            CloseReason = isOpen ? null : "representative-history",
            Subject = $"budget-{index}",
            Version = 1,
            CreatedAt = createdAt
        };
    }

    private static LiveSupportMessage NewMessage(
        LiveSupportConversation conversation,
        Guid studentId,
        int index) =>
        new()
        {
            ConversationId = conversation.Id,
            SenderType = LiveSupportSenderType.Student,
            SenderUserId = studentId,
            ClientMessageId = $"budget-message-{index}",
            Type = LiveSupportMessageType.Text,
            Content = "representative",
            SentAt = conversation.CreatedAt.AddSeconds(index + 2)
        };

    private static LiveSupportEvent NewEvent(
        LiveSupportConversation conversation,
        Guid studentId,
        int index) =>
        new()
        {
            ConversationId = conversation.Id,
            Type = LiveSupportEventType.MessageSent,
            ActorUserId = studentId,
            OccurredAt = conversation.CreatedAt.AddSeconds(index + 2),
            Sequence = index + 1
        };

    private static User NewUser(string prefix) =>
        new()
        {
            FullName = prefix,
            PhoneNumber = $"01{Random.Shared.NextInt64(100000000, 999999999)}",
            PasswordHash = "integration"
        };

    private sealed record SeededSupportData(Guid StaffId, Guid ConversationId);

    private sealed record QueryObservation(int DatabaseCommands, int ReturnedRows);

    private sealed record QueryBudgetMeasurement(
        int RowCount,
        QueryObservation Dashboard,
        QueryObservation History,
        QueryObservation Timeline);

    private sealed record SourceBinding(
        string ReleaseId,
        string GitCommit,
        string SourceStateSha256,
        bool DirtySourceSnapshot,
        string SourceDigestAlgorithm,
        string ManifestSha256);

    private sealed record AuthorizedDatabase(string DatabaseName, string IdentitySha256);

    private sealed record DatabaseEvidence(
        string DatabaseName,
        string IdentitySha256,
        string ServerVersion,
        int ServerVersionNumber);

    private sealed record LiveSupportWorkflowEvidence(int MaximumDatabaseCommandsObserved);

    private sealed record WorkflowEvidence(
        [property: JsonPropertyName("live-support-admin")]
        LiveSupportWorkflowEvidence LiveSupportAdmin);

    private sealed record QueryBudgetEvidence(
        int SchemaVersion,
        string EvidenceType,
        SourceBinding Source,
        DatabaseEvidence Database,
        IReadOnlyList<int> RowCounts,
        IReadOnlyList<QueryBudgetMeasurement> Measurements,
        WorkflowEvidence Workflows);

    private sealed class EnabledSettings : ICachedPlatformSettingsReader
    {
        public Task<CachedPlatformSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(
                CachedPlatformSettings.Default with { LiveSupportEnabled = true });

        public void Invalidate()
        {
        }
    }

    private sealed class CommandCounterInterceptor : DbCommandInterceptor
    {
        private int _commandCount;
        public int CommandCount => Volatile.Read(ref _commandCount);
        public void Reset() => Interlocked.Exchange(ref _commandCount, 0);
        private void Count() => Interlocked.Increment(ref _commandCount);

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            Count();
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count();
            return ValueTask.FromResult(result);
        }
    }
}
