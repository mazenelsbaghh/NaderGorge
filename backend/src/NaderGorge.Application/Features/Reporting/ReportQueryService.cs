using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Reporting;

public interface IReportQueryService
{
    Task<ReportResultDto> ExecuteAsync(ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct);
    Task<ReportResultDto> ExecuteForExportAsync(ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct);
    Task ValidateAsync(ExecuteReportRequest request, bool isTeacher, CancellationToken ct);
    Task<bool> CanAccessTeacherReportsAsync(Guid actorUserId, CancellationToken ct);
    Task<bool> CanAccessTeacherFinanceAsync(Guid actorUserId, CancellationToken ct);
    Task<ReportFilterOptionsDto> GetFilterOptionsAsync(Guid actorUserId, bool isTeacher, CancellationToken ct);
}

public sealed class ReportQueryService : IReportQueryService
{
    private const int SourceRowLimit = 10_000;
    private static readonly TimeZoneInfo CairoTimeZone = ResolveCairoTimeZone();
    private readonly IAppDbContext _db;

    public ReportQueryService(IAppDbContext db) => _db = db;

    public Task ValidateAsync(ExecuteReportRequest request, bool isTeacher, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var domain = ReportCatalog.Find(request.Domain, isTeacher)
            ?? throw new ArgumentException("مجال التقرير غير متاح لهذا الحساب.");
        var fields = domain.Fields.ToDictionary(field => field.Key, StringComparer.OrdinalIgnoreCase);

        if (!ReportFilterRules.HasValidStructure(request.FilterGroup) || ReportFilterRules.CountConditions(request.FilterGroup) > 30)
            throw new ArgumentException("بنية مجموعات الفلاتر غير صالحة أو تتجاوز الحدود المسموحة.");

        foreach (var group in FlattenGroups(request.FilterGroup))
            if (group.Logic is not ("and" or "or"))
                throw new ArgumentException("منطق مجموعة الفلاتر يجب أن يكون and أو or.");

        foreach (var filter in Flatten(request.FilterGroup))
        {
            if (!fields.TryGetValue(filter.Field, out var field) || !field.Operators.Contains(filter.Operator, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException($"الفلتر '{filter.Field}' أو العامل '{filter.Operator}' غير صالح لهذا التقرير.");
            if (filter.Values == null)
                throw new ArgumentException($"قيم الفلتر '{filter.Field}' مطلوبة.");
            if (filter.Values.Count > 100)
                throw new ArgumentException("لا يمكن أن يحتوي الفلتر الواحد على أكثر من 100 قيمة.");
            if (filter.Operator == "between" && filter.Values.Count != 2)
                throw new ArgumentException("عامل between يحتاج قيمتين بالضبط.");
            if (!ValuesMatchType(field.Type, filter.Operator, filter.Values))
                throw new ArgumentException($"قيم الفلتر '{filter.Field}' لا تطابق نوع الحقل {field.Type}.");
        }

        var requestedColumns = request.Columns is { Count: > 0 } ? request.Columns : domain.DefaultColumns;
        if (requestedColumns.Any(column => !fields.ContainsKey(column)))
            throw new ArgumentException("أحد الأعمدة المطلوبة غير صالح لهذا التقرير.");
        if (request.Sort != null && !fields.ContainsKey(request.Sort.Field))
            throw new ArgumentException("عمود الترتيب غير صالح لهذا التقرير.");
        if (request.Sort != null && request.Sort.Direction is not ("asc" or "desc"))
            throw new ArgumentException("اتجاه الترتيب يجب أن يكون asc أو desc.");

        return Task.CompletedTask;
    }

    public async Task<ReportFilterOptionsDto> GetFilterOptionsAsync(Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        var teacherId = await GetReportScopeTeacherIdAsync(actorUserId, isTeacher, ct);
        var (teacherNames, packageNames, videoNames) = await GetPickableValuesAsync(teacherId, ct);
        return new ReportFilterOptionsDto(CreateFilterOptions(teacherNames, packageNames, videoNames));
    }

    private async Task<Guid?> GetReportScopeTeacherIdAsync(Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        if (!isTeacher) return null;
        var scope = await ResolveTeacherScopeAsync(actorUserId, ct);
        return scope?.TeacherId ?? throw new UnauthorizedAccessException("لا يوجد نطاق مدرس صالح لهذا الحساب.");
    }

    private async Task<(List<string> Teachers, List<string> Packages, List<string> Videos)> GetPickableValuesAsync(Guid? teacherId, CancellationToken ct)
    {
        var teachers = await PickTeacherNamesAsync(teacherId, ct);
        var packages = await PickPackageNamesAsync(teacherId, ct);
        var videos = await PickVideoNamesAsync(teacherId, ct);
        return (teachers, packages, videos);
    }

    private Task<List<string>> PickTeacherNamesAsync(Guid? teacherId, CancellationToken ct) =>
        _db.TeacherProfiles.AsNoTracking().Where(teacher => !teacherId.HasValue || teacher.Id == teacherId.Value)
            .Select(teacher => teacher.User.FullName).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct().OrderBy(name => name).Take(250).ToListAsync(ct);

    private Task<List<string>> PickPackageNamesAsync(Guid? teacherId, CancellationToken ct) =>
        _db.Packages.AsNoTracking().Where(packageItem => !teacherId.HasValue || packageItem.TeacherId == teacherId.Value)
            .Select(packageItem => packageItem.Name).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct().OrderBy(name => name).Take(250).ToListAsync(ct);

    private Task<List<string>> PickVideoNamesAsync(Guid? teacherId, CancellationToken ct) =>
        _db.LessonVideos.AsNoTracking().Where(video => !teacherId.HasValue || video.Lesson.ContentSection.Term.Package.TeacherId == teacherId.Value)
            .Select(video => video.Title).Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct().OrderBy(title => title).Take(500).ToListAsync(ct);

    private static List<ReportFilterOptionDto> CreateFilterOptions(IEnumerable<string> teacherNames, IEnumerable<string> packageNames, IEnumerable<string> videoNames)
    {
        var contentNames = packageNames.Concat(videoNames).Distinct().OrderBy(name => name).Take(500);
        var options = teacherNames.Select(name => new ReportFilterOptionDto("teacherName", name, name)).ToList();
        foreach (var field in new[] { "contentName", "name" })
            options.AddRange(contentNames.Select(name => new ReportFilterOptionDto(field, name, name)));
        options.AddRange(packageNames.Select(name => new ReportFilterOptionDto("packageName", name, name)));
        options.AddRange(videoNames.Select(name => new ReportFilterOptionDto("videoName", name, name)));
        return options;
    }

    public async Task<ReportResultDto> ExecuteAsync(ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        await ValidateAsync(request, isTeacher, ct);
        return await ExecuteValidatedAsync(request, actorUserId, isTeacher, ct);
    }

    public async Task<ReportResultDto> ExecuteForExportAsync(ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        await ValidateAsync(request, isTeacher, ct);
        var domain = ReportCatalog.Find(request.Domain, isTeacher)!;
        var columns = (request.Columns is { Count: > 0 } ? request.Columns : domain.DefaultColumns).ToList();
        foreach (var key in ReportCatalog.StudentIdentityColumnKeys.Where(key => domain.Fields.Any(field => field.Key == key)))
            if (!columns.Contains(key, StringComparer.OrdinalIgnoreCase)) columns.Add(key);
        return await ExecuteValidatedAsync(request with { Page = 1, PageSize = 5_000, Columns = columns }, actorUserId, isTeacher, ct);
    }

    private async Task<ReportResultDto> ExecuteValidatedAsync(ExecuteReportRequest request, Guid actorUserId, bool isTeacher, CancellationToken ct)
    {
        var teacherScope = isTeacher ? await ResolveTeacherScopeAsync(actorUserId, ct) : null;
        var teacherId = teacherScope?.TeacherId;
        if (isTeacher && teacherScope == null)
            throw new UnauthorizedAccessException("لا يوجد نطاق مدرس صالح لهذا الحساب.");
        if (isTeacher && request.Domain == ReportDomains.TeachersFinance && !teacherScope!.CanViewFinance)
            throw new UnauthorizedAccessException("صلاحية التقارير المالية غير متاحة لهذا الحساب.");

        var teacherStudentIds = teacherId.HasValue
            ? await GetTeacherStudentIdsAsync(teacherId.Value, ct)
            : null;
        var rows = await LoadRowsAsync(request.Domain, teacherId, teacherStudentIds, ct);
        await EnrichStudentIdentityAsync(rows, ct);
        var filtered = rows.Where(row => MatchesGroup(row, request.FilterGroup)).ToList();
        var domain = ReportCatalog.Find(request.Domain, isTeacher)!;
        var sort = request.Sort ?? new ReportSort(domain.DefaultColumns[0]);
        filtered.Sort((left, right) => Compare(left.GetValueOrDefault(sort.Field), right.GetValueOrDefault(sort.Field)) * (sort.Direction == "desc" ? -1 : 1));

        var totalCount = filtered.Count;
        var pageRows = filtered.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToList();
        var selectedKeys = request.Columns is { Count: > 0 } ? request.Columns : domain.DefaultColumns;
        var selectedFields = selectedKeys.Select(key => domain.Fields.First(field => field.Key.Equals(key, StringComparison.OrdinalIgnoreCase))).ToArray();
        var projectedRows = pageRows
            .Select(row => (IReadOnlyDictionary<string, object?>)selectedKeys.ToDictionary(key => key, key => row.GetValueOrDefault(key)))
            .ToArray();

        return new ReportResultDto(
            request.Domain,
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CairoTimeZone),
            BuildSummary(filtered),
            BuildChart(filtered),
            selectedFields.Select(field => new ReportColumnDto(field.Key, field.Label, field.Type)).ToArray(),
            projectedRows,
            request.Page,
            request.PageSize,
            totalCount,
            Flatten(request.FilterGroup).Select(Describe).ToArray(),
            rows.Count >= SourceRowLimit,
            rows.Count >= SourceRowLimit ? $"تم تطبيق حد أمان قدره {SourceRowLimit:N0} سجل. استخدم فلاتر أضيق للحصول على إجماليات كاملة." : null);
    }

    public async Task<bool> CanAccessTeacherReportsAsync(Guid actorUserId, CancellationToken ct) =>
        await ResolveTeacherScopeAsync(actorUserId, ct) != null;

    public async Task<bool> CanAccessTeacherFinanceAsync(Guid actorUserId, CancellationToken ct) =>
        (await ResolveTeacherScopeAsync(actorUserId, ct))?.CanViewFinance == true;

    private async Task<TeacherReportScope?> ResolveTeacherScopeAsync(Guid userId, CancellationToken ct)
    {
        var ownTeacherId = await _db.TeacherProfiles.AsNoTracking()
            .Where(teacher => teacher.UserId == userId)
            .Select(teacher => (Guid?)teacher.Id)
            .FirstOrDefaultAsync(ct);
        if (ownTeacherId.HasValue) return new TeacherReportScope(ownTeacherId.Value, true);

        var memberships = await _db.TeacherStaffMembers.AsNoTracking()
            .Where(member => member.UserId == userId && member.IsActive && member.User.IsActive)
            .OrderBy(member => member.CreatedAt)
            .Select(member => new { member.TeacherId, member.PermissionKeys })
            .ToListAsync(ct);
        var membership = memberships.FirstOrDefault(member => HasPermission(member.PermissionKeys, "reports"));
        return membership == null ? null : new TeacherReportScope(membership.TeacherId, HasPermission(membership.PermissionKeys, "finance"));
    }

    private sealed record TeacherReportScope(Guid TeacherId, bool CanViewFinance);

    private static bool HasPermission(string permissionKeys, string permission) =>
        permissionKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(permission, StringComparer.OrdinalIgnoreCase);

    private async Task<HashSet<Guid>> GetTeacherStudentIdsAsync(Guid teacherId, CancellationToken ct)
    {
        var fromGrants = await TeacherGrants(teacherId)
            .Select(grant => grant.UserId)
            .Distinct()
            .ToListAsync(ct);
        var fromFinance = await _db.TeacherFinancialAllocations.AsNoTracking()
            .Where(allocation => allocation.TeacherId == teacherId && allocation.TeacherFinancialEvent.StudentId.HasValue)
            .Select(allocation => allocation.TeacherFinancialEvent.StudentId!.Value)
            .Distinct()
            .ToListAsync(ct);
        fromGrants.AddRange(fromFinance);
        return fromGrants.ToHashSet();
    }

    private IQueryable<Domain.Entities.StudentAccessGrant> TeacherGrants(Guid teacherId) =>
        _db.StudentAccessGrants.AsNoTracking().Where(grant =>
            (grant.PackageId.HasValue && _db.Packages.Any(package => package.Id == grant.PackageId && package.TeacherId == teacherId)) ||
            (grant.TermId.HasValue && _db.Terms.Any(term => term.Id == grant.TermId && term.Package.TeacherId == teacherId)) ||
            (grant.ContentSectionId.HasValue && _db.ContentSections.Any(section => section.Id == grant.ContentSectionId && section.Term.Package.TeacherId == teacherId)) ||
            (grant.LessonId.HasValue && _db.Lessons.Any(lesson => lesson.Id == grant.LessonId && lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.LessonVideoId.HasValue && _db.LessonVideos.Any(video => video.Id == grant.LessonVideoId && video.Lesson.ContentSection.Term.Package.TeacherId == teacherId)) ||
            (grant.ExamId.HasValue && _db.Exams.Any(exam => exam.Id == grant.ExamId && exam.CreatedByTeacherId == teacherId)));

    private Task<List<Dictionary<string, object?>>> LoadRowsAsync(string domain, Guid? teacherId, HashSet<Guid>? teacherStudentIds, CancellationToken ct) =>
        domain switch
        {
            ReportDomains.StudentJourney => LoadStudentJourneyAsync(teacherId, ct),
            ReportDomains.Students => LoadStudentsAsync(teacherStudentIds, ct),
            ReportDomains.Purchases => LoadPurchasesAsync(teacherId, ct),
            ReportDomains.Codes => LoadCodesAsync(teacherId, ct),
            ReportDomains.BalanceRecharge => LoadBalanceRechargeAsync(teacherId, teacherStudentIds, ct),
            ReportDomains.Content => LoadContentAsync(teacherId, ct),
            ReportDomains.Engagement => LoadEngagementAsync(teacherId, ct),
            ReportDomains.Attendance => LoadAttendanceAsync(teacherId, ct),
            ReportDomains.Assessments => LoadAssessmentsAsync(teacherId, ct),
            ReportDomains.TeachersFinance => LoadTeacherFinanceAsync(teacherId, ct),
            ReportDomains.Staff when teacherId == null => LoadStaffAsync(ct),
            ReportDomains.Support when teacherId == null => LoadSupportAsync(ct),
            ReportDomains.CommentsCommunity => LoadCommentsAsync(teacherId, ct),
            ReportDomains.ParentTracking => LoadParentTrackingAsync(teacherStudentIds, ct),
            ReportDomains.OperationsSecurity when teacherId == null => LoadOperationsAsync(ct),
            _ => throw new UnauthorizedAccessException("هذا التقرير غير متاح ضمن نطاق المدرس.")
        };

    private async Task<List<Dictionary<string, object?>>> LoadStudentsAsync(HashSet<Guid>? scope, CancellationToken ct)
    {
        var query = _db.StudentProfiles.AsNoTracking().AsQueryable();
        if (scope != null) query = query.Where(profile => scope.Contains(profile.UserId));
        var data = await query.OrderByDescending(profile => profile.CreatedAt).Take(SourceRowLimit)
            .Select(profile => new { profile.UserId, profile.User.FullName, profile.User.PhoneNumber, profile.SecondaryPhone, profile.StudentCode, Grade = profile.GradeLevel.ToString(), Stage = profile.EducationStage.ToString(), profile.Governorate, profile.District, profile.Address, profile.SchoolName, profile.ParentPhone, profile.MotherPhone, profile.Nationality, profile.User.IsActive, RegisteredAt = profile.User.CreatedAt })
            .ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.UserId.ToString()), ("studentName", item.FullName), ("phone", item.PhoneNumber), ("secondaryPhone", item.SecondaryPhone), ("studentCode", item.StudentCode), ("grade", item.Grade), ("stage", item.Stage), ("governorate", item.Governorate), ("district", item.District), ("address", item.Address), ("schoolName", item.SchoolName), ("parentPhone", item.ParentPhone), ("motherPhone", item.MotherPhone), ("nationality", item.Nationality), ("isActive", item.IsActive), ("registeredAt", item.RegisteredAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadPurchasesAsync(Guid? teacherId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = _db.StudentAccessGrants.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) query = TeacherGrants(teacherId.Value);
        var data = await query.OrderByDescending(grant => grant.GrantedAt).Take(SourceRowLimit / 2)
            .Select(grant => new
            {
                grant.UserId,
                grant.User.FullName,
                Type = grant.GrantType.ToString(),
                grant.PackageId,
                PackageName = grant.PackageId.HasValue ? _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Name).FirstOrDefault() : null,
                TeacherName = grant.PackageId.HasValue ? _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Teacher.User.FullName).FirstOrDefault() : null,
                ContentName = grant.PackageId.HasValue ? _db.Packages.Where(p => p.Id == grant.PackageId).Select(p => p.Name).FirstOrDefault() : grant.TermId.HasValue ? _db.Terms.Where(t => t.Id == grant.TermId).Select(t => t.Title).FirstOrDefault() : grant.ContentSectionId.HasValue ? _db.ContentSections.Where(s => s.Id == grant.ContentSectionId).Select(s => s.Title).FirstOrDefault() : grant.LessonId.HasValue ? _db.Lessons.Where(l => l.Id == grant.LessonId).Select(l => l.Title).FirstOrDefault() : grant.LessonVideoId.HasValue ? _db.LessonVideos.Where(v => v.Id == grant.LessonVideoId).Select(v => v.Title).FirstOrDefault() : grant.ExamId.HasValue ? _db.Exams.Where(e => e.Id == grant.ExamId).Select(e => e.Title).FirstOrDefault() : "محتوى",
                IsGift = grant.GiftRecipientId.HasValue,
                IsCode = grant.AccessCodeId.HasValue,
                IsBalance = _db.BalanceTransactions.Any(transaction => transaction.ReferenceId == grant.Id && transaction.TransactionType == "ContentPurchase"),
                grant.IsActive,
                IsExpired = !grant.IsActive || grant.CancelledAt.HasValue || (grant.ExpiresAt.HasValue && grant.ExpiresAt <= now),
                grant.GrantedAt,
                grant.ExpiresAt
            })
            .ToListAsync(ct);
        var rows = data.Select(grant => Row(
            ("_studentId", grant.UserId.ToString()),
            ("studentName", grant.FullName),
            ("packageId", grant.PackageId?.ToString()),
            ("packageName", grant.PackageName),
            ("teacherName", grant.TeacherName),
            ("grantType", grant.Type),
            ("contentName", grant.ContentName),
            ("purchaseStatus", PurchaseStatus(grant.IsExpired, grant.IsGift, grant.IsCode, grant.IsBalance)),
            ("source", PurchaseSource(grant.IsGift, grant.IsCode, grant.IsBalance)),
            ("isActive", grant.IsActive && !grant.IsExpired),
            ("grantedAt", grant.GrantedAt),
            ("expiresAt", grant.ExpiresAt))).ToList();

        rows.AddRange(await LoadNotPurchasedRowsAsync(teacherId, SourceRowLimit / 2, now, ct));
        return rows;
    }

    private async Task<List<Dictionary<string, object?>>> LoadNotPurchasedRowsAsync(Guid? teacherId, int limit, DateTime now, CancellationToken ct)
    {
        var packages = _db.Packages.AsNoTracking().Where(package => package.IsActive);
        if (teacherId.HasValue) packages = packages.Where(package => package.TeacherId == teacherId.Value);
        var teacherStudentIds = teacherId.HasValue ? await GetTeacherStudentIdsAsync(teacherId.Value, ct) : null;
        var candidates =
            from student in _db.StudentProfiles.AsNoTracking()
            from package in packages
            where student.User.IsActive && !student.User.IsDeleted
            where teacherStudentIds == null || teacherStudentIds.Contains(student.UserId)
            where _db.StudentFacingAcademicScopes.Any(scope =>
                scope.OwnerType == Domain.Enums.StudentFacingScopeOwnerType.Package && scope.OwnerId == package.Id &&
                (scope.ScopeLevel == Domain.Enums.AcademicScopeLevel.PlatformWide ||
                 (scope.ScopeLevel == Domain.Enums.AcademicScopeLevel.StageWide && scope.EducationStage == student.EducationStage) ||
                 (scope.ScopeLevel == Domain.Enums.AcademicScopeLevel.GradeAllSubjects && scope.EducationStage == student.EducationStage && scope.GradeLevel == student.GradeLevel) ||
                 (scope.ScopeLevel == Domain.Enums.AcademicScopeLevel.Exact && scope.EducationStage == student.EducationStage && scope.GradeLevel == student.GradeLevel && scope.SubjectId == package.SubjectId &&
                  _db.AcademicSubjectEligibilities.Any(eligibility => eligibility.IsActive && eligibility.EducationStage == student.EducationStage && eligibility.GradeLevel == student.GradeLevel && eligibility.SubjectId == scope.SubjectId))))
            where !_db.StudentAccessGrants.Any(grant => grant.UserId == student.UserId && grant.PackageId == package.Id && grant.IsActive && !grant.CancelledAt.HasValue && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now))
            orderby student.User.FullName, package.Name
            select new { StudentId = student.UserId, StudentName = student.User.FullName, PackageId = package.Id, PackageName = package.Name, TeacherName = package.Teacher.User.FullName };
        var missing = await candidates.Take(limit).ToListAsync(ct);
        return missing.Select(candidate => Row(("_studentId", candidate.StudentId.ToString()), ("studentName", candidate.StudentName), ("packageId", candidate.PackageId.ToString()), ("packageName", candidate.PackageName), ("teacherName", candidate.TeacherName), ("grantType", null), ("contentName", candidate.PackageName), ("purchaseStatus", "notPurchased"), ("source", null), ("isActive", false), ("grantedAt", null), ("expiresAt", null))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadStudentJourneyAsync(Guid? teacherId, CancellationToken ct)
    {
        var studentPackages = await LoadJourneyStudentPackagesAsync(teacherId, ct);
        if (studentPackages.Count == 0) return [];
        var studentIds = studentPackages.Select(pair => pair.StudentId).Distinct().ToArray();
        var packageIds = studentPackages.Select(pair => pair.PackageId).Distinct().ToArray();
        var watches = await LoadJourneyWatchesAsync(studentIds, packageIds, ct);
        var exams = await LoadJourneyExamsAsync(studentIds, packageIds, ct);
        var homeworks = await LoadJourneyHomeworksAsync(studentIds, packageIds, ct);
        return BuildJourneyRows(studentPackages, watches, exams, homeworks);
    }

    private async Task<List<JourneyStudentPackage>> LoadJourneyStudentPackagesAsync(Guid? teacherId, CancellationToken ct)
    {
        var purchaseRows = await LoadPurchasesAsync(teacherId, ct);
        return purchaseRows
            .Where(row => Guid.TryParse(Convert.ToString(row.GetValueOrDefault("_studentId"), CultureInfo.InvariantCulture), out _)
                && Guid.TryParse(Convert.ToString(row.GetValueOrDefault("packageId"), CultureInfo.InvariantCulture), out _))
            .GroupBy(row => $"{row["_studentId"]}:{row["packageId"]}", StringComparer.Ordinal)
            .Select(group => group.OrderBy(row => Equals(row.GetValueOrDefault("purchaseStatus"), "notPurchased") ? 1 : 0).First())
            .Take(SourceRowLimit)
            .Select(row => new JourneyStudentPackage(
                Guid.Parse((string)row["_studentId"]!), Guid.Parse((string)row["packageId"]!),
                row.GetValueOrDefault("studentName"), row.GetValueOrDefault("packageName"),
                row.GetValueOrDefault("teacherName"), row.GetValueOrDefault("purchaseStatus")))
            .ToList();
    }

    private async Task<Dictionary<(Guid StudentId, Guid PackageId), DateTime>> LoadJourneyWatchesAsync(Guid[] studentIds, Guid[] packageIds, CancellationToken ct)
    {
        var watchRows = await _db.VideoWatchEvents.AsNoTracking()
            .Where(watch => studentIds.Contains(watch.UserId) && packageIds.Contains(watch.LessonVideo.Lesson.ContentSection.Term.PackageId))
            .Select(watch => new { watch.UserId, PackageId = watch.LessonVideo.Lesson.ContentSection.Term.PackageId, LastActivityAt = watch.UpdatedAt ?? watch.CreatedAt })
            .ToListAsync(ct);
        return watchRows.GroupBy(watch => (watch.UserId, watch.PackageId))
            .ToDictionary(group => group.Key, group => group.Max(watch => watch.LastActivityAt));
    }

    private async Task<JourneyExamActivity> LoadJourneyExamsAsync(Guid[] studentIds, Guid[] packageIds, CancellationToken ct)
    {
        var examRows = await _db.Exams.AsNoTracking()
            .Where(exam =>
                (exam.LessonVideoId.HasValue && packageIds.Contains(exam.LessonVideo!.Lesson.ContentSection.Term.PackageId)) ||
                (!exam.LessonVideoId.HasValue && _db.Lessons.Any(lesson => lesson.ExamId == exam.Id && packageIds.Contains(lesson.ContentSection.Term.PackageId))))
            .Select(exam => new
            {
                exam.Id,
                PackageId = exam.LessonVideoId.HasValue
                    ? exam.LessonVideo!.Lesson.ContentSection.Term.PackageId
                    : _db.Lessons.Where(lesson => lesson.ExamId == exam.Id).Select(lesson => lesson.ContentSection.Term.PackageId).FirstOrDefault()
            }).ToListAsync(ct);
        var examPackageById = examRows.ToDictionary(exam => exam.Id, exam => exam.PackageId);
        var examIds = examPackageById.Keys.ToArray();
        var attemptRows = await _db.StudentExamAttempts.AsNoTracking()
            .Where(attempt => studentIds.Contains(attempt.UserId) && examIds.Contains(attempt.ExamId))
            .Select(attempt => new { attempt.UserId, attempt.ExamId, attempt.IsPassed })
            .ToListAsync(ct);
        var attempts = attemptRows.GroupBy(attempt => (attempt.UserId, PackageId: examPackageById[attempt.ExamId]))
            .ToDictionary(group => group.Key, group => group.Any(attempt => attempt.IsPassed));
        return new JourneyExamActivity(examRows.Select(exam => exam.PackageId).ToHashSet(), attempts);
    }

    private async Task<JourneyHomeworkActivity> LoadJourneyHomeworksAsync(Guid[] studentIds, Guid[] packageIds, CancellationToken ct)
    {
        var homeworkRows = await _db.Homeworks.AsNoTracking()
            .Where(homework => _db.Lessons.Any(lesson => lesson.Id == homework.LessonId && packageIds.Contains(lesson.ContentSection.Term.PackageId)))
            .Select(homework => new
            {
                homework.Id,
                PackageId = _db.Lessons.Where(lesson => lesson.Id == homework.LessonId).Select(lesson => lesson.ContentSection.Term.PackageId).FirstOrDefault()
            }).ToListAsync(ct);
        var homeworkPackageById = homeworkRows.ToDictionary(homework => homework.Id, homework => homework.PackageId);
        var homeworkIds = homeworkPackageById.Keys.ToArray();
        var submissionRows = await _db.HomeworkSubmissions.AsNoTracking()
            .Where(submission => studentIds.Contains(submission.StudentId) && homeworkIds.Contains(submission.HomeworkId))
            .Select(submission => new { submission.StudentId, submission.HomeworkId })
            .ToListAsync(ct);
        return new JourneyHomeworkActivity(
            homeworkRows.Select(homework => homework.PackageId).ToHashSet(),
            submissionRows.Select(submission => (submission.StudentId, PackageId: homeworkPackageById[submission.HomeworkId])).ToHashSet());
    }

    private static List<Dictionary<string, object?>> BuildJourneyRows(
        IEnumerable<JourneyStudentPackage> studentPackages,
        IReadOnlyDictionary<(Guid StudentId, Guid PackageId), DateTime> watches,
        JourneyExamActivity exams,
        JourneyHomeworkActivity homeworks) =>
        studentPackages.Select(pair =>
        {
            var key = (pair.StudentId, pair.PackageId);
            var hasWatch = watches.TryGetValue(key, out var lastActivityAt);
            var examStatus = !exams.PackageIds.Contains(pair.PackageId)
                ? "noExam"
                : !exams.PassedByStudentPackage.TryGetValue(key, out var passed)
                    ? "notAttempted"
                    : passed ? "passed" : "failed";
            var homeworkStatus = !homeworks.PackageIds.Contains(pair.PackageId)
                ? "noHomework"
                : homeworks.SubmittedStudentPackages.Contains(key) ? "submitted" : "notSubmitted";
            return Row(
                ("_studentId", pair.StudentId.ToString()), ("studentName", pair.StudentName), ("packageName", pair.PackageName),
                ("teacherName", pair.TeacherName), ("purchaseStatus", pair.PurchaseStatus),
                ("attendanceStatus", hasWatch ? "present" : "absent"),
                ("videoStatus", hasWatch ? "watched" : "notWatched"),
                ("examStatus", examStatus),
                ("homeworkStatus", homeworkStatus),
                ("lastActivityAt", hasWatch ? lastActivityAt : (DateTime?)null));
        }).ToList();

    private sealed record JourneyStudentPackage(Guid StudentId, Guid PackageId, object? StudentName, object? PackageName, object? TeacherName, object? PurchaseStatus);
    private sealed record JourneyExamActivity(HashSet<Guid> PackageIds, Dictionary<(Guid StudentId, Guid PackageId), bool> PassedByStudentPackage);
    private sealed record JourneyHomeworkActivity(HashSet<Guid> PackageIds, HashSet<(Guid StudentId, Guid PackageId)> SubmittedStudentPackages);

    private static string PurchaseStatus(bool expired, bool gift, bool code, bool balance) =>
        expired ? "expired" : gift ? "gift" : code ? "code" : balance ? "balance" : "purchased";

    private static string PurchaseSource(bool gift, bool code, bool balance) =>
        gift ? "gift" : code ? "code" : balance ? "balance" : "direct";

    private async Task<List<Dictionary<string, object?>>> LoadCodesAsync(Guid? teacherId, CancellationToken ct)
    {
        var query = _db.AccessCodes.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) query = query.Where(code => code.CodeGroup.TeacherId == teacherId);
        var data = await query.OrderByDescending(code => code.CreatedAt).Take(SourceRowLimit)
            .Select(code => new { code.ConsumedByUserId, GroupName = code.CodeGroup.Name, CodeType = code.CodeGroup.CodeType.ToString(), code.IsConsumed, StudentName = code.ConsumedByUser != null ? code.ConsumedByUser.FullName : null, code.ConsumedAt, ExpiresAt = code.ExpiresAt ?? code.CodeGroup.ExpiresAt })
            .ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.ConsumedByUserId?.ToString()), ("groupName", item.GroupName), ("codeType", item.CodeType), ("isConsumed", item.IsConsumed), ("studentName", item.StudentName), ("consumedAt", item.ConsumedAt), ("expiresAt", item.ExpiresAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadBalanceRechargeAsync(Guid? teacherId, HashSet<Guid>? students, CancellationToken ct)
    {
        var rechargeQuery = _db.RechargeRequests.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) rechargeQuery = rechargeQuery.Where(request => request.TeacherId == teacherId);
        var recharges = await rechargeQuery.OrderByDescending(request => request.CreatedAt).Take(SourceRowLimit / 2)
            .Select(request => new { request.UserId, request.User.FullName, request.Amount, Status = request.Status.ToString(), request.CreatedAt }).ToListAsync(ct);
        var transactionQuery = _db.BalanceTransactions.AsNoTracking().AsQueryable();
        if (teacherId.HasValue)
        {
            var scopedGrantIds = TeacherGrants(teacherId.Value).Select(grant => grant.Id);
            transactionQuery = transactionQuery.Where(transaction => transaction.ReferenceId.HasValue && scopedGrantIds.Contains(transaction.ReferenceId.Value));
        }
        else if (students != null) transactionQuery = transactionQuery.Where(transaction => students.Contains(transaction.StudentBalance.UserId));
        var transactions = await transactionQuery.OrderByDescending(transaction => transaction.CreatedAt).Take(SourceRowLimit / 2)
            .Select(transaction => new { transaction.StudentBalance.UserId, transaction.StudentBalance.User.FullName, transaction.Amount, transaction.TransactionType, transaction.CreatedAt }).ToListAsync(ct);
        return recharges.Select(item => Row(("_studentId", item.UserId.ToString()), ("recordType", "recharge"), ("studentName", item.FullName), ("amount", item.Amount), ("status", item.Status), ("transactionType", null), ("createdAt", item.CreatedAt)))
            .Concat(transactions.Select(item => Row(("_studentId", item.UserId.ToString()), ("recordType", "balance"), ("studentName", item.FullName), ("amount", item.Amount), ("status", null), ("transactionType", item.TransactionType), ("createdAt", item.CreatedAt)))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadContentAsync(Guid? teacherId, CancellationToken ct)
    {
        var packagesQuery = _db.Packages.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) packagesQuery = packagesQuery.Where(package => package.TeacherId == teacherId);
        var packages = await packagesQuery.Take(SourceRowLimit / 2).Select(package => new { package.Name, TeacherName = package.Teacher.User.FullName, package.Price, package.IsActive, package.CreatedAt }).ToListAsync(ct);
        var videosQuery = _db.LessonVideos.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) videosQuery = videosQuery.Where(video => video.Lesson.ContentSection.Term.Package.TeacherId == teacherId);
        var videos = await videosQuery.Take(SourceRowLimit / 2).Select(video => new { video.Title, TeacherName = video.Lesson.ContentSection.Term.Package.Teacher.User.FullName, video.IsActive, video.CreatedAt }).ToListAsync(ct);
        return packages.Select(item => Row(("contentType", "package"), ("name", item.Name), ("teacherName", item.TeacherName), ("price", item.Price), ("isActive", item.IsActive), ("createdAt", item.CreatedAt)))
            .Concat(videos.Select(item => Row(("contentType", "video"), ("name", item.Title), ("teacherName", item.TeacherName), ("price", null), ("isActive", item.IsActive), ("createdAt", item.CreatedAt)))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadEngagementAsync(Guid? teacherId, CancellationToken ct)
    {
        var query = _db.VideoWatchEvents.AsNoTracking().AsQueryable();
        if (teacherId.HasValue)
            query = query.Where(watch => watch.LessonVideo.Lesson.ContentSection.Term.Package.TeacherId == teacherId);
        var data = await query.OrderByDescending(watch => watch.UpdatedAt ?? watch.CreatedAt).Take(SourceRowLimit)
            .Select(watch => new { watch.UserId, StudentName = watch.User.FullName, VideoName = watch.LessonVideo.Title, TeacherName = watch.LessonVideo.Lesson.ContentSection.Term.Package.Teacher.User.FullName, WatchedSeconds = watch.ActualWatchedSeconds, watch.WatchCount, watch.IsLocked, LastActivityAt = watch.UpdatedAt ?? watch.CreatedAt })
            .ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.UserId.ToString()), ("studentName", item.StudentName), ("videoName", item.VideoName), ("teacherName", item.TeacherName), ("watchedSeconds", item.WatchedSeconds), ("watchCount", item.WatchCount), ("isLocked", item.IsLocked), ("lastActivityAt", item.LastActivityAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadAttendanceAsync(Guid? teacherId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var grants = _db.StudentAccessGrants.AsNoTracking()
            .Where(grant => grant.PackageId.HasValue && grant.IsActive && !grant.CancelledAt.HasValue && (!grant.ExpiresAt.HasValue || grant.ExpiresAt > now));
        if (teacherId.HasValue)
            grants = grants.Where(grant => _db.Packages.Any(package => package.Id == grant.PackageId && package.TeacherId == teacherId.Value));
        var rows = await grants.OrderByDescending(grant => grant.GrantedAt).Take(SourceRowLimit)
            .Select(grant => new
            {
                grant.UserId, StudentName = grant.User.FullName,
                PackageName = _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Name).FirstOrDefault(),
                TeacherName = _db.Packages.Where(package => package.Id == grant.PackageId).Select(package => package.Teacher.User.FullName).FirstOrDefault(),
                grant.GrantedAt,
                LastActivityAt = _db.VideoWatchEvents
                    .Where(watch => watch.UserId == grant.UserId && watch.LessonVideo.Lesson.ContentSection.Term.PackageId == grant.PackageId)
                    .Select(watch => (DateTime?)(watch.UpdatedAt ?? watch.CreatedAt))
                    .Max(),
            }).ToListAsync(ct);
        return rows.Select(row => Row(
            ("_studentId", row.UserId.ToString()), ("studentName", row.StudentName), ("packageName", row.PackageName), ("teacherName", row.TeacherName),
            ("attendanceStatus", row.LastActivityAt.HasValue ? "present" : "absent"), ("grantedAt", row.GrantedAt), ("lastActivityAt", row.LastActivityAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadAssessmentsAsync(Guid? teacherId, CancellationToken ct)
    {
        var attemptsQuery = _db.StudentExamAttempts.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) attemptsQuery = attemptsQuery.Where(attempt => attempt.Exam.CreatedByTeacherId == teacherId);
        var attempts = await attemptsQuery.OrderByDescending(attempt => attempt.CreatedAt).Take(SourceRowLimit / 2)
            .Select(attempt => new { attempt.UserId, attempt.Exam.Title, attempt.User.FullName, Score = attempt.ScoreAchieved, Passed = attempt.IsPassed, attempt.CreatedAt }).ToListAsync(ct);
        var homeworkQuery = _db.HomeworkSubmissions.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) homeworkQuery = homeworkQuery.Where(submission => _db.Lessons.Any(lesson => lesson.Id == submission.Homework.LessonId && lesson.ContentSection.Term.Package.TeacherId == teacherId));
        var homeworks = await homeworkQuery.OrderByDescending(submission => submission.StartedAt).Take(SourceRowLimit / 2)
            .Select(submission => new { submission.StudentId, submission.Homework.Title, submission.Student.FullName, Score = submission.OverallScore, Passed = submission.Homework.PassingScoreThreshold == null || submission.OverallScore >= submission.Homework.PassingScoreThreshold, CreatedAt = submission.StartedAt }).ToListAsync(ct);
        return attempts.Select(item => Row(("_studentId", item.UserId.ToString()), ("assessmentType", "exam"), ("title", item.Title), ("studentName", item.FullName), ("score", item.Score), ("isPassed", item.Passed), ("createdAt", item.CreatedAt)))
            .Concat(homeworks.Select(item => Row(("_studentId", item.StudentId.ToString()), ("assessmentType", "homework"), ("title", item.Title), ("studentName", item.FullName), ("score", item.Score), ("isPassed", item.Passed), ("createdAt", item.CreatedAt)))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadTeacherFinanceAsync(Guid? teacherId, CancellationToken ct)
    {
        var query = _db.TeacherFinancialAllocations.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) query = query.Where(allocation => allocation.TeacherId == teacherId);
        var data = await query.OrderByDescending(allocation => allocation.TeacherFinancialEvent.OccurredAt).Take(SourceRowLimit)
            .Select(allocation => new { allocation.TeacherFinancialEvent.StudentId, TeacherName = allocation.Teacher.User.FullName, StudentName = allocation.StudentNameSnapshot, ContentName = allocation.ContentNameSnapshot, GrossAmount = allocation.GrossBasisAmount, TeacherShare = allocation.TeacherShareAmount, PayoutStatus = allocation.PayoutStatus.ToString(), allocation.TeacherFinancialEvent.OccurredAt }).ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.StudentId?.ToString()), ("teacherName", item.TeacherName), ("studentName", item.StudentName), ("contentName", item.ContentName), ("grossAmount", item.GrossAmount), ("teacherShare", item.TeacherShare), ("payoutStatus", item.PayoutStatus), ("occurredAt", item.OccurredAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadStaffAsync(CancellationToken ct)
    {
        var data = await _db.UserRoles.AsNoTracking().Where(link => link.Role.Type != Domain.Enums.RoleType.Student)
            .OrderByDescending(link => link.User.CreatedAt).Take(SourceRowLimit)
            .Select(link => new { link.User.FullName, link.User.PhoneNumber, Role = link.Role.Name, link.User.IsActive, link.User.CreatedAt }).ToListAsync(ct);
        return data.Select(item => Row(("employeeName", item.FullName), ("phone", item.PhoneNumber), ("role", item.Role), ("isActive", item.IsActive), ("createdAt", item.CreatedAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadSupportAsync(CancellationToken ct)
    {
        var data = await _db.LiveSupportConversations.AsNoTracking().OrderByDescending(conversation => conversation.CreatedAt).Take(SourceRowLimit)
            .Select(conversation => new { conversation.StudentUserId, Status = conversation.Status.ToString(), Channel = conversation.ParticipantType.ToString(), StudentName = conversation.StudentUserId.HasValue ? _db.Users.Where(user => user.Id == conversation.StudentUserId).Select(user => user.FullName).FirstOrDefault() : null, conversation.CreatedAt }).ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.StudentUserId?.ToString()), ("status", item.Status), ("channel", item.Channel), ("studentName", item.StudentName), ("createdAt", item.CreatedAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadCommentsAsync(Guid? teacherId, CancellationToken ct)
    {
        var lessonQuery = _db.LessonComments.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) lessonQuery = lessonQuery.Where(comment => comment.Lesson.ContentSection.Term.Package.TeacherId == teacherId);
        var lessonComments = await lessonQuery.OrderByDescending(comment => comment.CreatedAt).Take(SourceRowLimit / 2)
            .Select(comment => new { comment.AuthorUserId, AuthorName = comment.AuthorUser.FullName, ContentName = comment.Lesson.Title, Status = comment.Status.ToString(), comment.CreatedAt }).ToListAsync(ct);
        var postQuery = _db.CommunityPosts.AsNoTracking().AsQueryable();
        if (teacherId.HasValue) postQuery = postQuery.Where(post => post.TeacherId == teacherId);
        var posts = await postQuery.OrderByDescending(post => post.CreatedAt).Take(SourceRowLimit / 2)
            .Select(post => new { post.AuthorUserId, AuthorName = post.AuthorUser.FullName, ContentName = post.Body, Status = post.Status.ToString(), post.CreatedAt }).ToListAsync(ct);
        return lessonComments.Select(item => Row(("_studentId", item.AuthorUserId.ToString()), ("recordType", "lesson-comment"), ("authorName", item.AuthorName), ("contentName", item.ContentName), ("status", item.Status), ("createdAt", item.CreatedAt)))
            .Concat(posts.Select(item => Row(("_studentId", item.AuthorUserId.ToString()), ("recordType", "community-post"), ("authorName", item.AuthorName), ("contentName", item.ContentName), ("status", item.Status), ("createdAt", item.CreatedAt)))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadParentTrackingAsync(HashSet<Guid>? scope, CancellationToken ct)
    {
        var query = _db.StudentProfiles.AsNoTracking().AsQueryable();
        if (scope != null) query = query.Where(profile => scope.Contains(profile.UserId));
        var data = await query.OrderByDescending(profile => profile.CreatedAt).Take(SourceRowLimit)
            .Select(profile => new { profile.UserId, profile.User.FullName, HasCode = profile.ParentTrackingCode != null, PopupSeen = profile.HasSeenTrackingCodePopup, RegisteredAt = profile.User.CreatedAt }).ToListAsync(ct);
        return data.Select(item => Row(("_studentId", item.UserId.ToString()), ("studentName", item.FullName), ("hasCode", item.HasCode), ("popupSeen", item.PopupSeen), ("registeredAt", item.RegisteredAt))).ToList();
    }

    private async Task<List<Dictionary<string, object?>>> LoadOperationsAsync(CancellationToken ct)
    {
        var data = await _db.AuditLogs.AsNoTracking().OrderByDescending(log => log.CreatedAt).Take(SourceRowLimit)
            .Select(log => new { log.Action, log.EntityType, PerformedBy = log.PerformedByUser != null ? log.PerformedByUser.FullName : null, log.CreatedAt }).ToListAsync(ct);
        return data.Select(item => Row(("action", item.Action), ("entityType", item.EntityType), ("performedBy", item.PerformedBy), ("createdAt", item.CreatedAt))).ToList();
    }

    private async Task EnrichStudentIdentityAsync(List<Dictionary<string, object?>> rows, CancellationToken ct)
    {
        var studentIds = StudentIds(rows);
        if (studentIds.Length == 0) return;

        var profiles = await _db.StudentProfiles.AsNoTracking()
            .Where(profile => studentIds.Contains(profile.UserId))
            .Select(profile => new
            {
                profile.UserId,
                profile.User.PhoneNumber,
                profile.EducationStage,
                profile.GradeLevel,
                profile.StudyTrack
            })
            .ToListAsync(ct);
        var identities = profiles.ToDictionary(
            profile => profile.UserId,
            profile => new StudentIdentity(
                profile.PhoneNumber,
                profile.EducationStage.ToString(),
                profile.GradeLevel.ToString(),
                profile.StudyTrack.HasValue ? profile.StudyTrack.Value.ToString() : null));

        foreach (var row in rows) ApplyStudentIdentity(row, identities);
    }

    private static Guid[] StudentIds(IEnumerable<Dictionary<string, object?>> rows) => rows
        .Select(row => Convert.ToString(row.GetValueOrDefault("_studentId"), CultureInfo.InvariantCulture))
        .Where(studentId => Guid.TryParse(studentId, out _))
        .Select(studentId => Guid.Parse(studentId!))
        .Distinct()
        .ToArray();

    private static void ApplyStudentIdentity(Dictionary<string, object?> row, IReadOnlyDictionary<Guid, StudentIdentity> identities)
    {
        if (!Guid.TryParse(Convert.ToString(row.GetValueOrDefault("_studentId"), CultureInfo.InvariantCulture), out var studentId) ||
            !identities.TryGetValue(studentId, out var identity)) return;
        row["phone"] = identity.Phone;
        row["stage"] = identity.Stage;
        row["grade"] = identity.Grade;
        row["studyTrack"] = identity.StudyTrack;
    }

    private sealed record StudentIdentity(string? Phone, string Stage, string Grade, string? StudyTrack);

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<ReportFilter> Flatten(ReportFilterGroup? group)
    {
        if (group == null) yield break;
        foreach (var filter in group.Filters ?? []) yield return filter;
        foreach (var child in group.Groups ?? [])
        foreach (var filter in Flatten(child)) yield return filter;
    }

    private static IEnumerable<ReportFilterGroup> FlattenGroups(ReportFilterGroup? group)
    {
        if (group == null) yield break;
        yield return group;
        foreach (var child in group.Groups ?? [])
        foreach (var nested in FlattenGroups(child)) yield return nested;
    }

    private static bool MatchesGroup(IReadOnlyDictionary<string, object?> row, ReportFilterGroup? group)
    {
        if (group == null) return true;
        var matches = (group.Filters ?? []).Select(filter => MatchesFilter(row.GetValueOrDefault(filter.Field), filter))
            .Concat((group.Groups ?? []).Select(child => MatchesGroup(row, child))).ToArray();
        return matches.Length == 0 || (group.Logic.Equals("or", StringComparison.OrdinalIgnoreCase) ? matches.Any(value => value) : matches.All(value => value));
    }

    private static bool MatchesFilter(object? actual, ReportFilter filter)
    {
        var values = (filter.Values ?? []).Select(ReadJsonValue).ToArray();
        var operation = filter.Operator.ToLowerInvariant();
        if (operation == "is-empty") return actual == null || string.IsNullOrWhiteSpace(actual.ToString());
        if (operation == "not-empty") return actual != null && !string.IsNullOrWhiteSpace(actual.ToString());
        if (operation == "contains") return values.Any(value => actual?.ToString()?.Contains(value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase) == true);
        if (operation == "in") return values.Any(value => Compare(actual, value) == 0);
        if (operation == "between" && values.Length >= 2) return Compare(actual, values[0]) >= 0 && Compare(actual, values[1]) <= 0;
        var compared = values.Length == 0 ? Compare(actual, null) : Compare(actual, values[0]);
        return operation switch { "eq" => compared == 0, "neq" => compared != 0, "gt" or "after" => compared > 0, "gte" => compared >= 0, "lt" or "before" => compared < 0, "lte" => compared <= 0, _ => false };
    }

    private static object? ReadJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String when value.TryGetDateTime(out var date) => date,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => value.ToString()
    };

    private static bool ValuesMatchType(string fieldType, string operation, IReadOnlyList<JsonElement> values)
    {
        if (operation is "is-empty" or "not-empty") return values.Count == 0;
        return fieldType switch
        {
            "number" => values.All(value => value.ValueKind == JsonValueKind.Number),
            "boolean" => values.All(value => value.ValueKind is JsonValueKind.True or JsonValueKind.False),
            "date" => values.All(value => value.ValueKind == JsonValueKind.String && value.TryGetDateTime(out _)),
            _ => values.All(value => value.ValueKind == JsonValueKind.String)
        };
    }

    private static int Compare(object? left, object? right)
    {
        if (left == null) return right == null ? 0 : -1;
        if (right == null) return 1;
        if (TryDecimal(left, out var leftNumber) && TryDecimal(right, out var rightNumber)) return leftNumber.CompareTo(rightNumber);
        if (TryDate(left, out var leftDate) && TryDate(right, out var rightDate)) return leftDate.CompareTo(rightDate);
        if (left is bool leftBool && bool.TryParse(right.ToString(), out var rightBool)) return leftBool.CompareTo(rightBool);
        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryDecimal(object value, out decimal number) => decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out number);
    private static bool TryDate(object value, out DateTime date)
    {
        if (value is DateTime typed)
        {
            date = typed;
            return true;
        }
        return DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
    }

    private static IReadOnlyList<ReportMetricDto> BuildSummary(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var primaryNumericKey = new[] { "amount", "teacherShare", "grossAmount", "watchedSeconds", "score", "price" }
            .FirstOrDefault(key => rows.Any(row => row.ContainsKey(key)));
        if (primaryNumericKey == null) return [new("count", "عدد النتائج", rows.Count)];
        var total = rows.Select(row => row.GetValueOrDefault(primaryNumericKey)).Where(number => number != null && TryDecimal(number, out _))
            .Sum(number => { TryDecimal(number!, out var parsed); return parsed; });
        return [new("count", "عدد النتائج", rows.Count), new("total", "الإجمالي", total)];
    }

    private static ReportChartDto BuildChart(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var chartKey = new[] { "status", "recordType", "contentType", "assessmentType", "isActive", "isPassed", "codeType" }
            .FirstOrDefault(key => rows.Any(row => row.ContainsKey(key))) ?? "total";
        var points = chartKey == "total"
            ? [new ReportChartPointDto("النتائج", rows.Count)]
            : rows.GroupBy(row => row.GetValueOrDefault(chartKey)?.ToString() ?? "غير محدد").OrderByDescending(group => group.Count()).Take(20).Select(group => new ReportChartPointDto(group.Key, group.Count())).ToArray();
        return new ReportChartDto("bar", chartKey, points);
    }

    private static string Describe(ReportFilter filter) => $"{filter.Field} {filter.Operator} ({filter.Values?.Count ?? 0})";

    private static TimeZoneInfo ResolveCairoTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Egypt Standard Time"); }
    }
}
