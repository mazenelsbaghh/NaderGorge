using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NaderGorge.Application.Features.Admin.Recharge;
using NaderGorge.Application.Services;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;

namespace NaderGorge.Application.Tests;

public sealed class RechargeMatchDiagnosisTests
{
    private static readonly DateTime Anchor = new(2026, 8, 9, 19, 40, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(-120)]
    [InlineData(120)]
    public void Exact_sms_at_either_window_boundary_is_eligible_even_on_another_wallet(int offsetMinutes)
    {
        var request = Request();
        var sms = Sms(request, Anchor.AddMinutes(offsetMinutes), walletId: Guid.NewGuid());

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.EligibleWaiting, diagnosis.Code);
        Assert.NotNull(diagnosis.Candidate);
        Assert.True(diagnosis.Candidate.WithinWindow);
        Assert.False(diagnosis.Candidate.SameWallet);
    }

    [Fact]
    public async Task Production_regression_attach_loads_cross_wallet_sms_nine_hours_early_as_outside_window_20260810()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();

        var request = Request();
        var receivingWalletId = Guid.NewGuid();
        var sms = Sms(
            request,
            new DateTime(2026, 8, 9, 10, 44, 0, DateTimeKind.Utc),
            walletId: receivingWalletId);
        db.IncomingSmsLogs.Add(sms);
        await db.SaveChangesAsync();

        await RechargeMatchDiagnosis.AttachAsync(db, [request], CancellationToken.None);

        var diagnosis = Assert.IsType<AdminRechargeMatchDiagnosisDto>(request.MatchDiagnosis);
        Assert.Equal(RechargeMatchDiagnosisCode.OutsideWindow, diagnosis.Code);
        Assert.NotNull(diagnosis.Candidate);
        Assert.Equal(receivingWalletId, diagnosis.Candidate.WalletId);
        Assert.Equal(-536, diagnosis.Candidate.TimeOffsetMinutes);
        Assert.Equal(416, diagnosis.Candidate.OutsideWindowByMinutes);
        Assert.True(diagnosis.Candidate.AmountMatches);
        Assert.True(diagnosis.Candidate.PhoneMatches);
        Assert.False(diagnosis.Candidate.WithinWindow);
        Assert.False(diagnosis.Candidate.SameWallet);
    }

    [Fact]
    public void Exact_sms_one_second_after_window_is_outside_by_one_rounded_minute()
    {
        var request = Request();
        var sms = Sms(request, Anchor.AddMinutes(120).AddSeconds(1));

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.OutsideWindow, diagnosis.Code);
        Assert.NotNull(diagnosis.Candidate);
        Assert.Equal(121, diagnosis.Candidate.TimeOffsetMinutes);
        Assert.Equal(1, diagnosis.Candidate.OutsideWindowByMinutes);
        Assert.False(diagnosis.Candidate.WithinWindow);
    }

    [Fact]
    public void Multiple_exact_sms_are_reported_instead_of_selecting_one_arbitrarily()
    {
        var request = Request();
        var first = Sms(request, Anchor.AddMinutes(-5));
        var second = Sms(request, Anchor.AddMinutes(5));

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [first, second]);

        Assert.Equal(RechargeMatchDiagnosisCode.MultipleExactSms, diagnosis.Code);
        Assert.Equal(2, diagnosis.ExactSmsCount);
    }

    [Fact]
    public void One_exact_sms_claimed_by_two_pending_requests_reports_the_competition()
    {
        var firstRequest = Request();
        var secondRequest = Request();
        var sms = Sms(firstRequest, Anchor.AddMinutes(5));

        var diagnosis = RechargeMatchDiagnosis.Diagnose(
            firstRequest,
            [firstRequest, secondRequest],
            [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.CompetingPendingRequests, diagnosis.Code);
        Assert.Equal(1, diagnosis.ExactSmsCount);
        Assert.Equal(2, diagnosis.CompetingRequestCount);
    }

    [Fact]
    public void Exact_sms_already_linked_to_another_request_is_reported_as_claimed()
    {
        var request = Request();
        var sms = Sms(request, Anchor.AddMinutes(3));
        sms.IsMatched = true;
        sms.MatchedRechargeRequestId = Guid.NewGuid();

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.SmsClaimedByAnotherRequest, diagnosis.Code);
        Assert.True(diagnosis.Candidate?.IsMatched);
    }

    [Fact]
    public void Same_phone_with_wrong_amount_is_reported_before_a_weaker_phone_candidate()
    {
        var request = Request();
        var wrongAmount = Sms(request, Anchor.AddMinutes(4), amount: 500m);
        var wrongPhone = Sms(request, Anchor.AddMinutes(1), phone: "01091993544");

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [wrongPhone, wrongAmount]);

        Assert.Equal(RechargeMatchDiagnosisCode.AmountMismatch, diagnosis.Code);
        Assert.Equal(wrongAmount.Id, diagnosis.Candidate?.SmsLogId);
        Assert.True(diagnosis.Candidate?.PhoneMatches);
        Assert.False(diagnosis.Candidate?.AmountMatches);
    }

    [Fact]
    public void Same_amount_with_single_middle_digit_mismatch_exposes_both_matching_sides()
    {
        var request = Request();
        var weakPhone = Sms(request, Anchor.AddMinutes(1), phone: "01511111111");
        var nearPhone = Sms(request, Anchor.AddMinutes(4), phone: "01092993554");

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [weakPhone, nearPhone]);

        Assert.Equal(RechargeMatchDiagnosisCode.PhoneMismatch, diagnosis.Code);
        Assert.Equal(nearPhone.Id, diagnosis.Candidate?.SmsLogId);
        Assert.Equal(6, diagnosis.Candidate?.MatchingDigits);
        Assert.True(diagnosis.Candidate?.HasSingleDigitMismatchPattern);
        Assert.Equal(4, diagnosis.Candidate?.MatchingDigitsBeforeMismatch);
        Assert.Equal(6, diagnosis.Candidate?.MatchingDigitsAfterMismatch);
    }

    [Fact]
    public void One_middle_mismatch_with_four_digits_on_both_sides_requires_confirmation()
    {
        var analysis = RechargePhoneSimilarity.Analyze("01091234567", "01092234567");

        Assert.Equal(6, analysis.LongestCommonDigitSequence);
        Assert.Equal(10, analysis.AlignedMatchingDigits);
        Assert.True(analysis.HasSingleDigitMismatchPattern);
        Assert.Equal(4, analysis.MatchingDigitsBeforeMismatch);
        Assert.Equal(6, analysis.MatchingDigitsAfterMismatch);
        Assert.True(analysis.RequiresConfirmation);
    }

    [Theory]
    [InlineData("01081234567", 3, 7)]
    [InlineData("01091235567", 7, 3)]
    public void One_mismatch_without_four_digits_on_each_side_does_not_require_confirmation(
        string receivedPhone,
        int expectedBefore,
        int expectedAfter)
    {
        var analysis = RechargePhoneSimilarity.Analyze("01091234567", receivedPhone);

        Assert.False(analysis.HasSingleDigitMismatchPattern);
        Assert.Equal(expectedBefore, analysis.MatchingDigitsBeforeMismatch);
        Assert.Equal(expectedAfter, analysis.MatchingDigitsAfterMismatch);
        Assert.False(analysis.RequiresConfirmation);
    }

    [Fact]
    public void Two_phone_mismatches_do_not_use_the_single_mismatch_confirmation_rule()
    {
        var analysis = RechargePhoneSimilarity.Analyze("01091234567", "01092234967");

        Assert.False(analysis.HasSingleDigitMismatchPattern);
        Assert.False(analysis.RequiresConfirmation);
    }

    [Fact]
    public void Unrelated_same_amount_phone_is_not_presented_as_a_candidate()
    {
        var request = Request();
        var sms = Sms(request, Anchor.AddMinutes(1), phone: "01511111111");

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.NoCandidate, diagnosis.Code);
        Assert.Null(diagnosis.Candidate);
    }

    [Fact]
    public void Exact_outgoing_sms_is_not_presented_as_recharge_evidence()
    {
        var request = Request();
        var sms = Sms(request, Anchor.AddMinutes(1));
        sms.Body = "تم خصم 200 جنيه وتحويلها إلى 01091993554";

        var diagnosis = RechargeMatchDiagnosis.Diagnose(request, [request], [sms]);

        Assert.Equal(RechargeMatchDiagnosisCode.NoCandidate, diagnosis.Code);
        Assert.Null(diagnosis.Candidate);
    }

    [Fact]
    public void Missing_request_proof_stops_diagnosis_before_sms_candidates()
    {
        var request = Request();
        request.ScreenshotUrl = null;

        var diagnosis = RechargeMatchDiagnosis.Diagnose(
            request,
            [request],
            [Sms(request, Anchor)]);

        Assert.Equal(RechargeMatchDiagnosisCode.AwaitingEvidence, diagnosis.Code);
        Assert.Null(diagnosis.Candidate);
    }

    private static AdminRechargeRequestDto Request() => new()
    {
        Id = Guid.NewGuid(),
        WalletId = Guid.NewGuid(),
        TeacherId = Guid.NewGuid(),
        Amount = 200m,
        SenderPhoneNumber = "01091993554",
        ScreenshotUrl = "/proof.webp",
        Status = RechargeRequestStatus.Pending,
        CreatedAt = Anchor.AddMinutes(-1),
        MatchingAnchorAt = Anchor
    };

    private static IncomingSmsLog Sms(
        AdminRechargeRequestDto request,
        DateTime receivedAt,
        decimal? amount = null,
        string? phone = null,
        Guid? walletId = null)
    {
        var resolvedWalletId = walletId ?? request.WalletId;
        return new IncomingSmsLog
        {
            Id = Guid.NewGuid(),
            WalletId = resolvedWalletId,
            Wallet = new DigitalWallet
            {
                Id = resolvedWalletId,
                Label = "محفظة استقبال",
                PhoneNumber = "01000000000"
            },
            Sender = "VodafoneCash",
            Body = "incoming transfer",
            ReceivedAt = receivedAt,
            ParsedAmount = amount ?? request.Amount,
            ParsedSenderPhone = phone ?? request.SenderPhoneNumber,
            DeduplicationHash = Guid.NewGuid().ToString("N")
        };
    }
}
