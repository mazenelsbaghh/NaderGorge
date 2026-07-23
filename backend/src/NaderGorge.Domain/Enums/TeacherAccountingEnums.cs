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
