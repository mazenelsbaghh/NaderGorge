using System.Net;
using System.Text.RegularExpressions;

namespace NaderGorge.Application.Features.Admin.Ocr;

public sealed record AssessmentOcrOptionDto(string Text, bool IsCorrect = false);

public sealed record AssessmentOcrQuestionDto(
    string Text,
    string Type,
    decimal Points,
    int Order,
    IReadOnlyList<AssessmentOcrOptionDto> Options,
    decimal Confidence);

public interface IAssessmentOcrService
{
    Task<IReadOnlyList<AssessmentOcrQuestionDto>> ExtractQuestionsAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Converts the plain text returned by Cloud Vision into question drafts.
/// It deliberately does not invent correct answers: the teacher must confirm them
/// in the inline question editor before publishing.
/// </summary>
public static class AssessmentOcrQuestionParser
{
    private static readonly Regex QuestionStart = new(
        @"^\s*(?:سؤال\s*)?([0-9٠-٩]{1,3})\s*[\)\.\-:：]?\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OptionStart = new(
        @"^\s*(?:\(?\s*([A-Da-dأبجدهـ])\s*\)?|\(?\s*([1-4])\s*\))\s*[\.\-:：]?\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<AssessmentOcrQuestionDto> Parse(string? rawText)
    {
        var lines = NormalizeLines(rawText);
        if (lines.Count == 0)
            return [];

        return GroupQuestionLines(lines)
            .Select((group, index) => ParseGroup(group, index + 1))
            .Where(question => !string.IsNullOrWhiteSpace(question.Text))
            .ToList();
    }

    private static List<string> NormalizeLines(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        return rawText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToList();
    }

    private static List<List<string>> GroupQuestionLines(IReadOnlyList<string> lines)
    {
        var groups = new List<List<string>>();
        List<string>? current = null;
        foreach (var line in lines)
        {
            if (QuestionStart.IsMatch(line))
            {
                current = [];
                groups.Add(current);
            }

            (current ??= CreateImplicitGroup(groups)).Add(line);
        }
        return groups;
    }

    private static List<string> CreateImplicitGroup(List<List<string>> groups)
    {
        var group = new List<string>();
        groups.Add(group);
        return group;
    }

    private static AssessmentOcrQuestionDto ParseGroup(IReadOnlyList<string> group, int order)
    {
        var body = new List<string>();
        var options = new List<AssessmentOcrOptionDto>();

        foreach (var line in group)
        {
            var optionMatch = OptionStart.Match(line);
            if (optionMatch.Success)
            {
                var optionText = optionMatch.Groups[3].Value.Trim();
                if (optionText.Length > 0)
                    options.Add(new AssessmentOcrOptionDto(optionText));
                continue;
            }

            var questionMatch = QuestionStart.Match(line);
            body.Add(questionMatch.Success && questionMatch.Groups[2].Value.Length > 0
                ? questionMatch.Groups[2].Value.Trim()
                : line);
        }

        var htmlText = string.Join("<br />", body.Select(WebUtility.HtmlEncode));
        var confidence = options.Count > 0 && body.Count > 0 ? 0.86m : 0.68m;

        return new AssessmentOcrQuestionDto(
            htmlText,
            options.Count > 0 ? "MCQ" : "Essay",
            1,
            order,
            options,
            confidence);
    }
}
