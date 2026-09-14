using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Controllers;
using NaderGorge.Application.Features.HR.Lifecycle;
using NaderGorge.Application.Features.HR.Recruitment;
using NaderGorge.Domain.Entities;
using NaderGorge.Domain.Enums;

namespace NaderGorge.Application.Tests.HR;

public sealed class RecruitmentLifecycleTests
{
    [Fact]
    public async Task AcceptedCandidateConvertsToOneAccountAndCompleteEmployeeAtomically()
    {
        await using var db = TestAppDbContextFactory.Create(); var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Recruiter", "01073333331");
        var requisition = new Requisition { RequisitionNumber = "REQ-1", Title = "Support", RequestedByUserId = actor.Id, State = RequisitionState.Open };
        var candidate = new Candidate { RequisitionId = requisition.Id, Requisition = requisition, FullName = "New Employee", PhoneNumber = "01073333332", Stage = CandidateStage.Offer };
        var offer = new CandidateOffer { CandidateId = candidate.Id, Candidate = candidate, OfferNumber = "OFF-1", BaseSalary = 7000, ProposedStartDate = new DateOnly(2026, 8, 1), State = OfferState.Accepted, AcceptedAt = DateTime.UtcNow };
        requisition.Candidates.Add(candidate); candidate.Offers.Add(offer); db.Requisitions.Add(requisition); await db.SaveChangesAsync();
        var service = new RecruitmentService(db); var first = await service.HireAcceptedCandidateAsync(candidate.Id, offer.Id, "hashed-password", actor.Id, default); var replay = await service.HireAcceptedCandidateAsync(candidate.Id, offer.Id, "hashed-password", actor.Id, default);
        Assert.True(first.Success); Assert.Equal(first.Data, replay.Data); Assert.Single(db.EmployeeProfiles); Assert.Equal(candidate.PhoneNumber, db.Users.Single(item => item.Id != actor.Id).PhoneNumber);
        Assert.Equal(first.Data, db.Candidates.Single().EmployeeProfileId); Assert.Equal(CandidateStage.Hired, db.Candidates.Single().Stage); Assert.NotEmpty(db.EmployeeLifecycleTasks);
    }

    [Fact]
    public async Task CandidateWithExistingUserPhoneIsNotHired()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Recruiter", "01073333335");
        await TestAppDbContextFactory.SeedUserAsync(db, "Existing", "01073333336");
        var requisition = new Requisition { RequisitionNumber = "REQ-2", Title = "Support", RequestedByUserId = actor.Id, State = RequisitionState.Open };
        var candidate = new Candidate { Requisition = requisition, FullName = "Duplicate Phone", PhoneNumber = "01073333336", Stage = CandidateStage.Offer };
        var offer = new CandidateOffer { Candidate = candidate, OfferNumber = "OFF-2", BaseSalary = 7000, ProposedStartDate = new DateOnly(2026, 8, 1), State = OfferState.Accepted };
        candidate.Offers.Add(offer); requisition.Candidates.Add(candidate); db.Requisitions.Add(requisition); await db.SaveChangesAsync();

        var response = await new RecruitmentService(db).HireAcceptedCandidateAsync(candidate.Id, offer.Id, "hashed-password", actor.Id, default);

        Assert.False(response.Success);
        Assert.NotNull(response.Errors);
        Assert.Contains("PHONE_ALREADY_EXISTS", response.Errors);
        Assert.Empty(db.EmployeeProfiles);
        Assert.Null(candidate.EmployeeProfileId);
    }

    [Fact]
    public async Task ConvertedOfferCannotBeAcceptedAgain()
    {
        await using var db = TestAppDbContextFactory.Create();
        var actor = await TestAppDbContextFactory.SeedUserAsync(db, "Recruiter", "01073333337");
        var requisition = new Requisition { RequisitionNumber = "REQ-3", Title = "Support", RequestedByUserId = actor.Id, State = RequisitionState.Open };
        var candidate = new Candidate { Requisition = requisition, FullName = "Already Hired", PhoneNumber = "01073333338", Stage = CandidateStage.Hired };
        var offer = new CandidateOffer { Candidate = candidate, OfferNumber = "OFF-3", BaseSalary = 7000, ProposedStartDate = new DateOnly(2026, 8, 1), State = OfferState.Converted };
        candidate.Offers.Add(offer); requisition.Candidates.Add(candidate); db.Requisitions.Add(requisition); await db.SaveChangesAsync();
        var controller = CreateController(db, actor.Id);

        var response = await controller.AcceptOffer(offer.Id, new CandidateVersionRequest(offer.Version), default);

        Assert.IsType<ConflictResult>(response);
        Assert.Equal(OfferState.Converted, offer.State);
        Assert.Null(offer.AcceptedAt);
    }

    [Fact]
    public async Task OpenAssetBlocksOffboardingThenCompletionDisablesAccessAndKeepsHistory()
    {
        await using var db = TestAppDbContextFactory.Create(); var actor = await TestAppDbContextFactory.SeedUserAsync(db, "HR", "01073333333"); var user = await TestAppDbContextFactory.SeedUserAsync(db, "Leaving", "01073333334");
        var employee = new EmployeeProfile { UserId = user.Id, User = user }; employee.EmployeeNumber = EmployeeProfile.GenerateEmployeeNumber(employee.Id); var asset = new HrAsset { Code = "PHONE-1", Name = "Phone" };
        db.EmployeeProfiles.Add(employee); db.HrAssets.Add(asset); await db.SaveChangesAsync(); var documentAssets = new DocumentAssetService(db); await documentAssets.AssignAssetAsync(asset.Id, employee.Id, actor.Id, "good", default);
        var service = new LifecycleOrchestrationService(db, documentAssets); var process = await service.StartOffboardingAsync(employee.Id, new DateOnly(2026, 8, 31), "resigned", actor.Id, default);
        Assert.False(process.Success); var custody = db.AssetCustodies.Single(); await documentAssets.ReturnAssetAsync(custody.Id, actor.Id, "good", default);
        process = await service.StartOffboardingAsync(employee.Id, new DateOnly(2026, 8, 31), "resigned", actor.Id, default); Assert.True(process.Success);
        Assert.True((await service.CompleteOffboardingAsync(process.Data, actor.Id, 1, default)).Success); Assert.False(user.IsActive); Assert.Equal(EmployeeEmploymentStatus.Terminated, employee.EmploymentStatus); Assert.Single(db.AssetCustodies);
    }

    private static HrRecruitmentLifecycleController CreateController(NaderGorge.Infrastructure.Data.AppDbContext db, Guid actorUserId)
    {
        var controller = new HrRecruitmentLifecycleController(
            db,
            new RecruitmentService(db),
            new LifecycleOrchestrationService(db, new DocumentAssetService(db)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, actorUserId.ToString())], "Test"))
            }
        };
        return controller;
    }
}
