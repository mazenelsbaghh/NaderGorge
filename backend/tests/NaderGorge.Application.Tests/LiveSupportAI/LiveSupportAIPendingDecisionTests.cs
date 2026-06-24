using System.Text;
using FluentValidation.TestHelper;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupportAI.Dtos;
using NaderGorge.Application.Features.LiveSupportAI.Validation;
using NaderGorge.Infrastructure.Services.LiveSupportAI;

namespace NaderGorge.Application.Tests.LiveSupportAI;

public sealed class LiveSupportAIPendingDecisionTests
{
    [Fact]
    public void Protected_payload_is_not_plaintext_and_keyed_digests_are_purpose_bound()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI_CALLBACK_SECRET"] = "Feature146OnlyStrongCallbackSecretValue123456789"
        }).Build();
        var protector = new LiveSupportAIDataProtector(configuration);
        var plaintext = Encoding.UTF8.GetBytes("{\"password\":\"never-store-me\"}");

        var protectedPayload = protector.Protect(plaintext);

        Assert.DoesNotContain("never-store-me", Encoding.UTF8.GetString(protectedPayload), StringComparison.Ordinal);
        Assert.Equal(plaintext, protector.Unprotect(protectedPayload));
        Assert.Equal(protector.ComputeKeyedDigest("action", plaintext), protector.ComputeKeyedDigest("action", plaintext));
        Assert.NotEqual(protector.ComputeKeyedDigest("action", plaintext), protector.ComputeKeyedDigest("verification", plaintext));
    }

    [Fact]
    public void Registration_validator_requires_complete_authoritative_profile_and_distinct_valid_phones()
    {
        var validator = new LiveSupportAISecureRegistrationValidator();
        var invalid = new LiveSupportAISecureRegistrationDto(
            Guid.NewGuid(), "short", "اسم ثنائي", "123", "weak", DateTime.UtcNow.AddDays(1), "Unknown",
            "", "", "Secondary", "ThirdSecondary", "", "123");

        var result = validator.TestValidate(invalid);

        result.ShouldHaveValidationErrorFor(item => item.FullName);
        result.ShouldHaveValidationErrorFor(item => item.PhoneNumber);
        result.ShouldHaveValidationErrorFor(item => item.Password);
        result.ShouldHaveValidationErrorFor(item => item.DateOfBirth);
        result.ShouldHaveValidationErrorFor(item => item.Gender);
        result.ShouldHaveValidationErrorFor(item => item.Address);
        result.ShouldHaveValidationErrorFor(item => item.ParentPhoneNumber);
    }
}
