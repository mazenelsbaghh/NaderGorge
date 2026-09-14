namespace NaderGorge.Application.Common;

public static class EgyptMobilePhone
{
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        phone = phone.Trim();
        if (phone.Length > 32 || phone.Count(character => character == '+') > 1 ||
            phone.Contains('+') && phone[0] != '+' ||
            phone.Any(character => !(character is >= '0' and <= '9') &&
                character is not ('+' or '-' or '(' or ')') && !char.IsWhiteSpace(character))) return null;
        var digits = new string(phone.Where(character => character is >= '0' and <= '9').ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal)) digits = digits[2..];
        if (digits.Length == 11 && digits[0] == '0' && digits[1] == '1' && "0125".Contains(digits[2]))
            digits = $"20{digits[1..]}";
        return digits.Length == 12 && digits.StartsWith("201", StringComparison.Ordinal) &&
            "0125".Contains(digits[3]) ? digits : null;
    }
}
