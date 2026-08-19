using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.DTOs;
using TmsApi.Application.Queries;
using TmsApi.Application.Utilities;


namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/courses")]
[ApiVersion("2.0")]
public class CourseController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromQuery] string? fields,
        [FromQuery] PagedRequest paging,
        CancellationToken ct = default
    )
    {
        var courses = await mediator.Send(new GetCoursesQuery(paging), ct);
        var shaped = courses.Items.ShapeData(fields, CourseDtoFields.Allowed);
        var links = new List<LinkDto>
        {
        new(Url.Action(nameof(GetCourses), new { page = courses.Page, fields
        })!, "self", "GET")
    };
        if (courses.HasNext)
            links.Add(new(Url.Action(nameof(GetCourses), new { page = courses.Page + 1, fields })!, "next", "GET"));
        if (courses.HasPrevious)
            links.Add(new(Url.Action(nameof(GetCourses), new { page = courses.Page - 1, fields })!, "prev", "GET"));

        return Ok(new
        {
            Data = shaped,
            Meta = new
            {
                courses.TotalCount,
                courses.Page,
                courses.TotalPages,
                courses.HasNext,
                courses.HasPrevious
            },
            Links = links
        });
    }

    [HttpGet("{code}")]
    public async Task<IActionResult> GetCourse(string code, CancellationToken ct)
    {
        var course = await mediator.Send(new GetCourseQuery(code), ct);
        if (course is null) return NotFound();

        return Ok(new
        {
            Data = course,
            Links = new[]
            {
            new LinkDto(Url.Action(nameof(GetCourse), new { code })!, "self","GET"),
            new LinkDto(Url.Action("Enroll", "Enrollments", new { courseCode
= code })!,
                "enroll", "POST")
        }
        });
    }
}