
using Microsoft.AspNetCore.Mvc;
using TmsApi.Dtos;
using TmsApi.Services;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentServices enrollmentServices) : ControllerBase
{
    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await enrollmentServices.GetByIdAsync(courseId, id, ct);
        return enrollment is not null ? Ok(enrollment) : NotFound();
    }

    [HttpPost]
    public async Task<IActionResult> EnrollStudent(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        // Gate 1: Check existence (404)
        var course = await courseService.GetByIdAsync(courseId, ct);
        if (course is null)
        {
            return NotFound();
        }

        // Gate 2: Check capacity business rule (404 was ruled out, now check 409)
        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Course is full",
                Detail = $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}.",
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext.Request.Path
            });
        }

        // Success Path
        var enrollment = await enrollmentServices.CreateAsync(courseId, request, ct);
        return CreatedAtAction(nameof(GetEnrollment), new { courseId, id = enrollment.Id }, enrollment);
    }
}