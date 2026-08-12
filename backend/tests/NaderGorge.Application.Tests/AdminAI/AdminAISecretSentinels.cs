namespace NaderGorge.Application.Tests.AdminAI;

public static class AdminAISecretSentinels
{
    public const string PasswordHash = "ADMINAI_CANARY_PASSWORD_HASH_9f6d";
    public const string RefreshToken = "ADMINAI_CANARY_REFRESH_TOKEN_74a1";
    public const string EncryptionKey = "ADMINAI_CANARY_ENCRYPTION_KEY_c3b8";
    public const string ServiceSecret = "ADMINAI_CANARY_SERVICE_SECRET_a7e2";
    public const string SessionFingerprint = "ADMINAI_CANARY_SESSION_FINGERPRINT_2de4";
    public const string VerificationCode = "ADMINAI_CANARY_VERIFICATION_CODE_638195";
    public const string ParentTrackingCode = "ADMINAI_CANARY_PARENT_TRACKING_6bc0";
    public const string PayrollDetail = "ADMINAI_CANARY_PAYROLL_DETAIL_871d";

    public static readonly IReadOnlyList<string> Prohibited =
    [PasswordHash, RefreshToken, EncryptionKey, ServiceSecret, SessionFingerprint, VerificationCode];

    public static readonly IReadOnlyList<string> MinimizedPii =
    [ParentTrackingCode, PayrollDetail];

    public static void AssertAbsent(string surface, string serialized)
    {
        foreach (var sentinel in Prohibited.Concat(MinimizedPii))
            Assert.DoesNotContain(sentinel, serialized, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(surface));
    }
}

public sealed class AdminAISecretSentinelTests
{
    [Fact]
    public void CanaryValues_AreUniqueAcrossAllLeakDetectionSurfaces()
    {
        var values = AdminAISecretSentinels.Prohibited.Concat(AdminAISecretSentinels.MinimizedPii).ToArray();
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("transcript")]
    [InlineData("proposal")]
    [InlineData("audit")]
    [InlineData("log")]
    [InlineData("metric")]
    [InlineData("realtime")]
    [InlineData("export")]
    public void SafeSurfaceFixture_RejectsAnySentinel(string surface)
    {
        AdminAISecretSentinels.AssertAbsent(surface, "{\"safe\":true}");
        Assert.ThrowsAny<Exception>(() => AdminAISecretSentinels.AssertAbsent(surface, AdminAISecretSentinels.PasswordHash));
    }
}
