namespace NaderGorge.Domain.Enums;

public enum GiftTargetType
{
    Package = 0,
    Lesson = 1,
    Video = 2,
    Exam = 3,
    GeneralBalance = 4,
    TeacherBalance = 5
}

public enum GiftIssuanceStatus
{
    Active = 0,
    PartiallySuccessful = 1,
    Completed = 2,
    Expired = 3,
    Revoked = 4
}

public enum GiftRecipientStatus
{
    Granted = 0,
    AlreadyEntitled = 1,
    Failed = 2,
    Active = 3,
    PartiallyUsed = 4,
    Completed = 5,
    Expired = 6,
    Revoked = 7
}

public enum PromotionalBalanceStatus
{
    Active = 0,
    PartiallyUsed = 1,
    Consumed = 2,
    Expired = 3,
    Revoked = 4
}
