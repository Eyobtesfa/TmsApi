using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using TmsApi.Application.Hubs;

namespace TmsApi.Api.Controllers;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/enrollments")]
public class EnrollmentsController(ISender mediator,
IHubContext<TmsHub, ITmsHubClient> hubContext) : ControllerBase
{
    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken ct)
    {
        await hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(id, "Approved");

        return NoContent();
    }
    [HttpPost]
    public async Task<IActionResult> Enroll(
        [FromBody] EnrollStudentCommand command,
        CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created => CreatedAtAction(
                nameof(GetSchedule),
                new { studentId = created.StudentId },
                created),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" => StatusCodes.Status404NotFound,
                    "course_full" or "already_enrolled" => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status400BadRequest
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}");
            });
    }

    [HttpGet("{studentId:int}/schedule")]
    public async Task<IActionResult> GetSchedule(
        int studentId,
        CancellationToken ct)
    {
        var schedule = await mediator.Send(
            new GetStudentScheduleQuery(studentId), ct);

        return Ok(schedule);
    }
}