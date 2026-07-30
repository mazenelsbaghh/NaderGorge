using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.People;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/hr/organization")]
[Authorize]
public sealed class HrOrganizationController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    public HrOrganizationController(IAppDbContext db, IMediator mediator) { _db = db; _mediator = mediator; }

    [HttpGet("units")]
    [HasPermission(HrPermissions.OrganizationRead)]
    public async Task<IActionResult> GetUnits(CancellationToken ct) => Ok(await _db.OrganizationUnits
        .AsNoTracking().OrderBy(item => item.Name)
        .Select(item => new { item.Id, item.Code, item.Name, type = item.Type.ToString(), item.ParentId, item.ManagerEmployeeId, item.IsActive })
        .ToListAsync(ct));

    [HttpPost("assignments")]
    [HasPermission(HrPermissions.OrganizationManage)]
    public async Task<IActionResult> CreateAssignment(CreateAssignmentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateEmploymentAssignmentCommand(
            request.EmployeeId, request.OrganizationUnitId, request.JobPositionId, request.JobGradeId,
            request.ManagerEmployeeId, request.WorkLocationId, request.CostCenterId,
            request.EffectiveFrom, request.EffectiveTo, request.ChangeReason, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public sealed record CreateAssignmentRequest(
    Guid EmployeeId, Guid OrganizationUnitId, Guid? JobPositionId, Guid? JobGradeId,
    Guid? ManagerEmployeeId, Guid? WorkLocationId, Guid? CostCenterId,
    DateOnly EffectiveFrom, DateOnly? EffectiveTo, string ChangeReason);
