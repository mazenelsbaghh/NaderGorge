using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services;

public sealed partial class WhatsAppCampaignService
{
    public async Task<WhatsAppContactCandidatePageDto> SearchContactCandidatesAsync(
        string search,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var term = search?.Trim() ?? string.Empty;
        if (term.Length is < 2 or > 100) throw Invalid("اكتب حرفين على الأقل للبحث عن جهة اتصال.");
        var searchDigits = new string(term.Where(char.IsAsciiDigit).ToArray());
        if (searchDigits.Length >= 10)
            throw Invalid("البحث برقم الهاتف غير مسموح؛ استخدم اسم الطالب أو كوده.");
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var students = await _db.Users.AsNoTracking()
            .Where(user => user.IsActive && !user.IsDeleted && user.StudentProfile != null &&
                user.UserRoles.Any(link => link.Role.Type == RoleType.Student) &&
                (EF.Functions.ILike(user.FullName, $"%{term}%") ||
                 user.StudentProfile.StudentCode != null && user.StudentProfile.StudentCode.Contains(term)))
            .OrderBy(user => user.FullName).ThenBy(user => user.Id)
            .Take(5_001)
            .Select(user => new AudienceStudentRow(
                user.Id, user.FullName, user.PhoneNumber, user.StudentProfile!.SecondaryPhone,
                user.StudentProfile.ParentPhone, user.StudentProfile.SecondaryParentPhone,
                user.StudentProfile.MotherPhone, user.StudentProfile.EducationStage,
                user.StudentProfile.GradeLevel, user.StudentProfile.StudyTrack,
                user.StudentProfile.Governorate, user.StudentProfile.SchoolName))
            .ToListAsync(ct);
        if (students.Count > 5_000) throw Invalid("نتائج البحث كثيرة؛ اكتب اسمًا أو كود طالب أدق.");
        var contacts = students.SelectMany(student => ContactRoleWhitelist.Select(role => new
            {
                Student = student,
                Role = role,
                E164 = NormalizeE164(ContactPhone(student, role))
            }))
            .Where(item => item.E164 is not null)
            .Select(item => new
            {
                item.Student,
                item.Role,
                E164 = item.E164!,
                Hash = _protector.DestinationHash(item.E164!)
            })
            .OrderBy(item => item.Student.FullName, StringComparer.Ordinal)
            .ThenBy(item => item.Student.StudentUserId)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        var pageRows = contacts.Skip((page - 1) * pageSize).Take(pageSize + 1).ToArray();
        var hashes = pageRows.Take(pageSize).Select(item => item.Hash).Distinct().ToArray();
        var preferences = hashes.Length == 0
            ? []
            : await _db.WhatsAppContactPreferences.AsNoTracking()
                .Where(item => hashes.Contains(item.DestinationHash) && item.EffectiveAt <= DateTime.UtcNow)
                .ToListAsync(ct);
        var byDestination = preferences.GroupBy(item => item.DestinationHash)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var items = pageRows.Take(pageSize).Select(item =>
        {
            var rows = byDestination.GetValueOrDefault(item.Hash) ?? [];
            return new WhatsAppContactCandidateDto(
                item.Student.StudentUserId,
                item.Student.FullName,
                item.Role,
                $"***{item.E164[^4..]}",
                CategoryState(rows, WhatsAppContactPreferenceCategory.Marketing),
                CategoryState(rows, WhatsAppContactPreferenceCategory.Utility),
                GlobalState(rows));
        }).ToArray();
        return new WhatsAppContactCandidatePageDto(items, page, pageSize, pageRows.Length > pageSize);
    }

    private static WhatsAppContactCategoryStateDto CategoryState(
        IReadOnlyList<WhatsAppContactPreference> rows,
        WhatsAppContactPreferenceCategory category)
    {
        static WhatsAppContactPreference? Latest(
            IReadOnlyList<WhatsAppContactPreference> source,
            WhatsAppContactPreferenceCategory target) => source
            .Where(item => item.Category == target)
            .OrderByDescending(item => item.EffectiveAt)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.State == WhatsAppContactPreferenceState.OptedOut)
            .ThenByDescending(item => item.Id).FirstOrDefault();
        var categoryPreference = Latest(rows, category);
        var global = Latest(rows, WhatsAppContactPreferenceCategory.All);
        var overridden = global is not null &&
            global.State == WhatsAppContactPreferenceState.OptedOut &&
            (categoryPreference is null || PreferenceAtLeastAsRecent(global, categoryPreference));
        var state = overridden
            ? WhatsAppContactPreferenceState.OptedOut.ToString()
            : categoryPreference?.State.ToString() ?? "Unknown";
        var effective = overridden ? global : categoryPreference;
        return new WhatsAppContactCategoryStateDto(
            state,
            categoryPreference?.Id,
            effective?.EffectiveAt,
            overridden,
            effective?.Id);
    }

    private static WhatsAppContactCategoryStateDto GlobalState(
        IReadOnlyList<WhatsAppContactPreference> rows)
    {
        var global = rows.Where(item => item.Category == WhatsAppContactPreferenceCategory.All)
            .OrderByDescending(item => item.EffectiveAt)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.State == WhatsAppContactPreferenceState.OptedOut)
            .ThenByDescending(item => item.Id)
            .FirstOrDefault();
        return new WhatsAppContactCategoryStateDto(
            global?.State.ToString() ?? "Unknown",
            global?.Id,
            global?.EffectiveAt,
            false,
            global?.Id);
    }
}
