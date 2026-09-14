using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Infrastructure.Services.AdminAI.Reads;

public sealed class AdminAIStudentSnapshotRead : IAdminAIReadCapability
{
    private static readonly string[] SectionOrder =
        ["profile", "contact", "balances", "subscriptions", "activity", "assessments"];

    private static readonly IReadOnlySet<string> AllowedSections =
        new HashSet<string>(SectionOrder, StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ProfileFieldOptions =
        new HashSet<string>(["account", "personal", "academic", "school"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ContactFieldOptions =
        new HashSet<string>(["studentPhones", "guardianPhones", "location"], StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> ActivityFieldOptions =
        new HashSet<string>(
            ["watching", "lessonProgress", "devices", "commitment", "warnings", "adminNotes"],
            StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> AssessmentFieldOptions =
        new HashSet<string>(["exams", "homework", "essays"], StringComparer.Ordinal);

    private readonly IAppDbContext db;
    private readonly AdminAIStudentIdentitySnapshotReader identityReader;
    private readonly AdminAIStudentBalanceSnapshotReader balanceReader;
    private readonly AdminAIStudentSubscriptionSnapshotReader subscriptionReader;
    private readonly AdminAIStudentActivitySnapshotReader activityReader;
    private readonly AdminAIStudentAssessmentSnapshotReader assessmentReader;

    public AdminAIStudentSnapshotRead(IAppDbContext db)
    {
        this.db = db;
        identityReader = new(db);
        balanceReader = new(db);
        subscriptionReader = new(db);
        activityReader = new(db);
        assessmentReader = new(db);
    }

    public string Key => "student.snapshot";
    public Type OutputType => typeof(AdminAIStudentSnapshotOutput);

    public async Task<AdminAIReadCapabilityResult> ExecuteAsync(Guid actorId, object input, CancellationToken ct)
    {
        await using var transaction = await BeginConsistentReadAsync(ct);
        var snapshot = await ExecuteSnapshotAsync(input, ct);
        if (transaction is not null)
            await transaction.CommitAsync(ct);
        return snapshot;
    }

    private async Task<AdminAIReadCapabilityResult> ExecuteSnapshotAsync(object input, CancellationToken ct)
    {
        var selection = ParseSelection(input);
        var student = await FindStudentAsync(selection.Request.StudentId, ct);
        if (student is null)
            return BuildUnavailableResult(selection);

        var snapshotSections = await LoadRequestedSectionsAsync(selection, ct);
        var snapshot = BuildOutput(student, selection, snapshotSections);
        return new(
            snapshot,
            1,
            !snapshotSections.IsTruncated,
            snapshotSections.IsTruncated,
            selection.Request.DataAsOf,
            [$"admin.student.details:{student.Id:D}"]);
    }

    private async Task<IDbContextTransaction?> BeginConsistentReadAsync(CancellationToken ct)
    {
        if (db is not DbContext context ||
            !context.Database.IsRelational() ||
            context.Database.CurrentTransaction is not null)
            return null;
        return await db.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
    }

    private static AdminAIStudentSnapshotSelection ParseSelection(object input)
    {
        var studentId = AdminAIReadArguments.RequireGuid(input, "studentId");
        var recentLimit = AdminAIReadArguments.RequireInt32(input, "recentLimit", 0, 10);
        var selectionInput = AdminAIReadArguments.RequireObject(input, "selection");
        var sections = AdminAIReadArguments.RequireObjectKeys(
            selectionInput,
            AllowedSections,
            1,
            SectionOrder.Length);

        var profileFields = ReadFields(selectionInput, sections, "profile", ProfileFieldOptions);
        var contactFields = ReadFields(selectionInput, sections, "contact", ContactFieldOptions);
        var activityFields = ReadFields(selectionInput, sections, "activity", ActivityFieldOptions);
        var assessmentFields = ReadFields(selectionInput, sections, "assessments", AssessmentFieldOptions);
        var balanceTeacherId = ReadContextTeacherId(selectionInput, sections, "balances");
        var subscriptionTeacherId = ReadContextTeacherId(selectionInput, sections, "subscriptions");

        var request = new AdminAIStudentSnapshotRequest(
            studentId,
            recentLimit,
            balanceTeacherId,
            subscriptionTeacherId,
            DateTime.UtcNow,
            profileFields,
            contactFields,
            activityFields,
            assessmentFields);
        var includedSections = SectionOrder.Where(sections.Contains).ToArray();
        return new(request, sections, includedSections);
    }

    private static IReadOnlySet<string> ReadFields(
        JsonElement selection,
        IReadOnlySet<string> sections,
        string sectionName,
        IReadOnlySet<string> allowedFields)
    {
        if (!sections.Contains(sectionName))
            return new HashSet<string>(StringComparer.Ordinal);
        var section = AdminAIReadArguments.GetSelectedObject(selection, sectionName);
        return AdminAIReadArguments.RequireStringSet(
            section,
            "fields",
            allowedFields,
            1,
            allowedFields.Count);
    }

    private static Guid? ReadContextTeacherId(
        JsonElement selection,
        IReadOnlySet<string> sections,
        string sectionName)
    {
        if (!sections.Contains(sectionName))
            return null;
        var section = AdminAIReadArguments.GetSelectedObject(selection, sectionName);
        return AdminAIReadArguments.OptionalGuid(section, "teacherId");
    }

    private async Task<AdminAIStudentSnapshotSubject?> FindStudentAsync(Guid studentId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(user =>
                user.Id == studentId &&
                !user.IsDeleted &&
                user.StudentProfile != null &&
                user.UserRoles.Any(role => role.Role.Type == RoleType.Student))
            .Select(user => new AdminAIStudentSnapshotSubject(user.Id, user.FullName, user.PhoneNumber))
            .SingleOrDefaultAsync(ct);

    private static AdminAIReadCapabilityResult BuildUnavailableResult(AdminAIStudentSnapshotSelection selection)
    {
        var unavailableSnapshot = new AdminAIStudentSnapshotOutput(
            false,
            null,
            null,
            null,
            selection.IncludedSections,
            null,
            null,
            null,
            null,
            null,
            null,
            selection.Request.DataAsOf);
        return new(unavailableSnapshot, 0, true, false, selection.Request.DataAsOf, ["admin.students"]);
    }

    private async Task<AdminAIStudentSnapshotSections> LoadRequestedSectionsAsync(
        AdminAIStudentSnapshotSelection selection,
        CancellationToken ct)
    {
        var profile = selection.Sections.Contains("profile")
            ? await identityReader.LoadProfileAsync(selection.Request, ct)
            : null;
        var contact = selection.Sections.Contains("contact")
            ? await identityReader.LoadContactAsync(selection.Request, ct)
            : null;
        var balances = selection.Sections.Contains("balances")
            ? await balanceReader.LoadAsync(selection.Request, ct)
            : null;
        var subscriptions = selection.Sections.Contains("subscriptions")
            ? await subscriptionReader.LoadAsync(selection.Request, ct)
            : null;
        var activity = selection.Sections.Contains("activity")
            ? await activityReader.LoadAsync(selection.Request, ct)
            : null;
        var assessments = selection.Sections.Contains("assessments")
            ? await assessmentReader.LoadAsync(selection.Request, ct)
            : null;

        var isTruncated = balances?.IsTruncated == true ||
                          subscriptions?.IsTruncated == true ||
                          activity?.IsTruncated == true ||
                          assessments?.IsTruncated == true;
        return new(
            profile,
            contact,
            balances?.Payload,
            subscriptions?.Payload,
            activity?.Payload,
            assessments?.Payload,
            isTruncated);
    }

    private static AdminAIStudentSnapshotOutput BuildOutput(
        AdminAIStudentSnapshotSubject student,
        AdminAIStudentSnapshotSelection selection,
        AdminAIStudentSnapshotSections sections) =>
        new(
            true,
            student.Id,
            AdminAIReadArguments.SafeText(student.DisplayName, 120),
            AdminAIReadArguments.MaskPhone(student.PhoneNumber),
            selection.IncludedSections,
            sections.Profile,
            sections.Contact,
            sections.Balances,
            sections.Subscriptions,
            sections.Activity,
            sections.Assessments,
            selection.Request.DataAsOf);
}
