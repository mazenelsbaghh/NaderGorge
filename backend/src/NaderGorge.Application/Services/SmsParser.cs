using System;
using System.Text.RegularExpressions;

namespace NaderGorge.Application.Services;

public class SmsParserResult
{
    public decimal? Amount { get; set; }
    public string? SenderPhone { get; set; }
    public bool IsParsedSuccessfully => Amount.HasValue && !string.IsNullOrWhiteSpace(SenderPhone);
}

public static class SmsParser
{
    private static readonly Regex PhoneRegex = new(@"\b01[0125]\d{8}\b", RegexOptions.Compiled);
    
    // Patterns to match transfer amounts
    private static readonly Regex[] AmountRegexes = new[]
    {
        // تم استقبال مبلغ 150.00 ج.م
        new Regex(@"(?:تم استقبال مبلغ|تم استقبال|مبلغ)\s*(\d+(?:\.\d+)?)\s*(?:ج\.م|EGP)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // received EGP 150.00
        new Regex(@"(?:received|amount|value of)\s*(?:EGP\s*)?(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // بقيمة 150.00 ج.م
        new Regex(@"(?:بقيمة)\s*(\d+(?:\.\d+)?)\s*(?:ج\.م|EGP)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // 150.00 ج.م من
        new Regex(@"(\d+(?:\.\d+)?)\s*(?:ج\.م|EGP)\s*من", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    public static SmsParserResult Parse(string body)
    {
        var result = new SmsParserResult();

        if (string.IsNullOrWhiteSpace(body))
            return result;

        // 1. Parse Phone Number (look for 11-digit Egyptian mobile number)
        var phoneMatch = PhoneRegex.Match(body);
        if (phoneMatch.Success)
        {
            result.SenderPhone = phoneMatch.Value;
        }

        // 2. Parse Amount
        foreach (var regex in AmountRegexes)
        {
            var match = regex.Match(body);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var amount))
            {
                result.Amount = amount;
                break;
            }
        }

        // Fallback: If no structured amount pattern matches, look for any decimal number followed by EGP or ج.م
        if (!result.Amount.HasValue)
        {
            var genericAmountRegex = new Regex(@"(\d+(?:\.\d+)?)\s*(?:ج\.م|EGP)", RegexOptions.IgnoreCase);
            var match = genericAmountRegex.Match(body);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var amount))
            {
                result.Amount = amount;
            }
        }

        return result;
    }
}
