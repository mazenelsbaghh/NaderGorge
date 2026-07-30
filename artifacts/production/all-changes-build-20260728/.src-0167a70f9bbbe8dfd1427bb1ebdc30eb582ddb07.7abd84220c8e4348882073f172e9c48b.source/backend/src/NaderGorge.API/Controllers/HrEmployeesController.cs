using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Common.HR;
using NaderGorge.Application.Features.HR.People;
using NaderGorge.Domain.Enums;
using NaderGorge.Domain.Interfaces;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/hr/employees")]
[Authorize]
public sealed class HrEmployeesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAppDbContext _db;
    public HrEmployeesController(IMediator mediator, IAppDbContext db) { _mediator = mediator; _db = db; }

    [HttpGet("{employeeId:guid}")]
    [HasPermission(HrPermissions.EmployeeRead)]
    public async Task<IActionResult> GetEmployee(Guid employeeId, CancellationToken ct)
    {
        var employee = await _db.EmployeeProfiles.AsNoTracking()
            .Where(item => item.Id == employeeId)
            .Select(item => new
            {
                item.Id, item.EmployeeNumber, item.UserId, item.User!.FullName, item.User.PhoneNumber,
                employmentStatus = item.EmploymentStatus.ToString(), item.HireDate, item.TerminationDate,
                workMode = item.WorkMode.ToString(), item.StandardStartTime, item.TargetDailyHours,
                assignments = _db.EmploymentAssignments.Where(a => a.EmployeeId == item.Id)
                    .OrderByDescending(a => a.EffectiveFrom).Select(a => new
                    {
                        a.Id, a.OrganizationUnitId, organizationUnit = a.OrganizationUnit!.Name,
                        position = a.JobPosition != null ? a.JobPosition.Name : null,
                        grade = a.JobGrade != null ? a.JobGrade.Name : null,
                        manager = a.ManagerEmployee != null ? a.ManagerEmployee.User!.FullName : null,
                        location = a.WorkLocation != null ? a.WorkLocation.Name : null,
                        costCenter = a.CostCenter != null ? a.CostCenter.Name : null,
                        a.EffectiveFrom, a.EffectiveTo, a.ChangeReason
                    }).ToList(),
                contracts = _db.EmploymentContracts.Where(c => c.EmployeeId == item.Id)
                    .OrderByDescending(c => c.StartDate).Select(c => new
                    {
                        c.Id, c.ContractNumber, type = c.Type.ToString(), status = c.Status.ToString(),
                        c.StartDate, c.EndDate, c.ProbationEndDate, c.Currency, c.TermsVersion
                    }).ToList()
            }).SingleOrDefaultAsync(ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost("{employeeId:guid}/contracts")]
    [HasPermission(HrPermissions.ContractManage)]
    public async Task<IActionResult> CreateContract(Guid employeeId, CreateContractRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateEmploymentContractCommand(
            employeeId, request.ContractNumber, request.Type, request.StartDate, request.EndDate,
            request.ProbationEndDate, request.BaseSalary, request.Currency, request.TermsJson, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("contracts/{contractId:guid}/transition")]
    [HasPermission(HrPermissions.ContractManage)]
    public async Task<IActionResult> TransitionContract(Guid contractId, TransitionContractRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new TransitionEmploymentContractCommand(contractId, request.Status, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{employeeId:guid}/exit")]
    [HasPermission(HrPermissions.EmployeeManage)]
    public async Task<IActionResult> CompleteExit(Guid employeeId, CompleteExitRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CompleteEmployeeExitCommand(employeeId, request.TerminationDate, User.RequireUserId()), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}

public sealed record CreateContractRequest(string ContractNumber, EmploymentContractType Type, DateOnly StartDate,
    DateOnly? EndDate, DateOnly? ProbationEndDate, decimal BaseSalary, string Currency, string? TermsJson);
public sealed record TransitionContractRequest(EmploymentContractStatus Status);
public sealed record CompleteExitRequest(DateOnly TerminationDate);
