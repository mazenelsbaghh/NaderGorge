using System;
using NaderGorge.Application.Services;
using Xunit;

namespace NaderGorge.Application.Tests;

public class SmsParserTests
{
    [Theory]
    [InlineData("تم استقبال مبلغ 150.00 ج.م من 01012345678", 150.00, "01012345678")]
    [InlineData("تم استقبال مبلغ 50 ج.م من 01211112222", 50.00, "01211112222")]
    [InlineData("You have received EGP 150.00 from 01199998888", 150.00, "01199998888")]
    [InlineData("تم استقبال مبلغ 200.50 ج.م من 01533334444", 200.50, "01533334444")]
    [InlineData("تم استقبال مبلغ 75 ج.م من الرقم 01098765432", 75.00, "01098765432")]
    [InlineData("بقيمة 500.00 ج.م من 01200001111", 500.00, "01200001111")]
    [InlineData("100 ج.م من 01033332222", 100.00, "01033332222")]
    [InlineData("تم استقبال 20 ج.م من 01144445555", 20.00, "01144445555")]
    public void Parse_ShouldExtractAmountAndPhoneNumber_WhenValidEgyptianSms(string body, decimal expectedAmount, string expectedPhone)
    {
        // Act
        var result = SmsParser.Parse(body);

        // Assert
        Assert.True(result.IsParsedSuccessfully);
        Assert.Equal(expectedAmount, result.Amount);
        Assert.Equal(expectedPhone, result.SenderPhone);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("رسالة عشوائية لا تحتوي على مبالغ أو أرقام هواتف")]
    [InlineData("تم استقبال مبلغ من 01012345678")]
    [InlineData("تم استقبال مبلغ 150 ج.م")]
    public void Parse_ShouldFail_WhenSmsIsInvalidOrIncomplete(string body)
    {
        // Act
        var result = SmsParser.Parse(body);

        // Assert
        Assert.False(result.IsParsedSuccessfully);
    }
}
