using ClosedXML.Excel;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Teacher;

namespace NaderGorge.Infrastructure.Services;

public static class TeacherStudentsWorkbook
{
    public static byte[] Build(IReadOnlyList<TeacherStudentDto> students, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("الطلاب");
        sheet.RightToLeft = true;

        string[] headers =
        [
            "الاسم الكامل", "كود الطالب", "رقم الهاتف", "رقم الهاتف الإضافي", "هاتف ولي الأمر",
            "هاتف ولي الأمر الإضافي", "هاتف الأم", "النوع", "الجنسية", "تاريخ ميلاد الطالب",
            "تاريخ ميلاد الأب", "تاريخ ميلاد الأم", "الأب على قيد الحياة", "الأم على قيد الحياة",
            "المرحلة الدراسية", "الصف الدراسي", "الشعبة / التخصص", "المدرسة", "نوع المدرسة",
            "المحافظة", "المنطقة / المركز", "العنوان", "حالة الحساب", "تاريخ الانضمام للمنصة",
            "الباقات النشطة", "آخر باقة مفعلة", "عدد الباقات النشطة", "عدد الصلاحيات النشطة",
            "أول تفعيل نشط (القاهرة)", "آخر تفعيل نشط (القاهرة)", "معرّف الطالب"
        ];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];

        for (var index = 0; index < students.Count; index++)
        {
            ct.ThrowIfCancellationRequested();
            WriteStudent(sheet, index + 2, students[index]);
        }

        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#0A1D3D");
        header.Style.Font.FontColor = XLColor.White;
        header.Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, students.Count + 1, headers.Length).SetAutoFilter();
        sheet.Columns(1, headers.Length).AdjustToContents(12, 44);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteStudent(IXLWorksheet sheet, int row, TeacherStudentDto student)
    {
        var details = student.ExportDetails ?? throw new InvalidOperationException("Student export details are required.");
        object?[] values =
        [
            student.FullName, student.StudentCode, student.PhoneNumber, student.SecondaryPhone, student.ParentPhone,
            details.SecondaryParentPhone, student.MotherPhone, GenderLabel(student.Gender), details.Nationality,
            BirthDate(details.DateOfBirth), BirthDate(details.FatherDateOfBirth), BirthDate(details.MotherDateOfBirth),
            YesNo(details.IsFatherAlive), YesNo(details.IsMotherAlive), StageLabel(student.EducationStage),
            StudentFollowUpExportService.GradeLabel(student.GradeLevel),
            StudentFollowUpExportService.StudyTrackLabel(student.StudyTrack), student.SchoolName, SchoolTypeLabel(student.SchoolType),
            student.Governorate, student.District, student.Address, details.IsActive ? "نشط" : "موقوف",
            CairoTime.ToLocal(details.CreatedAt).Date, string.Join("، ", student.ActivePackages.Select(package => package.Name)),
            student.ActivatedPackageName, student.ActivePackageCount, student.ActiveGrantCount,
            CairoTime.ToLocal(student.ActivatedAt).Date,
            student.LastActivationAt is { } lastActivation ? CairoTime.ToLocal(lastActivation).Date : null,
            student.Id.ToString()
        ];
        for (var column = 0; column < values.Length; column++)
        {
            var cell = sheet.Cell(row, column + 1);
            cell.Value = XLCellValue.FromObject(values[column]);
            if (cell.DataType == XLDataType.DateTime) cell.Style.DateFormat.Format = "yyyy-mm-dd";
        }
    }

    private static DateTime? BirthDate(DateTime? date) => date == DateTime.MinValue ? null : date?.Date;

    private static string? YesNo(bool? value) => value switch { true => "نعم", false => "لا", null => null };

    private static string? GenderLabel(string? value) => value switch
    {
        "Male" => "ذكر", "Female" => "أنثى", _ => value
    };

    private static string StageLabel(string value) => value switch
    {
        "Primary" => "ابتدائي", "Preparatory" => "إعدادي", "Secondary" => "ثانوي",
        "Baccalaureate" => "بكالوريا", "Azhari" => "أزهري", "American" => "أمريكي", _ => value
    };

    private static string? SchoolTypeLabel(string? value) => value switch
    {
        "Government" => "حكومية", "Language" => "لغات", "Experimental" => "تجريبية",
        "Private" => "خاصة", "Azhari" => "أزهرية", "American" => "أمريكية", _ => value
    };
}
