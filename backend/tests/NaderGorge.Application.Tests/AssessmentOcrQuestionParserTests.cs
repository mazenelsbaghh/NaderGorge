using NaderGorge.Application.Features.Admin.Ocr;

namespace NaderGorge.Application.Tests;

public sealed class AssessmentOcrQuestionParserTests
{
    [Fact]
    public void Parses_numbered_questions_and_arabic_options_without_inventing_answers()
    {
        var result = AssessmentOcrQuestionParser.Parse("""
            ١) ما عاصمة مصر؟
            أ) القاهرة
            ب) الإسكندرية
            ٢) اشرح دورة الماء.
            """);

        Assert.Equal(2, result.Count);
        Assert.Equal("MCQ", result[0].Type);
        Assert.Equal(2, result[0].Options.Count);
        Assert.All(result[0].Options, option => Assert.False(option.IsCorrect));
        Assert.Equal("Essay", result[1].Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Empty_vision_text_returns_no_question_drafts(string? rawText)
    {
        Assert.Empty(AssessmentOcrQuestionParser.Parse(rawText));
    }
}
