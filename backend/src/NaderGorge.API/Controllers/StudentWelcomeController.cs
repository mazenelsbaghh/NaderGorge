using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaderGorge.API.Extensions;
using NaderGorge.Application.Features.Student.Welcome;

namespace NaderGorge.API.Controllers;

[ApiController]
[Route("api/student/welcome")]
[Authorize(Roles = "Student")]
public sealed class StudentWelcomeController(IMediator mediator) : ControllerBase
{
    public sealed record Receipt(Guid Token);

    [HttpPost("claim")]
    public async Task<IActionResult> Claim(CancellationToken ct) =>
        Ok(await mediator.Send(new ClaimStudentWelcomeCommand(User.RequireUserId()), ct));

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(Receipt receipt, CancellationToken ct) =>
        Ok(await mediator.Send(new CompleteStudentWelcomeCommand(User.RequireUserId(), receipt.Token), ct));

    [HttpPost("release")]
    public async Task<IActionResult> Release(Receipt receipt, CancellationToken ct) =>
        Ok(await mediator.Send(new ReleaseStudentWelcomeCommand(User.RequireUserId(), receipt.Token), ct));
}
