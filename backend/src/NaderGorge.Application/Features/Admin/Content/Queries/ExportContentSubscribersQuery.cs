using System.Text;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Common;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.Application.Features.Admin.Content.Queries;

public record ExportContentSubscribersQuery(
    string ContentType,
    Guid ContentId,
    string? Search = null
) : IRequest<byte[]>;

public class ExportContentSubscribersQueryHandler : IRequestHandler<ExportContentSubscribersQuery, byte[]>
{
    private readonly IAppDbContext _db;

    public ExportContentSubscribersQueryHandler(IAppDbContext db) => _db = db;

    public async Task<byte[]> Handle(ExportContentSubscribersQuery request, CancellationToken ct)
    {
        var grantType = MapContentType(request.ContentType);

        var query = _db.StudentAccessGrants.AsQueryable();

        if (grantType is not null)
            query = query.Where(sag => sag.GrantType == grantType.Value);

        query = request.ContentType.ToLowerInvariant() switch
        {
            "package" => query.Where(sag => sag.PackageId == request.ContentId),
            "term" => query.Where(sag => sag.TermId == request.ContentId),
            "section" => query.Where(sag => sag.ContentSectionId == request.ContentId),
            _ => query.Where(sag => false)
        };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(sag =>
                sag.User.FullName.ToLower().Contains(search) ||
                sag.User.PhoneNumber.Contains(search));
        }

        var rows = await query
            .OrderByDescending(sag => sag.GrantedAt)
            .Select(sag => new
            {
                sag.User.FullName,
                sag.User.PhoneNumber,
                Governorate = sag.User.StudentProfile != null ? sag.User.StudentProfile.Governorate : "",
                District = sag.User.StudentProfile != null ? sag.User.StudentProfile.District : "",
                EducationStage = sag.User.StudentProfile != null ? sag.User.StudentProfile.EducationStage.ToString() : "",
                GradeLevel = sag.User.StudentProfile != null ? sag.User.StudentProfile.GradeLevel.ToString() : "",
                SchoolName = sag.User.StudentProfile != null ? sag.User.StudentProfile.SchoolName : "",
                ParentPhone = sag.User.StudentProfile != null ? sag.User.StudentProfile.ParentPhone : "",
                MotherPhone = sag.User.StudentProfile != null ? sag.User.StudentProfile.MotherPhone : "",
                sag.GrantedAt,
                sag.IsActive
            })
            .ToListAsync(ct);

        var sb = new StringBuilder();

        // Header row
        sb.AppendLine("الاسم الكامل,رقم الهاتف,المحافظة,المنطقة,المرحلة,الصف,المدرسة,هاتف الأب,هاتف الأم,تاريخ الاشتراك,الحالة");

        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                CsvEscape(row.FullName),
                CsvEscape(row.PhoneNumber),
                CsvEscape(row.Governorate),
                CsvEscape(row.District ?? ""),
                CsvEscape(MapEducationStageAr(row.EducationStage)),
                CsvEscape(MapGradeLevelAr(row.GradeLevel)),
                CsvEscape(row.SchoolName ?? ""),
                CsvEscape(row.ParentPhone ?? ""),
                CsvEscape(row.MotherPhone ?? ""),
                CsvEscape(row.GrantedAt.ToString("yyyy-MM-dd")),
                CsvEscape(row.IsActive ? "نشط" : "ملغى")
            ));
        }

        // UTF-8 BOM for Excel compatibility
        var bom = Encoding.UTF8.GetPreamble();
        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + csvBytes.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(csvBytes, 0, result, bom.Length, csvBytes.Length);

        return result;
    }

    private static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static CodeType? MapContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "package" => CodeType.Package,
            "term" => CodeType.Term,
            "section" => CodeType.Month,
            _ => null
        };
    }

    private static string MapEducationStageAr(string stage)
    {
        return stage switch
        {
            "Primary" => "ابتدائي",
            "Preparatory" => "إعدادي",
            "Secondary" => "ثانوي",
            _ => stage
        };
    }

    private static string MapGradeLevelAr(string grade)
    {
        return grade switch
        {
            "FirstPrimary" => "أولى ابتدائي",
            "SecondPrimary" => "ثانية ابتدائي",
            "ThirdPrimary" => "ثالثة ابتدائي",
            "FourthPrimary" => "رابعة ابتدائي",
            "FifthPrimary" => "خامسة ابتدائي",
            "SixthPrimary" => "سادسة ابتدائي",
            "FirstPreparatory" => "أولى إعدادي",
            "SecondPreparatory" => "ثانية إعدادي",
            "ThirdPreparatory" => "ثالثة إعدادي",
            "FirstSecondary" => "أولى ثانوي",
            "SecondSecondary" => "ثانية ثانوي",
            "ThirdSecondary" => "ثالثة ثانوي",
            _ => grade
        };
    }
}
