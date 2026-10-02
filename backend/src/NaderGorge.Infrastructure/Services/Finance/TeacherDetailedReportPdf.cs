using System.Globalization;
using System.Xml.Linq;
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
        // The PDF SVG renderer does not apply CSS classes; preserve the original logo colors as SVG attributes.
        var svg = XDocument.Parse(reader.ReadToEnd());
        var colors = new Dictionary<string, string> { ["st0"] = "none", ["st1"] = "#cb951e", ["st2"] = Navy, ["st3"] = Teal };
        foreach (var shape in svg.Descendants())
            if (shape.Attribute("class") is { } css && colors.TryGetValue(css.Value, out var fill)) shape.SetAttributeValue("fill", fill);
        return svg.ToString(SaveOptions.DisableFormatting);
    });
    private static readonly Lazy<bool> Font = new(() =>
    {
        foreach (var file in new[] { "ReportTahoma.ttf", "ReportTahomaBold.ttf", "ReportArial.ttf", "ReportArialBold.ttf" })
        {
            using var stream = typeof(TeacherDetailedReportPdf).Assembly.GetManifestResourceStream("NaderGorge.Infrastructure.Assets." + file)
                ?? throw new InvalidOperationException("Massar report font is missing: " + file);
            FontManager.RegisterFont(stream);
        }
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

    private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-EG");
    private static string PeriodLabel(TeacherReportPeriod period) =>
        $"من {(period.From.HasValue ? period.From.Value.ToString("d MMMM yyyy", Arabic) : "البداية")} لحد {period.To.ToString("d MMMM yyyy", Arabic)}";
    private static string CutoffLabel(TeacherReportPeriod period) => period.To.ToString("d MMMM", Arabic);

    private static void Configure(PageDescriptor page, TeacherDetailedReport report, bool appendix = false)
    {
        page.MarginTop(appendix ? 34 : 39.7f);
        page.MarginHorizontal(appendix ? 34 : 39.7f);
        page.MarginBottom(16.5f);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(x => x.FontFamily("Tahoma").FontSize(9).LineHeight(1.7f).FontColor("#20354a"));
        page.Footer().BorderTop(.5f).BorderColor(Line).PaddingTop(3.75f).Row(row =>
        {
            row.ConstantItem(35).ContentFromLeftToRight().Text(text =>
            {
                text.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(7).FontColor("#5b7085"));
                text.CurrentPageNumber(); text.Span(" / "); text.TotalPages();
            });
            row.RelativeItem().AlignCenter().Text($"{report.TeacherName} · التقفيل حتى {report.Period.To.ToString("d MMMM yyyy", Arabic)}").FontSize(6.75f);
            row.ConstantItem(90).ContentFromLeftToRight().Text("Massar Academy").FontFamily("Arial").FontSize(6.75f);
        });
    }

    private static void Brand(IContainer container, TeacherDetailedReport report, string title, string part, bool appendix = false)
    {
        container.BorderBottom(appendix ? 1.5f : 2.25f).BorderColor(Teal).PaddingBottom(appendix ? 3.75f : 8.25f).Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().PaddingBottom(3.75f).Text("منصة مسار").FontColor(Teal).FontSize(9).LineHeight(1.2f);
                column.Item().Text(title).Bold().FontColor(Navy).FontSize(appendix ? 15 : 19.5f).LineHeight(1.5f);
                column.Item().PaddingTop(4.5f).Text($"حساب {report.TeacherName} {PeriodLabel(report.Period)}").FontColor("#5b7085").FontSize(9).LineHeight(1.2f);
                column.Item().PaddingTop(4.5f).Text(part).FontColor("#5b7085").FontSize(9).LineHeight(1.2f);
            });
            row.ConstantItem(appendix ? 57 : 81).Height(appendix ? 43.5f : 61.5f).Svg(Logo.Value);
        });
    }

    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);
    private static string Date(DateTime at) => CairoTime.ToLocal(at).ToString("dd/MM/yyyy, HH:mm", CultureInfo.InvariantCulture);
    private static string CourseLabel(string name) => name.Replace("باقه ال 3 شهور", "3 شهور").Replace("باقه الترم الاول", "الترم الأول").Replace(" ( ", " (").Replace(" )", ")");
    private static string Count(IEnumerable<Guid?> ids) => ids.Where(x => x.HasValue).Distinct().Count().ToString(CultureInfo.InvariantCulture);

    private static void Courses(IDocumentContainer document, TeacherDetailedReport report)
    {
        var courseIds = report.Purchases.Select(x => x.CourseId).Concat(report.Gifts.Select(x => x.CourseId))
            .Concat(report.Cancellations.Select(x => x.CourseId)).Distinct()
            .OrderBy(id => report.Purchases.FirstOrDefault(x => x.CourseId == id)?.Course
                ?? report.Gifts.FirstOrDefault(x => x.CourseId == id)?.Course
                ?? report.Cancellations.First(x => x.CourseId == id).Course, StringComparer.Ordinal).ToArray();
        var chunks = courseIds.Length == 0 ? new[] { Array.Empty<Guid>() } : courseIds.Chunk(2).ToArray();
        for (var index = 0; index < chunks.Length; index++)
        {
            var chunk = chunks[index]; var first = index == 0; var last = index == chunks.Length - 1;
            document.Page(page =>
            {
                Configure(page, report); page.Size(PageSizes.A4);
                page.Header().Element(x => Brand(x, report, first ? $"تفاصيل حساب {report.TeacherName}" : "باقي تفاصيل الكورسات", "تفاصيل الكورسات"));
                page.Content().PaddingTop(12).PaddingBottom(30).Column(column =>
                {
                    column.Item().PaddingBottom(11.25f).Text(first
                        ? "الشراء حسب الاتفاق وقت الشراء، والكروت للاتفاق الحالي. المجاني والهدايا من غير عمولة. الأرقام بالجنيه."
                        : "كل شراء بيتحسب حسب الاتفاق الخاص بيه. عدد الشراء ممكن يزيد عن عدد الطلاب لأن الطالب ممكن يشتري أكتر من مرة.").FontSize(9).LineHeight(1.85f);
                    if (first && report.CurrentAgreements.Count > 0) column.Item().PaddingBottom(12.75f).Element(x => Rules(x, report));
                    if (chunk.Length > 0)
                        column.Item().PaddingBottom(4.5f).Text($"الكورسات {index * 2 + 1}" + (chunk.Length > 1 ? $" و{index * 2 + 2}" : "") + $" من {courseIds.Length}").Bold().FontColor(Teal).FontSize(8.25f);
                    foreach (var id in chunk) column.Item().PaddingBottom(13.5f).PreventPageBreak().Element(x => Course(x, report, id));
                    if (chunk.Length == 0) column.Item().Text("مفيش مشتريات أو هدايا في الفترة دي.");
                    if (last && !first)
                    {
                        var paid = report.Purchases.Where(x => x.Counted && x.Paid > 0).ToArray();
                        column.Item().Background("#fbf6e9").CornerRadius(3.75f).BorderRight(2.25f).BorderColor("#cb951e").Padding(9)
                            .Text($"في الكورسات {Count(paid.Select(x => x.StudentId))} طالب مختلف دفعوا في {paid.Length} عملية شراء موجودة. إجمالي المدفوع {Money(paid.Sum(x => x.Paid))} جنيه. نصيب المستر {Money(paid.Sum(x => x.Teacher))} وعمولتنا {Money(paid.Sum(x => x.Platform))}.").FontSize(8.25f).LineHeight(1.85f);
                    }
                    column.Item().PaddingTop(7.5f).Text("عدد الطلاب في إجمالي الكورس بيحسب كل طالب مرة واحدة، حتى لو اشترى أكتر من نوع. المجاني والهدايا ظاهرين منفصلين.").FontColor("#5b7085").FontSize(7.5f).LineHeight(1.9f);
                });
            });
        }
    }

    private static void Rules(IContainer container, TeacherDetailedReport report)
    {
        var current = report.CurrentAgreements;
        var primary = current.Where(agreement => new[] { "الحصة:", "الشهر:", "الترم / الكورس:", "السنة / الباقة:" }
            .Any(scope => agreement.StartsWith(scope, StringComparison.Ordinal))).ToArray();
        var candidates = primary.Length > 0 ? primary : current;
        var agreements = candidates.GroupBy(agreement => agreement.Split(":", 2)[0])
            .SelectMany(group =>
            {
                // The service orders agreements by effective start. A later general
                // agreement overrides an older open-ended agreement for the same scope.
                var general = group.Where(agreement => !agreement.Contains("(اتفاق لمحتوى محدد)", StringComparison.Ordinal)).LastOrDefault();
                var scoped = group.Where(agreement => agreement.Contains("(اتفاق لمحتوى محدد)", StringComparison.Ordinal));
                return general is null ? scoped : scoped.Prepend(general);
            });
        var rules = agreements.Select(text =>
        {
            var parts = text.Split(":", 2); var detail = parts.Length == 2 ? parts[1].Split(" · ")[0].Trim() : text;
            var platform = detail.Contains(" لينا", StringComparison.Ordinal);
            var value = platform ? detail.Split(" لينا")[0] : detail;
            return (Scope: parts[0], Value: value, Platform: platform);
        }).Distinct().GroupBy(rule => rule.Scope).Select(group => group.Count() == 1 ? group.First()
            : (Scope: group.Key, Value: "حسب المحتوى", Platform: group.All(rule => rule.Platform))).ToList();
        var term = rules.FindIndex(x => x.Scope == "الترم / الكورس");
        var year = rules.FindIndex(x => x.Scope == "السنة / الباقة");
        if (term >= 0 && year >= 0 && rules[term].Value == rules[year].Value && rules[term].Platform == rules[year].Platform)
        {
            rules[term] = ("الترم والسنة", rules[term].Value, rules[term].Platform); rules.RemoveAt(year);
        }
        rules = rules.OrderBy(x => x.Scope switch { "الحصة" => 0, "الشهر" => 1, "الترم والسنة" => 2, _ => 3 }).ToList();
        container.Column(column =>
        {
            column.Spacing(6);
            foreach (var chunk in rules.Chunk(3)) column.Item().Row(row =>
            {
                row.Spacing(6);
                foreach (var rule in chunk)
                    row.RelativeItem().Background("#f2f8f8").Border(.75f).BorderColor("#d9eeed").CornerRadius(4.5f).Padding(6.75f).Column(card =>
                    {
                        card.Item().AlignCenter().Text((rule.Platform ? "عمولتنا الحالية في " : "نصيب المستر الحالي في ") + rule.Scope).FontSize(8.25f).LineHeight(1.2f);
                        card.Item().PaddingTop(3).AlignCenter().Text(rule.Value).Bold().FontColor(Teal).FontSize(12).LineHeight(1.2f);
                    });
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
        container.Border(.75f).BorderColor(Line).CornerRadius(7.5f).Column(column =>
        {
            var people = Count(purchases.Where(x => x.Counted).Select(x => x.StudentId)
                .Concat(gifts.Where(x => x.Status == "موجود").Select(x => (Guid?)x.StudentId)));
            column.Item().Background(Pale).CornerRadiusTopLeft(7.5f).CornerRadiusTopRight(7.5f).BorderRight(3).BorderColor(Teal).PaddingVertical(9).PaddingHorizontal(10.5f).Row(row =>
            {
                row.RelativeItem().Text(name).Bold().FontColor(Navy).FontSize(11.25f).LineHeight(1.6f);
                row.ConstantItem(105).AlignMiddle().AlignLeft().Text($"{people} طالب مختلف عنده").FontColor(Teal).FontSize(8.25f);
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
            column.Item().BorderTop(.75f).BorderColor(Line).PaddingVertical(8.25f).PaddingHorizontal(9).Row(row =>
            {
                row.Spacing(4.5f);
                foreach (var stat in new[] {
                    (Value: Money(refunded), Label: "اترد لرصيد الطلاب"),
                    (Value: cancellations.Length.ToString(), Label: "شراء اتلغى"),
                    (Value: gifts.Count(x => x.Status == "موجود").ToString(), Label: "اشتراكات هدية"),
                    (Value: purchases.Count(x => x.Counted && x.Paid == 0).ToString(), Label: "شراء مجاني موجود") })
                    row.RelativeItem().Background(Pale).CornerRadius(3.75f).PaddingVertical(6).PaddingHorizontal(3).AlignCenter().Column(box =>
                    {
                        box.Item().AlignCenter().Text(stat.Value).FontFamily("Arial").Bold().FontColor(Navy).FontSize(6.75f).LineHeight(1.4f);
                        box.Item().PaddingTop(3).AlignCenter().Text(stat.Label).FontSize(6.75f).LineHeight(1.4f);
                    });
            });
        });
    }

    private static void Summary(IDocumentContainer document, TeacherDetailedReport report) => document.Page(page =>
    {
        Configure(page, report); page.Size(PageSizes.A4);
        page.Header().Element(x => Brand(x, report, "ملخص الحساب", "الحساب تحت تفاصيل الكورسات"));
        page.Content().PaddingTop(12).PaddingBottom(30).Column(column =>
        {
            var summary = report.Summary; var end = CutoffLabel(report.Period);
            column.Item().Background(Navy).CornerRadius(7.5f).PaddingVertical(12.75f).PaddingHorizontal(15).Row(hero =>
            {
                hero.RelativeItem().AlignMiddle().Column(caption =>
                {
                    caption.Item().Text((summary.Closing < 0 ? "عليه للمنصة لحد " : "له عندنا لحد ") + end).FontColor("#FFFFFF").FontSize(11.25f).LineHeight(1.2f);
                    caption.Item().PaddingTop(5.25f).Text("بعد استبعاد الإلغاء والمجاني، وحسب السداد المسجل.").FontColor("#d6e5ed").FontSize(8.25f);
                });
                hero.RelativeItem().ContentFromLeftToRight().Column(value =>
                {
                    value.Item().Text(Money(Math.Abs(summary.Closing))).FontFamily("Arial").Bold().FontColor(Gold).FontSize(24.75f).LineHeight(1.2f);
                    value.Item().PaddingTop(3.75f).Text("جنيه").Bold().FontColor("#d6e5ed").FontSize(9).LineHeight(1.2f);
                });
            });
            column.Item().PaddingTop(12.75f).PaddingBottom(6).Text("الحسبة واحدة واحدة").FontColor(Teal).Bold().FontSize(9.75f);
            var paid = report.Purchases.Where(x => x.Counted && x.Paid > 0).ToArray();
            List<string[]> rows = [];
            if (report.Period.From.HasValue) rows.Add(["رصيد قبل بداية الفترة", Money(summary.Opening)]);
            rows.AddRange([
                ["الطلاب دفعوا في الاشتراكات الموجودة", Money(paid.Sum(x => x.Paid))],
                ["عمولتنا حسب الاتفاق", Money(summary.Platform)],
                [$"نصيب {report.TeacherName}", Money(summary.Earned)],
                ["نصيبه اللي قبضه من أكواد مدفوعة", Money(summary.Retained)],
                [$"سداد مسجل له حتى {end}", Money(summary.Paid)],
                [summary.Adjustments <= 0 ? "مبالغ عليه تخص الفترة" : "تسويات تخص الفترة", Money(Math.Abs(summary.Adjustments))],
                [summary.Closing < 0 ? $"الصافي اللي عليه للمنصة حتى {end}" : $"الصافي اللي له عندنا حتى {end}", Money(Math.Abs(summary.Closing))] ]);
            column.Item().Element(x => Table(x, new([], [68,32], rows, LastRowIsTotal: true, Account: true)));
            foreach (var note in report.Notes)
                column.Item().PaddingTop(9.75f).Background("#fbf6e9").CornerRadius(3.75f).Padding(9).Text(note).FontSize(8.25f);
        });
    });

    private sealed record ReportTable(string[] Headers, float[] Widths, IReadOnlyList<string[]> Rows, bool LastRowIsTotal = false, bool Account = false, bool Appendix = false);
    private static void Table(IContainer container, ReportTable contents) => container.Table(table =>
    {
        table.ColumnsDefinition(columns => { foreach (var width in contents.Widths) columns.RelativeColumn(width); });
        table.Header(header =>
        {
            foreach (var title in contents.Headers) header.Cell().Background(Navy).PaddingVertical(contents.Appendix ? 3.75f : 6.75f).PaddingHorizontal(5.25f)
                .AlignCenter().Text(title).FontColor("#FFFFFF").Bold().FontSize(contents.Appendix ? 8.25f : 7.5f).LineHeight(contents.Appendix ? 1.5f : 1.65f);
        });
        var index = 0;
        foreach (var cells in contents.Rows)
        {
            var total = contents.LastRowIsTotal && index == contents.Rows.Count - 1 || contents.Account && cells[0].StartsWith("نصيب ", StringComparison.Ordinal);
            foreach (var cell in cells)
            {
                IContainer entry = table.Cell().Background(total ? "#E5F3F1" : index % 2 == 0 ? "#FFFFFF" : "#f5f8fa")
                    .BorderBottom(.75f).BorderColor(Line).PaddingVertical(contents.Account ? 6 : contents.Appendix ? 3.75f : 7.35f).PaddingHorizontal(contents.Account ? 8.25f : 5.25f).AlignMiddle();
                var numeric = decimal.TryParse(cell, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
                var timestamp = DateTime.TryParseExact(cell, "dd/MM/yyyy, HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
                if (numeric || timestamp)
                    entry = entry.ContentFromLeftToRight().DefaultTextStyle(x => x.FontFamily("Arial"));
                if (!contents.Account && numeric) entry = entry.AlignCenter();
                var text = entry.Text(cell).FontSize(contents.Account || contents.Appendix ? 8.25f : 7.5f).LineHeight(contents.Appendix ? 1.5f : 1.65f);
                if (total || contents.Account && cell != cells[0]) text.Bold().FontColor(Navy);
            }
            index++;
        }
        if (contents.Rows.Count == 0) table.Cell().ColumnSpan((uint)contents.Headers.Length).Padding(12).Text("مفيش عمليات في الفترة دي.");
    });

    private static void Appendix(IDocumentContainer document, TeacherDetailedReport report, (string Title, string Note, ReportTable Contents) section) =>
        document.Page(page =>
        {
            Configure(page, report, appendix: true); page.Size(PageSizes.A4.Landscape());
            page.Content().PaddingBottom(28).Column(column =>
            {
                column.Item().PaddingBottom(6).Element(x => Brand(x, report, section.Title, "التفاصيل الكاملة", appendix: true));
                column.Item().PaddingBottom(6).Text(section.Note).FontSize(7.5f);
                column.Item().Element(x => Table(x, section.Contents with { Appendix = true }));
            });
        });

    private static void Appendices(IDocumentContainer document, TeacherDetailedReport report)
    {
        Appendix(document, report, ("كل عمليات الشراء", $"{report.Purchases.Count} عملية في الفترة. المجاني والملغي والمرفوض ظاهرين منفصلين.", new(
            ["الطالب","الكورس","اشترى إيه؟","البند","وقت الشراء","الحالة","دفع كام؟","عمولتنا","للمستر","اترد للطالب"],
            [17,12,8,18,11,9,7,6,6,6], report.Purchases.Select(x => new[] { x.Student, CourseLabel(x.Course), x.Kind, x.Content,
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
            report.Gifts.Select(x => new[] { x.Student, CourseLabel(x.Course), x.Kind, x.Content, Date(x.At), x.Status }).ToArray())));
        if (report.Agreements.Count > 0)
            Appendix(document, report, ("تفاصيل اتفاقات المستر", "الكروت في أول الكشف بتعرض الاتفاقات السارية في نهاية المدة. هنا كل الاتفاقات وتواريخها؛ الحسبة حسب الاتفاق المسجل لكل عملية.", new(
                ["نوع الاشتراك", "الاتفاق ومدة سريانه"], [22,78], report.Agreements.Select(agreement =>
                {
                    var parts = agreement.Split(":", 2);
                    return new[] { parts[0], parts.Length == 2 ? parts[1].Trim() : agreement };
                }).ToArray())));
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
        var rows = report.Cancellations.Select(x => new[] { x.Student, CourseLabel(x.Course), x.Kind, x.Content, Date(x.CancelledAt!.Value), Money(x.Paid),
            Money(report.Refunds.Where(r => r.SourceId == x.OperationId || r.GrantId.HasValue && r.GrantId == x.GrantId).Sum(r => r.Amount)), x.CancellationReason ?? "مش مسجل" }).ToList();
        rows.AddRange(report.Refunds.Where(r => !report.Cancellations.Any(x => r.SourceId == x.OperationId || r.GrantId.HasValue && r.GrantId == x.GrantId))
            .Select(r => new[] { r.Student, "استرداد منفصل", r.Method, r.Reason, Date(r.At), "-", Money(r.Amount), r.Reason }));
        rows.Add(["الإجمالي", "", "", "", "", Money(report.Cancellations.Sum(x => x.Paid)), Money(report.Refunds.Sum(x => x.Amount)), ""]);
        return new(["الطالب","الكورس","نوع الاشتراك","البند الملغي","وقت الإلغاء / الرد","كان دفع كام؟","اترد كام؟","سبب الإلغاء"], [20,15,10,23,12,7,7,6], rows, LastRowIsTotal: true);
    }
}
