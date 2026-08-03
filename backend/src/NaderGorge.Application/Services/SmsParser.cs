using System;
using System.Text.RegularExpressions;

namespace NaderGorge.Application.Services;

public class SmsParserResult
{
    public decimal? Amount { get; set; }
    public string? SenderPhone { get; set; }
    public decimal? CurrentBalance { get; set; }
    public bool IsParsedSuccessfully => Amount.HasValue && !string.IsNullOrWhiteSpace(SenderPhone);
}

public static class SmsParser
{
    private static readonly Regex PhoneRegex = new(@"\b01[0125]\d{8}\b", RegexOptions.Compiled);
    private static readonly Regex IncomingTransferMarker = new(
        @"(?:تم\s+(?:استلام|استقبال)|استلمت|تحويل\s+(?:إلى|الي)\s+محفظتك|received|transfer\s+received|cash\s+in)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex OutgoingTransferMarker = new(
        @"(?:قمت\s+بتحويل|تم\s+خصم|تم\s+إرسال|you\s+(?:sent|transferred)|cash\s+out)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Patterns to match transfer amounts
    private static readonly Regex[] AmountRegexes = new[]
    {
        // تم استقبال مبلغ 150.00 ج.م
        new Regex(@"(?:تم استقبال مبلغ|تم استقبال|تم استلام مبلغ|تم استلام|مبلغ)\s*(\d+(?:\.\d+)?)\s*(?:ج\.م|جنيه|EGP)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // received EGP 150.00
        new Regex(@"(?:received|amount|value of)\s*(?:EGP\s*)?(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // بقيمة 150.00 ج.م
        new Regex(@"(?:بقيمة)\s*(\d+(?:\.\d+)?)\s*(?:ج\.م|جنيه|EGP)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        // 150.00 ج.م من
        new Regex(@"(\d+(?:\.\d+)?)\s*(?:ج\.م|جنيه|EGP)\s*من", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    private static readonly Regex[] CurrentBalanceRegexes = new[]
    {
        new Regex(@"رصيدك\s+الحالي\s*[:：]?\s*(\d+(?:\.\d+)?)\s*(?:ج\.م|جنيه|EGP)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"(?:current\s+balance|balance)\s*[:：]?\s*(?:EGP\s*)?(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
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
            var genericAmountRegex = new Regex(@"(\d+(?:\.\d+)?)\s*(?:ج\.م|جنيه|EGP)", RegexOptions.IgnoreCase);
            var match = genericAmountRegex.Match(body);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var amount))
            {
                result.Amount = amount;
            }
        }

        foreach (var regex in CurrentBalanceRegexes)
        {
            var match = regex.Match(body);
            if (match.Success && decimal.TryParse(match.Groups[1].Value, out var currentBalance))
            {
                result.CurrentBalance = currentBalance;
                break;
            }
        }

        return result;
    }

    public static bool IsIncomingTransfer(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || OutgoingTransferMarker.IsMatch(body))
            return false;

        return IncomingTransferMarker.IsMatch(body) && Parse(body).Amount.HasValue;
    }
}
