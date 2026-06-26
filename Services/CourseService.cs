
using Microsoft.EntityFrameworkCore;
using TmsApi.Data;
using TmsApi.Entities;

namespace TmsApi.Services;

public class CourseService(TmsDbContext context, ILogger<CourseService>logger): ICourseService
{
    public async Task<Course?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await context.Courses
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        throw new NotImplementedException();
    }
    public async Task<Course> CreateAsync(Course course, CancellationToken ct)
    {
        await context.Courses.AddAsync(course, ct);
        await context.SaveChangesAsync(ct);

        //logger.LogInformation("Successfully created course with ID: {CourseId}", course.Id);

        return course;
        throw new NotImplementedException();
    }
}