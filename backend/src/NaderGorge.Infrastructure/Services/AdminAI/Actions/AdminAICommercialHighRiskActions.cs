using MediatR;
using NaderGorge.Application.Common;
using NaderGorge.Application.Features.Admin.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Commands;
using NaderGorge.Application.Features.Admin.Gifts.Models;
using NaderGorge.Application.Features.Admin.Sales;
using NaderGorge.Application.Features.Admin.SharedPackages;
using NaderGorge.Application.Features.AdminAI.Interfaces;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Infrastructure.Services.AdminAI.Actions;

public sealed record AdminAIBulkGenerateCodesInput(string GroupName, CodeType CodeType, int Count, int CodeLength, Guid? PackageId, Guid? TermId, Guid? ContentSectionId, Guid? LessonId, Guid? ExamId, Guid? PublicExamProductId, Guid? VideoTypeId, bool IncludeFutureVideos, List<Guid>? VideoTargetIds, decimal? BalanceAmount, Guid? TeacherId, decimal? DiscountPercentage, SalesOwnerType? RevenueOwner, TeacherAllocationMode? RevenueAllocationMode, decimal? RevenueAllocationValue, CodeAccountingTiming AccountingTiming, DateTime? ExpiresAt, bool ExpireActivatedAccess);
public sealed record AdminAIRemoveUnusedCodesInput(Guid GroupId, bool KeepEmptyGroup);
public sealed record AdminAIResetCodeProfileInput(Guid PackageId);
public sealed record AdminAIPublishSharedPackageInput(Guid PackageId);
public sealed record AdminAIIssueGiftInput(IssueGiftRequest Request);
public sealed record AdminAIRevokeGiftInput(Guid GiftId, string Reason);
public sealed record AdminAIDisableCouponInput(Guid CouponId, string? Reason);
public sealed record AdminAIDisablePublicExamInput(Guid ProductId, string? Reason);
public sealed record AdminAICreatePrintableBatchInput(PrintableBatchRequest Request);
public sealed record AdminAISaveSalesRuleInput(SalesRuleRequest Request);
public sealed record AdminAICreateCouponInput(SalesCouponRequest Request);
public sealed record AdminAIUpdateCouponInput(Guid CouponId, SalesCouponRequest Request);
public sealed record AdminAISaveStackingPolicyInput(StackingPolicyRequest Request);
public sealed record AdminAISavePublicExamProductInput(PublicExamProductRequest Request);
public sealed record AdminAICreatePublicExamProductInput(CreatePublicExamRequest Request);

public sealed class AdminAIBulkGenerateCodesAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIBulkGenerateCodesInput, ApiResponse<BulkGenerateCodesResponse>>(m, p)
{
    public override string Key => "admin.commercial.codes.bulk-generate";
    protected override IRequest<ApiResponse<BulkGenerateCodesResponse>> CreateCommand(AdminAIBulkGenerateCodesInput i, Guid a, string o) => new BulkGenerateCodesCommand(i.GroupName, i.CodeType, i.Count, i.CodeLength, a, i.PackageId, i.TermId, i.ContentSectionId, i.LessonId, i.ExamId, i.PublicExamProductId, i.VideoTypeId, i.IncludeFutureVideos, i.VideoTargetIds, i.BalanceAmount, i.TeacherId, i.DiscountPercentage, i.RevenueOwner, i.RevenueAllocationMode, i.RevenueAllocationValue, i.AccountingTiming, i.ExpiresAt, i.ExpireActivatedAccess);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<BulkGenerateCodesResponse> r) => r.Success
        ? AdminAIActionOutcomeFactory.Success(new { codeGroupId = r.Data!.CodeGroupId, generated = r.Data.CodesGenerated }, r.Data.CodesGenerated, ["codes", "sales"])
        : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["codes", "sales"]);
}
public sealed class AdminAIRemoveUnusedCodesAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRemoveUnusedCodesInput, ApiResponse<RemoveUnusedCodesResult>>(m, p)
{
    public override string Key => "admin.commercial.codes.remove-unused";
    protected override IRequest<ApiResponse<RemoveUnusedCodesResult>> CreateCommand(AdminAIRemoveUnusedCodesInput i, Guid a, string o) => new RemoveUnusedCodesCommand(i.GroupId, a, i.KeepEmptyGroup);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<RemoveUnusedCodesResult> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { r.Data!.RemovedCount, r.Data.KeptUsedCount, r.Data.GroupDeleted }, r.Data.RemovedCount, ["codes"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["codes"]);
}
public sealed class AdminAIResetCodeProfileAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIResetCodeProfileInput, ApiResponse>(m, p)
{
    public override string Key => "admin.commercial.code-profile.reset";
    protected override IRequest<ApiResponse> CreateCommand(AdminAIResetCodeProfileInput i, Guid a, string o) => new ResetPackageCodeProfileCommand(i.PackageId, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse r) => IdentityOutcome.From(r, ["codes", "packages"]);
}
public sealed class AdminAIPublishSharedPackageAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIPublishSharedPackageInput, SharedPackageCommandResult>(m, p)
{
    public override string Key => "admin.commercial.shared-package.publish";
    protected override IRequest<SharedPackageCommandResult> CreateCommand(AdminAIPublishSharedPackageInput i, Guid a, string o) => new PublishSharedPackageCommand(a, i.PackageId);
    protected override AdminAIActionOutcome ToOutcome(SharedPackageCommandResult r) => r.Status == SharedPackageCommandStatus.Success ? AdminAIActionOutcomeFactory.Success(new { packageId = r.Id }, 1, ["shared-packages", "sales"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.ErrorCode }, ["shared-packages", "sales"]);
}
public sealed class AdminAIIssueGiftAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIIssueGiftInput, ApiResponse<IssueGiftResultDto>>(m, p)
{
    public override string Key => "admin.commercial.gift.issue";
    protected override IRequest<ApiResponse<IssueGiftResultDto>> CreateCommand(AdminAIIssueGiftInput i, Guid a, string o) => new IssueGiftCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<IssueGiftResultDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { giftId = r.Data!.Id, recipientCount = r.Data.Recipients.Count, r.Data.IsReplay }, r.Data.Recipients.Count, ["gifts", "access-grants", "balances"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["gifts", "access-grants", "balances"]);
}
public sealed class AdminAIRevokeGiftAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIRevokeGiftInput, ApiResponse<RevokeGiftResultDto>>(m, p)
{
    public override string Key => "admin.commercial.gift.revoke";
    protected override IRequest<ApiResponse<RevokeGiftResultDto>> CreateCommand(AdminAIRevokeGiftInput i, Guid a, string o) => new RevokeGiftCommand(i.GiftId, i.Reason, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<RevokeGiftResultDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { giftId = r.Data!.Id, r.Data.Changed, r.Data.RevokedAmount }, 1, ["gifts", "access-grants", "balances"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["gifts", "access-grants", "balances"]);
}
public sealed class AdminAIDisableCouponAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDisableCouponInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.commercial.coupon.disable";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDisableCouponInput i, Guid a, string o) => new DisableSalesCouponCommand(i.CouponId, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["sales", "coupons"]);
}
public sealed class AdminAIDisablePublicExamAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIDisablePublicExamInput, ApiResponse<bool>>(m, p)
{
    public override string Key => "admin.commercial.public-exam.disable";
    protected override IRequest<ApiResponse<bool>> CreateCommand(AdminAIDisablePublicExamInput i, Guid a, string o) => new DisablePublicExamProductCommand(i.ProductId, a, i.Reason);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<bool> r) => IdentityOutcome.From(r, ["sales", "public-exams"]);
}
public sealed class AdminAICreatePrintableBatchAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAICreatePrintableBatchInput, ApiResponse<PrintableBatchDto>>(m, p)
{
    public override string Key => "admin.commercial.printable-batch.create";
    protected override IRequest<ApiResponse<PrintableBatchDto>> CreateCommand(AdminAICreatePrintableBatchInput i, Guid a, string o) => new CreatePrintableBatchCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<PrintableBatchDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { batchId = r.Data!.Id }, 1, ["sales", "printable-batches"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "printable-batches"]);
}
public sealed class AdminAISaveSalesRuleAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAISaveSalesRuleInput, ApiResponse<SalesRuleDto>>(m, p)
{
    public override string Key => "admin.commercial.sales-rule.save";
    protected override IRequest<ApiResponse<SalesRuleDto>> CreateCommand(AdminAISaveSalesRuleInput i, Guid a, string o) => new SaveSalesRuleCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<SalesRuleDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { ruleId = r.Data!.Id, r.Data.IsActive }, 1, ["sales", "pricing"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "pricing"]);
}
public sealed class AdminAICreateCouponAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAICreateCouponInput, ApiResponse<SalesCouponDto>>(m, p)
{
    public override string Key => "admin.commercial.coupon.create";
    protected override IRequest<ApiResponse<SalesCouponDto>> CreateCommand(AdminAICreateCouponInput i, Guid a, string o) => new CreateSalesCouponCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<SalesCouponDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { couponId = r.Data!.Id, r.Data.Status }, 1, ["sales", "coupons"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "coupons"]);
}
public sealed class AdminAIUpdateCouponAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAIUpdateCouponInput, ApiResponse<SalesCouponDto>>(m, p)
{
    public override string Key => "admin.commercial.coupon.update";
    protected override IRequest<ApiResponse<SalesCouponDto>> CreateCommand(AdminAIUpdateCouponInput i, Guid a, string o) => new UpdateSalesCouponCommand(i.CouponId, i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<SalesCouponDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { couponId = r.Data!.Id, r.Data.Status }, 1, ["sales", "coupons"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "coupons"]);
}
public sealed class AdminAISaveStackingPolicyAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAISaveStackingPolicyInput, ApiResponse<StackingPolicyDto>>(m, p)
{
    public override string Key => "admin.commercial.discount-policy.save";
    protected override IRequest<ApiResponse<StackingPolicyDto>> CreateCommand(AdminAISaveStackingPolicyInput i, Guid a, string o) => new SaveStackingPolicyCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<StackingPolicyDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { policyId = r.Data!.Id, r.Data.IsActive }, 1, ["sales", "pricing"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "pricing"]);
}
public sealed class AdminAISavePublicExamProductAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAISavePublicExamProductInput, ApiResponse<PublicExamProductDto>>(m, p)
{
    public override string Key => "admin.commercial.public-exam.save";
    protected override IRequest<ApiResponse<PublicExamProductDto>> CreateCommand(AdminAISavePublicExamProductInput i, Guid a, string o) => new SavePublicExamProductCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<PublicExamProductDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { productId = r.Data!.Id, r.Data.IsPublished, r.Data.Price }, 1, ["sales", "public-exams"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "public-exams"]);
}
public sealed class AdminAICreatePublicExamProductAction(IMediator m, IAdminAIActionPreviewSource p) : AdminAIMediatRActionCapability<AdminAICreatePublicExamProductInput, ApiResponse<PublicExamProductDto>>(m, p)
{
    public override string Key => "admin.commercial.public-exam.create";
    protected override IRequest<ApiResponse<PublicExamProductDto>> CreateCommand(AdminAICreatePublicExamProductInput i, Guid a, string o) => new CreatePublicExamProductCommand(i.Request, a);
    protected override AdminAIActionOutcome ToOutcome(ApiResponse<PublicExamProductDto> r) => r.Success ? AdminAIActionOutcomeFactory.Success(new { productId = r.Data!.Id, r.Data.IsPublished, r.Data.Price }, 1, ["sales", "public-exams"]) : AdminAIActionOutcomeFactory.Rejected(new { r.Message, r.Errors }, ["sales", "public-exams"]);
}
