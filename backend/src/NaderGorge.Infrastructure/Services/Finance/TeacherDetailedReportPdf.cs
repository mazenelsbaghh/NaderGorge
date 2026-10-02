using System.Globalization;
using NaderGorge.Application.Common;
using NaderGorge.Application.Interfaces.Finance;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NaderGorge.Infrastructure.Services.Finance;

public static class TeacherDetailedReportPdf
{
    private const string Navy = "#021f45", Teal = "#068d96", Gold = "#F2DBA6", Line = "#DBE6EB", Pale = "#F2F7F8";
    private static readonly Lazy<string> Logo = new(() =>
    {
        using var stream = typeof(TeacherDetailedReportPdf).Assembly.GetManifestResourceStream("NaderGorge.Infrastructure.Assets.MassarLogo.svg")
            ?? throw new InvalidOperationException("Massar report logo is missing");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });
    private static readonly Lazy<bool> Font = new(() =>
    {
        using var stream = typeof(TeacherDetailedReportPdf).Assembly.GetManifestResourceStream("NaderGorge.Infrastructure.Assets.Tajawal-Regular.ttf")
            ?? throw new InvalidOperationException("Massar report font is missing");
        FontManager.RegisterFontWithCustomName("Massar Report", stream);
        return true;
    });

    public static byte[] Generate(TeacherDetailedReport report)
    {
        _ = Font.Value;
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document =>
        {
            Courses(document, report);
            Summary(document, report);
            Appendices(document, report);
        }).GeneratePdf();
    }

    private static void Configure(PageDescriptor page, TeacherDetailedReport report)
    {
        page.Margin(34);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(x => x.FontFamily("Massar Report").FontSize(10).FontColor(Navy));
        page.Footer().BorderTop(.5f).BorderColor(Line).PaddingTop(5).Row(row =>
        {
            row.RelativeItem().Text(PeriodLabel(report.Period)).FontSize(8);
            row.ConstantItem(80).ContentFromLeftToRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontSize(8)); text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
            });
            row.ConstantItem(90).ContentFromLeftToRight().Text("Massar Academy").FontSize(8);
        });
    }

    private static void Brand(IContainer container, TeacherDetailedReport report, string title)
    {
        container.BorderBottom(2).BorderColor(Teal).PaddingBottom(10).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("منصة مسار").FontColor(Teal).FontSize(10);
                column.Item().Text(title).Bold().FontSize(21);
                column.Item().PaddingTop(5).Text($"حساب {report.TeacherName} · {PeriodLabel(report.Period)}").FontSize(10);
            });
            row.ConstantItem(82).Height(66).Svg(Logo.Value);
        });
    }

    private static string PeriodLabel(TeacherReportPeriod period) =>
        $"من {(period.From.HasValue ? period.From.Value.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("ar-EG")) : "البداية")} لحد {period.To.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("ar-EG"))}";
    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
    private static string Date(DateTime at) => CairoTime.ToLocal(at).ToString("dd/MM/yyyy, HH:mm", CultureInfo.InvariantCulture);
    private static string Count(IEnumerable<Guid?> ids) => ids.Where(x => x.HasValue).Distinct().Count().ToString(CultureInfo.InvariantCulture);

    private static void Courses(IDocumentContainer document, TeacherDetailedReport report)
    {
        var courseIds = report.Purchases.Select(x => x.CourseId).Concat(report.Gifts.Select(x => x.CourseId))
            .Concat(report.Cancellations.Select(x => x.CourseId)).Distinct().ToArray();
        var chunks = courseIds.Length == 0 ? new[] { Array.Empty<Guid>() } : courseIds.Chunk(2);
        foreach (var chunk in chunks)
            document.Page(page =>
            {
                Configure(page, report); page.Size(PageSizes.A4);
                page.Header().Element(x => Brand(x, report, "تفاصيل حساب المدرس"));
                page.Content().PaddingTop(14).Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("المدفوع بيتحسب حسب اتفاق المستر، والمجاني والهدايا من غير عمولة. كل الأرقام بالجنيه.").FontSize(10);
                    if (report.Agreements.Count > 0)
                        column.Item().Background(Pale).Padding(9).Column(terms =>
                        {
                            foreach (var agreement in report.Agreements) terms.Item().Text(agreement).FontSize(8);
                        });
                    foreach (var courseId in chunk) column.Item().PreventPageBreak().Element(x => Course(x, report, courseId));
                    if (chunk.Length == 0) column.Item().Text("مفيش مشتريات أو هدايا في الفترة دي.");
                    column.Item().Text("عدد الطلاب بيحسب كل طالب مرة واحدة داخل الكورس، حتى لو اشترى أكتر من مرة. عدد الشراء ممكن يزيد عن عدد الطلاب.").FontSize(8);
                });
            });
    }

    private static void Course(IContainer container, TeacherDetailedReport report, Guid courseId)
    {
        var purchases = report.Purchases.Where(x => x.CourseId == courseId).ToArray();
        var gifts = report.Gifts.Where(x => x.CourseId == courseId).ToArray();
        var cancellations = report.Cancellations.Where(x => x.CourseId == courseId).ToArray();
        var paid = purchases.Where(x => x.Counted && x.Paid > 0).ToArray();
        var name = purchases.FirstOrDefault()?.Course ?? gifts.FirstOrDefault()?.Course ?? cancellations.First().Course;
        container.Border(1).BorderColor(Line).Column(column =>
        {
            var people = Count(purchases.Where(x => x.Counted).Select(x => x.StudentId)
                .Concat(gifts.Where(x => x.Status == "موجود").Select(x => (Guid?)x.StudentId)));
            column.Item().Background(Pale).BorderRight(3).BorderColor(Teal).Padding(10).Row(row =>
            {
                row.RelativeItem().Text(name).Bold().FontSize(13);
                row.ConstantItem(115).Text($"{people} طالب مختلف عنده").FontColor(Teal).FontSize(9);
            });
            var kinds = new[] { "الحصة", "الشهر", "الترم / الكورس", "السنة / الباقة" }
                .Concat(purchases.Select(x => x.Kind)).Distinct();
            var rows = kinds.Select(kind =>
            {
                var group = paid.Where(x => x.Kind == kind).ToArray();
                return new[] { kind, Count(group.Select(x => x.StudentId)), group.Length.ToString(),
                    Money(group.Sum(x => x.Paid)), Money(group.Sum(x => x.Platform)), Money(group.Sum(x => x.Teacher)) };
            }).ToList();
            rows.Add(["إجمالي الكورس", Count(paid.Select(x => x.StudentId)), paid.Length.ToString(),
                Money(paid.Sum(x => x.Paid)), Money(paid.Sum(x => x.Platform)), Money(paid.Sum(x => x.Teacher))]);
            column.Item().Element(x => Table(x, new(["نوع الاشتراك", "كام طالب دفع؟", "كام شراء؟", "دفعوا كام؟", "عمولتنا", "حساب المستر"], [22,13,13,18,16,18], rows, LastRowIsTotal: true)));
            var operations = purchases.Concat(cancellations).ToArray();
            var ids = operations.Select(x => x.GrantId).OfType<Guid>().ToHashSet();
            var refunded = report.Refunds.Where(x => x.GrantId.HasValue && ids.Contains(x.GrantId.Value)
                || operations.Any(p => p.OperationId == x.SourceId)).Sum(x => x.Amount);
            column.Item().Padding(8).Row(row =>
            {
                row.Spacing(6);
                foreach (var stat in new[] {
                    (Value: Money(refunded), Label: "اترد للطلاب"),
                    (Value: cancellations.Length.ToString(), Label: "شراء اتلغى"),
                    (Value: gifts.Count(x => x.Status == "موجود").ToString(), Label: "اشتراكات هدية"),
                    (Value: purchases.Count(x => x.Counted && x.Paid == 0).ToString(), Label: "شراء مجاني موجود") })
                    row.RelativeItem().Background(Pale).Padding(6).AlignCenter().Column(box =>
                    {
                        box.Item().AlignCenter().Text(stat.Value).Bold().FontSize(13);
                        box.Item().AlignCenter().Text(stat.Label).FontSize(8);
                    });
            });
        });
    }

    private static void Summary(IDocumentContainer document, TeacherDetailedReport report) => document.Page(page =>
    {
        Configure(page, report); page.Size(PageSizes.A4);
        page.Header().Element(x => Brand(x, report, "ملخص الحساب"));
        page.Content().PaddingTop(16).Column(column =>
        {
            column.Spacing(14); var summary = report.Summary;
            column.Item().Background(Navy).Padding(16).Row(hero =>
            {
                hero.RelativeItem().AlignMiddle().Text(summary.Closing < 0 ? "عليه للمنصة لحد نهاية الفترة" : "له عندنا لحد نهاية الفترة").FontColor("#FFFFFF").FontSize(14);
                hero.RelativeItem().ContentFromLeftToRight().Column(value =>
                {
                    value.Item().Text(Money(Math.Abs(summary.Closing))).Bold().FontColor(Gold).FontSize(30);
                    value.Item().Text("جنيه").FontColor(Gold);
                });
            });
            column.Item().Text("الحسبة واحدة واحدة").FontColor(Teal).Bold().FontSize(14);
            var paid = report.Purchases.Where(x => x.Counted && x.Paid > 0).ToArray();
            List<string[]> rows = [
                ["رصيد قبل بداية الفترة", Money(summary.Opening)],
                ["الطلاب دفعوا في الاشتراكات الموجودة", Money(paid.Sum(x => x.Paid))],
                ["عمولتنا حسب الاتفاق في الفترة", Money(summary.Platform)],
                ["نصيب المستر في الفترة بعد المرتجعات", Money(summary.Earned)],
                ["نصيبه اللي قبضه من أكواد مدفوعة", Money(summary.Retained)],
                ["سداد مسجل له في الفترة", Money(summary.Paid)],
                ["تسويات ومديونيات تخص الفترة", Money(summary.Adjustments)],
                ["الصافي لحد نهاية الفترة", Money(summary.Closing)] ];
            column.Item().Element(x => Table(x, new(["البيان", "جنيه"], [75,25], rows, LastRowIsTotal: true)));
            column.Item().Text($"في الفترة {Count(paid.Select(x => x.StudentId))} طالب مختلف دفعوا في {paid.Length} عملية شراء موجودة.").FontSize(10);
            if (summary.Earned != paid.Sum(x => x.Teacher))
                column.Item().Text($"نصيب المستر يشمل كمان {Money(summary.Earned - paid.Sum(x => x.Teacher))} جنيه من باقي الحركات والمرتجعات المسجلة للفترة.").FontSize(9);
            foreach (var note in report.Notes) column.Item().Background("#FBF6E9").Padding(10).Text(note).FontSize(9);
        });
    });

    private sealed record ReportTable(string[] Headers, float[] Widths, IReadOnlyList<string[]> Rows, bool LastRowIsTotal = false);
    private static void Table(IContainer container, ReportTable contents) => container.Table(table =>
    {
        table.ColumnsDefinition(columns => { foreach (var width in contents.Widths) columns.RelativeColumn(width); });
        table.Header(header =>
        {
            foreach (var title in contents.Headers) header.Cell().Background(Navy).Padding(6).Text(title).FontColor("#FFFFFF").Bold().FontSize(8);
        });
        var index = 0;
        foreach (var cells in contents.Rows)
        {
            var total = contents.LastRowIsTotal && index == contents.Rows.Count - 1;
            foreach (var cell in cells)
            {
                IContainer entry = table.Cell().Background(total ? "#E5F3F1" : index % 2 == 0 ? "#FFFFFF" : Pale)
                    .BorderBottom(.5f).BorderColor(Line).Padding(6);
                if (decimal.TryParse(cell, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
                    entry = entry.ContentFromLeftToRight().AlignCenter();
                var text = entry.Text(cell).FontSize(8.5f);
                if (total) text.Bold();
            }
            index++;
        }
        if (contents.Rows.Count == 0) table.Cell().ColumnSpan((uint)contents.Headers.Length).Padding(12).Text("مفيش عمليات في الفترة دي.");
    });

    private static void Appendix(IDocumentContainer document, TeacherDetailedReport report, (string Title, string Note, ReportTable Contents) section) =>
        document.Page(page =>
        {
            Configure(page, report); page.Size(PageSizes.A4.Landscape());
            page.Header().Element(x => Brand(x, report, section.Title));
            page.Content().PaddingTop(12).Column(column =>
            {
                column.Spacing(10); column.Item().Text(section.Note).FontSize(9);
                column.Item().Element(x => Table(x, section.Contents));
            });
        });

    private static void Appendices(IDocumentContainer document, TeacherDetailedReport report)
    {
        Appendix(document, report, ("كل عمليات الشراء", $"{report.Purchases.Count} عملية في الفترة. المجاني والملغي والمرفوض ظاهرين منفصلين.", new(
            ["الطالب","الكورس","اشترى إيه؟","البند","وقت الشراء","الحالة","دفع كام؟","عمولتنا","للمستر","اترد للطالب"],
            [17,12,8,18,11,9,7,6,6,6], report.Purchases.Select(x => new[] { x.Student, x.Course, x.Kind, x.Content,
                Date(x.At), x.Status, Money(x.Paid), Money(x.Platform), Money(x.Teacher),
                Money(report.Refunds.Where(r => r.SourceId == x.OperationId || r.GrantId.HasValue && r.GrantId == x.GrantId).Sum(r => r.Amount)) })
                .Append(["إجمالي كل الشراء", "", "", "", "", "", Money(report.Purchases.Sum(x => x.Paid)),
                    Money(report.Purchases.Sum(x => x.Platform)), Money(report.Purchases.Sum(x => x.Teacher)),
                    Money(report.Refunds.Where(r => report.Purchases.Any(x => r.SourceId == x.OperationId
                        || r.GrantId.HasValue && r.GrantId == x.GrantId)).Sum(r => r.Amount))])
                .ToArray(), LastRowIsTotal: true)));
        Appendix(document, report, ("كل الطلاب وأرصدتهم", "الشراء والشحن في الفترة المختارة، وأرصدة الطلاب الخاصة بالمستر لحد نهاية الفترة.", StudentTable(report)));
        Appendix(document, report, ("كل الإلغاءات والرد للطلاب", "الإلغاء مش معناه إن الفلوس اتردت. مبلغ الرد ظاهر من الاسترداد المسجل بس.", CancellationTable(report)));
        Appendix(document, report, ("كل طلبات الشحن المؤكدة", $"{report.Recharges.Count} طلب شحن معتمد. إجماليها {Money(report.Recharges.Sum(x => x.Amount))} جنيه.", new(
            ["الطالب","المبلغ","وقت الاعتماد"], [48,18,34], report.Recharges.Select(x => new[] { x.Student, Money(x.Amount), Date(x.At) })
                .Append(["الإجمالي", Money(report.Recharges.Sum(x => x.Amount)), ""]).ToArray(), LastRowIsTotal: true)));
        var funding = report.Funding.Where(x => !report.Period.From.HasValue
            || x.At >= CairoTime.GetDayRangeUtc(report.Period.From.Value.ToDateTime(TimeOnly.MinValue)).StartUtc).ToArray();
        Appendix(document, report, ("كل إضافات الرصيد عند المستر", "إضافات الرصيد والاستخدام والمتبقي حتى نهاية الفترة. الشحن غير مستحقات المبيعات.", new(
            ["الطالب","الرصيد جه منين؟","اتضاف كام؟","استخدم لحد نهاية الفترة","متبقي لحد نهاية الفترة","وقت إضافة الرصيد"], [28,23,12,13,13,11],
            funding.Select(x => new[] { x.Student, x.Source, Money(x.Added), Money(x.Used), Money(x.Remaining), Date(x.At) })
                .Append(["الإجمالي", "", Money(funding.Sum(x => x.Added)), Money(funding.Sum(x => x.Used)), Money(funding.Sum(x => x.Remaining)), ""])
                .ToArray(), LastRowIsTotal: true)));
        Appendix(document, report, ("اشتراكات الهدايا", "الهدايا مجانية وعمولتها صفر. نفس الطالب ممكن يظهر في الشراء وفي الهدية.", new(
            ["الطالب","الكورس","نوع الاشتراك","البند","وقت الهدية","الحالة"], [25,21,12,22,14,6],
            report.Gifts.Select(x => new[] { x.Student, x.Course, x.Kind, x.Content, Date(x.At), x.Status }).ToArray())));
        if (report.Payments.Count > 0)
            Appendix(document, report, ("السداد المسجل للمستر", "صرف الأرباح المسجل في الفترة المختارة.", new(["المبلغ","وقت السداد","طريقة السداد","المرجع"], [20,25,25,30],
                report.Payments.Select(x => new[] { Money(x.Amount), Date(x.At), x.Method, x.Reference ?? "مش مسجل" })
                    .Append([Money(report.Payments.Sum(x => x.Amount)), "", "الإجمالي", ""]).ToArray(), LastRowIsTotal: true)));
        if (report.Movements.Count > 0)
            Appendix(document, report, ("حركات الحساب الإضافية", "الحركات دي داخلة في ملخص الحساب حسب حالتها. المرتجع المالي غير المبلغ اللي اترد فعليًا للطالب.", new(
                ["وقت الحركة", "البيان", "تأثيرها على حساب المستر", "نصيب المنصة", "الحالة", "المرجع"], [15,30,15,12,12,16],
                report.Movements.Select(x => new[] { Date(x.At), x.Description, Money(x.Teacher), Money(x.Platform), x.Status, x.Reference }).ToArray())));
    }

    private static ReportTable StudentTable(TeacherDetailedReport report)
    {
        var names = report.Purchases.Where(x => x.StudentId.HasValue).Select(x => (Id: x.StudentId!.Value, x.Student))
            .Concat(report.Gifts.Select(x => (Id: x.StudentId, x.Student))).Concat(report.Recharges.Select(x => (Id: x.StudentId, x.Student)))
            .Concat(report.Funding.Select(x => (Id: x.StudentId, x.Student))).Concat(report.Refunds.Select(x => (Id: x.StudentId, x.Student)))
            .DistinctBy(x => x.Id).OrderBy(x => x.Student, StringComparer.Ordinal);
        var rows = names.Select(student =>
        {
            var paid = report.Purchases.Where(x => x.StudentId == student.Id && x.Counted && x.Paid > 0).ToArray();
            var credit = report.Funding.Where(x => x.StudentId == student.Id).ToArray();
            return new[] { student.Student, paid.Length.ToString(), Money(paid.Sum(x => x.Paid)), Money(paid.Sum(x => x.Platform)), Money(paid.Sum(x => x.Teacher)),
                Money(report.Refunds.Where(x => x.StudentId == student.Id).Sum(x => x.Amount)), Money(credit.Where(x => x.Paid == true).Sum(x => x.Remaining)),
                Money(credit.Where(x => x.Paid == false).Sum(x => x.Remaining)), Money(credit.Where(x => x.Paid == null).Sum(x => x.Remaining)),
                Money(report.Recharges.Where(x => x.StudentId == student.Id).Sum(x => x.Amount)) };
        }).ToList();
        var paid = report.Purchases.Where(x => x.Counted && x.Paid > 0).ToArray();
        rows.Add(["الإجمالي", paid.Length.ToString(), Money(paid.Sum(x => x.Paid)), Money(paid.Sum(x => x.Platform)), Money(paid.Sum(x => x.Teacher)),
            Money(report.Refunds.Sum(x => x.Amount)), Money(report.Funding.Where(x => x.Paid == true).Sum(x => x.Remaining)),
            Money(report.Funding.Where(x => x.Paid == false).Sum(x => x.Remaining)), Money(report.Funding.Where(x => x.Paid == null).Sum(x => x.Remaining)),
            Money(report.Recharges.Sum(x => x.Amount))]);
        if (report.Funding.Any(x => x.Paid == null && x.Remaining > 0))
            return new(["الطالب","شراء مدفوع موجود","دفع في الشراء الموجود","عمولتنا","للمستر","اترد للطالب","رصيد مدفوع متبقي","رصيد مجاني متبقي","رصيد محتاج مراجعة","شحن مؤكد"], [23,8,11,8,9,8,9,8,8,8], rows, LastRowIsTotal: true);
        return new(["الطالب","شراء مدفوع موجود","دفع في الشراء الموجود","عمولتنا","للمستر","اترد للطالب","رصيد مدفوع متبقي","رصيد مجاني متبقي","شحن مؤكد"],
            [25,9,12,8,10,9,10,8,9], rows.Select(x => x.Where((_, index) => index != 8).ToArray()).ToArray(), LastRowIsTotal: true);
    }

    private static ReportTable CancellationTable(TeacherDetailedReport report)
    {
        var rows = report.Cancellations.Select(x => new[] { x.Student, x.Course, x.Kind, x.Content, Date(x.CancelledAt!.Value), Money(x.Paid),
            Money(report.Refunds.Where(r => r.SourceId == x.OperationId || r.GrantId.HasValue && r.GrantId == x.GrantId).Sum(r => r.Amount)), x.CancellationReason ?? "مش مسجل" }).ToList();
        rows.AddRange(report.Refunds.Where(r => !report.Cancellations.Any(x => r.SourceId == x.OperationId || r.GrantId.HasValue && r.GrantId == x.GrantId))
            .Select(r => new[] { r.Student, "استرداد منفصل", r.Method, r.Reason, Date(r.At), "-", Money(r.Amount), r.Reason }));
        rows.Add(["الإجمالي", "", "", "", "", Money(report.Cancellations.Sum(x => x.Paid)), Money(report.Refunds.Sum(x => x.Amount)), ""]);
        return new(["الطالب","الكورس","نوع الاشتراك","البند الملغي","وقت الإلغاء / الرد","كان دفع كام؟","اترد كام؟","سبب الإلغاء"], [20,15,10,23,12,7,7,6], rows, LastRowIsTotal: true);
    }
}
