using NaderGorge.Application.Features.Student.Queries;

namespace NaderGorge.Application.Features.LiveSupport.Dtos;

public sealed record LiveSupportPurchaseCourseDto(Guid PackageId, string Name, string TeacherName, string SubjectName, decimal Price);
public sealed record LiveSupportPurchasePreviewDto(LiveSupportPurchaseCourseDto Course, PurchaseFundingPreviewDto Funding, string QuoteVersion, string ConfirmationVersion);
