using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using NaderGorge.Application.Features.LiveSupport.Dtos;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Entities.LiveSupport;
using NaderGorge.Domain.Enums;
using NaderGorge.Infrastructure.Data;
using NaderGorge.Infrastructure.Services;

namespace NaderGorge.Integration.Tests.LiveSupport;

public sealed class WhatsAppCampaignVariableTests
{
    [Fact]
    public async Task ProductionRegression_20260826_ParentTrackingCode_IsFrozenAndMissingCodesAreExcluded()
    {
        await using var db = CreateDb();
        var protector = CreateProtector();
        var template = CampaignTemplate();
        var studentRole = new Role { Name = "Student", Type = RoleType.Student };
        AddStudent(db, protector, studentRole, ("Student With Code", "01012345678", "123456"));
        AddStudent(db, protector, studentRole, ("Student Without Code", "01112345678", null));
        AddStudent(db, protector, studentRole, ("Student With Blank Code", "01212345678", "  "));
        db.LiveSupportWhatsAppTemplates.Add(template);
        await db.SaveChangesAsync();
        var service = new WhatsAppCampaignService(db, protector, new ConfigurationBuilder().Build());
        var request = PreviewRequest(template.Id);

        var preview = await service.PreviewAsync(request, CancellationToken.None);
        var draft = await service.CreateDraftAsync(Guid.NewGuid(), "tracking-code-draft", new(
            "Tracking code campaign", template.Id, preview.AudienceFingerprint,
            request.Filters, request.VariableMappings), CancellationToken.None);

        Assert.Equal(1, preview.EligibleCount);
        Assert.Equal(2, preview.ExcludedCount);
        Assert.Equal(2, preview.ExcludedByReason["missing_variable"]);
        var renderedPreview = Assert.Single(preview.Samples).RenderedPreview;
        Assert.Contains("رقم متابعة محجوب", renderedPreview);
        Assert.DoesNotContain("123456", renderedPreview);
        Assert.Equal(1, draft.RecipientCount);
        var recipient = Assert.Single(await db.WhatsAppCampaignRecipients.AsNoTracking().ToListAsync());
        var plaintext = protector.Unprotect(recipient.Id, recipient.ProtectedPayload, recipient.PayloadDigest);
        var payload = Assert.IsType<WhatsAppCampaignService.FrozenRecipientPayload>(
            WhatsAppCampaignService.DeserializeFrozenRecipientPayload(plaintext));
        Assert.Equal(["123456"], Assert.Single(payload.Components).Parameters);
    }

    [Fact]
    public async Task ParentTrackingCode_MarketingTemplate_IsRejected()
    {
        await using var db = CreateDb();
        var template = CampaignTemplate("MARKETING");
        db.LiveSupportWhatsAppTemplates.Add(template);
        await db.SaveChangesAsync();
        var service = new WhatsAppCampaignService(
            db, CreateProtector(), new ConfigurationBuilder().Build());

        var exception = await Assert.ThrowsAsync<WhatsAppCampaignException>(() =>
            service.PreviewAsync(PreviewRequest(template.Id), CancellationToken.None));

        Assert.Equal(WhatsAppCampaignErrorCodes.InvalidRequest, exception.Code);
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase($"whatsapp-campaign-variables-{Guid.NewGuid():N}")
        .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
        .Options);

    private static WhatsAppCampaignDataProtector CreateProtector()
    {
        var hmacKey = Convert.ToBase64String(Enumerable.Range(1, 32).Select(number => (byte)number).ToArray());
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["WhatsAppCampaigns:HmacKey"] = hmacKey
        }).Build();
        return new WhatsAppCampaignDataProtector(new EphemeralDataProtectionProvider(), configuration);
    }

    private static LiveSupportWhatsAppTemplate CampaignTemplate(string category = "UTILITY") => new()
    {
        MetaTemplateId = "meta-parent-tracking-code",
        Name = "student_parent_tracking_code",
        Language = "ar",
        Category = category,
        Status = "APPROVED",
        ComponentsJson = """[{"type":"BODY","text":"رقم المتابعة: {{1}}"}]""",
        Fingerprint = new string('a', 64),
        LastSyncedAt = DateTime.UtcNow,
        Version = 1
    };

    private static WhatsAppCampaignPreviewRequest PreviewRequest(Guid templateId) => new(
        templateId,
        new WhatsAppCampaignAudienceFilterDto(ContactRoles: ["StudentPrimary"]),
        [new WhatsAppCampaignVariableMappingDto(
            "BODY", 1, "ParentTrackingCode", ComponentIndex: 0)]);

    private static void AddStudent(
        AppDbContext db,
        WhatsAppCampaignDataProtector protector,
        Role role,
        (string Name, string Phone, string? ParentTrackingCode) studentSeed)
    {
        var student = new User
        {
            FullName = studentSeed.Name,
            PhoneNumber = studentSeed.Phone,
            PasswordHash = "test"
        };
        student.StudentProfile = new StudentProfile
        {
            User = student,
            UserId = student.Id,
            ParentTrackingCode = studentSeed.ParentTrackingCode
        };
        student.UserRoles.Add(new UserRole { User = student, UserId = student.Id, Role = role, RoleId = role.Id });
        db.Users.Add(student);
        var e164 = Assert.IsType<string>(WhatsAppCampaignService.NormalizeE164(studentSeed.Phone));
        db.WhatsAppContactPreferences.Add(new WhatsAppContactPreference
        {
            StudentUserId = student.Id,
            ContactRole = "StudentPrimary",
            DestinationHash = protector.DestinationHash(e164),
            DestinationLast4 = e164[^4..],
            Category = WhatsAppContactPreferenceCategory.Utility,
            State = WhatsAppContactPreferenceState.OptedIn,
            Source = "test",
            EvidenceReference = "campaign variable regression",
            EffectiveAt = DateTime.UtcNow.AddMinutes(-1),
            IdempotencyKey = $"consent-{student.Id:N}",
            RequestHash = "test"
        });
    }
}
