namespace NaderGorge.Domain.Enums;

public enum SalesTargetType
{
    Package = 0,
    Term = 1,
    ContentSection = 2,
    Lesson = 3,
    SpecificVideo = 4,
    VideoType = 5,
    PublicExam = 6,
    Teacher = 7,
    Platform = 8
}

public enum DiscountType
{
    Percentage = 0,
    FixedAmount = 1
}

public enum SalesOwnerType
{
    Platform = 0,
    Teacher = 1
}

public enum SalesStatus
{
    Draft = 0,
    Active = 1,
    Disabled = 2,
    Expired = 3,
    Archived = 4,
    Consumed = 5
}

public enum StackingMode
{
    SingleOnly = 0,
    AllowCouponAndPrintedCode = 1,
    AllowMultipleWithCap = 2
}

public enum PrintableCodeBehavior
{
    Discount = 0,
    DirectAccess = 1,
    PromotionalCredit = 2
}
