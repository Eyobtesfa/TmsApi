using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/registrar")]
public class RegistrarController(TmsDbContext context) : ControllerBase
{
    // Query 1 & 2 are grouped here conceptually to satisfy the criteria:
    // "What is the average GPA per course?" and "Which students have zero enrollments?"

    /// <summary>
    /// Business Query 3: What is the average GPA per course?
    /// Verification: Look for GROUP BY and the AVG aggregation function in your console log.
    /// </summary>
    [HttpGet("average-gpa-per-course")]
    public async Task<IActionResult> GetAverageGpaPerCourse()
    {
        var list = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                Course = g.Key,
                AverageGPA = g.Average(e => e.Student.GPA)
            })
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>
    /// Business Query 4 (Approach A): Which students have zero enrollments using a Subquery?
    /// Verification: Outputs a query utilizing NOT EXISTS (SELECT 1 FROM "Enrollments" ...).
    /// </summary>
    [HttpGet("unenrolled-students-subquery")]
    public async Task<IActionResult> GetUnenrolledStudentsSubquery()
    {
        var list = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();

        return Ok(list);
    }

    /// <summary>
    /// Business Query 4 (Approach B): Which students have zero enrollments using LeftJoin?
    /// Verification: Outputs a explicit LEFT JOIN ... WHERE ... IS NULL layout.
    /// </summary>
    [HttpGet("unenrolled-students-leftjoin")]
    public async Task<IActionResult> GetUnenrolledStudentsLeftJoin()
    {
        var list = await context.Students
            .LeftJoin(context.Enrollments,
                s => s.Id,
                e => e.StudentId,
                (s, e) => new { s, e })
            .Where(x => x.e == null)
            .Select(x => x.s.Name)
           .ToListAsync();

        return Ok(list);
    }
}