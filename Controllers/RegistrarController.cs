using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/registrar")]
public class RegistrarController(TmsDbContext context) : ControllerBase
{

    [HttpGet("higher-gpa")]
    public async Task<IActionResult> GetHigherGPA()
    {
        var count = await context.Students
        .Where(s => s.IsActive && s.GPA >= 3.0m)
        .CountAsync();
        return Ok(count);
    }
    [HttpGet("most-enrollment")]
    public async Task<IActionResult> GetMostEnrollment()
    {
        var list = await context.Courses
.Select(c => new
{
    c.Title,
    EnrollmentCount = c.Enrollments.Count
})
.OrderByDescending(x => x.EnrollmentCount)
.ToListAsync();
        return Ok(list);
    }

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


    [HttpGet("unenrolled-students-subquery")]
    public async Task<IActionResult> GetUnenrolledStudentsSubquery()
    {
        var list = await context.Students
            .Where(s => !s.Enrollments.Any())
            .Select(s => s.Name)
            .ToListAsync();

        return Ok(list);
    }


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
    [HttpGet("students")]
    public async Task<IActionResult> GetPaginatedStudents([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;

        // Calculate the database-side offset
        int skipCount = (page - 1) * pageSize;

        var students = await context.Students
            .OrderBy(s => s.Name) // Stable sort
            .Skip(skipCount)      // Translated to OFFSET
            .Take(pageSize)       // Translated to LIMIT
            .ToListAsync();

        return Ok(students);
    }
    [HttpGet("top-courses")]
    public async Task<IActionResult> GetTopCourses()
    {
        var topCourses = await context.Enrollments
            .GroupBy(e => e.Course.Title)
            .Select(g => new
            {
                CourseTitle = g.Key,
                EnrollmentCount = g.Count() // Database-side math
            })
            .OrderByDescending(c => c.EnrollmentCount)
            .Take(5) // Top 5 records only
            .ToListAsync();

        return Ok(topCourses);
    }
    //MODULE 5 SESSION 3  
    [HttpGet("slow-report")]
    public async Task<IActionResult> GetEnrollmentReportSlow(CancellationToken cancellationToken)
    {
        var students = await context.Students.AsNoTracking().ToListAsync(cancellationToken);
        var output = new List<string>();
        foreach (var s in students)
        {
            var count = await context.Enrollments
                .AsNoTracking()
                .CountAsync(e => e.StudentId == s.Id, cancellationToken);
            Console.WriteLine($"{s.Name}: {count} enrollments");
            output.Add($"{s.Name}: {count} enrollments");
        }
        return Ok(output);
    }
    [HttpGet("fast-report")]
    public async Task<IActionResult> GetEnrollmentReportFast(CancellationToken cancellationToken)
    {
        var report = await context.Students
            .AsNoTracking()
            .Select(s => new
            {
                s.Name,
                EnrollmentCount = s.Enrollments.Count
            })
            .ToListAsync(cancellationToken);
        foreach (var r in report)
            Console.WriteLine($"{r.Name}: {r.EnrollmentCount} enrollments");
        var outputLines = report.Select(r => $"{r.Name}: {r.EnrollmentCount} enrollments");

        return Ok(outputLines);
    }
    [HttpGet("alternative")]
    public async Task<IActionResult> GetEnrollmentAlternative(CancellationToken cancellationToken)
    {
        var output = new List<string>();
        var students = await context.Students
            .AsNoTracking()
            .Include(s => s.Enrollments)
            .ToListAsync(cancellationToken);
        foreach (var s in students)
        {
            Console.WriteLine($"{s.Name}: {s.Enrollments.Count} enrollments");
            output.Add($"{s.Name}: {s.Enrollments.Count}");
        }
        return Ok(output);
    }
}