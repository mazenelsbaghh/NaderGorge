namespace NaderGorge.Domain.Common;

public sealed class DuplicatePhoneNumberException : Exception
{
    public DuplicatePhoneNumberException()
        : base("رقم الهاتف مسجل بالفعل في حساب آخر.")
    {
    }
}
