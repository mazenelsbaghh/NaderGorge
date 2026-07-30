namespace NaderGorge.Domain.Enums;

public enum TeacherFinancialSourceType
{
    AccessCodeActivation = 0,
    DirectPurchase = 1,
    PublicExamPurchase = 2,
    SharedPackagePurchase = 3,
    Refund = 4,
    Cancellation = 5,
    ManualCompensation = 6,
    ManualAdjustment = 7,
    AccessCodeGeneration = 8
}

public enum TeacherFinancialReviewStatus
{
    AutoApproved = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Reversed = 4
}

public enum TeacherFinancialPayoutStatus
{
    NotEligible = 0,
    Unpaid = 1,
    Reserved = 2,
    Paid = 3,
    Reversed = 4,
    Debt = 5
}

public enum TeacherAllocationMode
{
    CommissionRate = 0,
    Percentage = 1,
    FixedAmount = 2,
    ManualCompensation = 3,
    Reversal = 4
}

public enum TeacherAgreementScopeType
{
    Default = 0,
    Package = 1,
    Term = 2,
    ContentSection = 3,
    Lesson = 4,
    LessonVideo = 5,
    PublicExam = 6,
    SharedPackage = 7,
    CodeGroup = 8
}

public enum TeacherAgreementTrigger
{
    ContentSale = 0,
    CodeDelivery = 1,
    CodeActivation = 2
}

public enum TeacherAgreementAllocationMode
{
    Percentage = 0,
    FixedPerSale = 1,
    FixedPerCode = 2,
    FixedPerBatch = 3
}

public enum TeacherPriceBasis
{
    Gross = 0,
    NetAfterDiscount = 1
}

public enum TeacherDiscountBearer
{
    Platform = 0,
    Teacher = 1,
    Split = 2
}

public enum TeacherSettlementStatus
{
    Draft = 0,
    Reviewed = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4
}

public enum TeacherReversalDisposition
{
    ReverseAvailableBalance = 0,
    TeacherDebt = 1,
    NextSettlementDeduction = 2
}

public enum FinancialInvoiceStatus
{
    Draft = 0,
    Reviewed = 1,
    Approved = 2,
    Paid = 3,
    Cancelled = 4
}

public enum FinancialInvoiceType
{
    TeacherSettlement = 0,
    ProductionExpense = 1,
    BunnyExpense = 2,
    GeneralExpense = 3
}

public enum SharedPackageDistributionMode
{
    Percentage = 0,
    FixedAmount = 1,
    Mixed = 2
}

public enum TeacherPayoutAdjustmentStatus
{
    Open = 0,
    Applied = 1,
    Voided = 2
}
