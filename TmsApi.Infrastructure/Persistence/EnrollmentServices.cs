using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;

namespace TmsApi.Infrastructure.Persistence;

public class EnrollmentServices(TmsDbContext context, ILogger<EnrollmentServices> logger) : IEnrollmentServices
{
    public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
        context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt, new CourseInfo(e.Course.Code, e.Course.Title)))
            .FirstOrDefaultAsync(ct);
    public Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct) =>
     context.Enrollments
         .AsNoTracking()
         .Where(e => e.CourseId == courseId)
         .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt, new CourseInfo(e.Course.Code, e.Course.Title)))
         .FirstOrDefaultAsync(ct);

    public async Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct)
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Student {StudentId} successfully enrolled in Course {CourseId}", request.StudentId, courseId);

        return (await GetByIdAsync(courseId, enrollment.Id, ct))!;
    }

    public async Task<EnrollmentResponseDto> AddAsync(Enrollment enrollment, CancellationToken ct)
    {
       await context.Enrollments.AddAsync(enrollment, ct);
       await context.SaveChangesAsync(ct);


       var courseInfo = await context.Courses
            .AsNoTracking()
            .Where(c => c.Id == enrollment.CourseId)
            .Select(c => new CourseInfo( c.Code, c.Title ))
            .FirstAsync(ct);

        return new EnrollmentResponseDto(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.EnrolledAt,
            courseInfo
        );
    }

    public async Task<List<EnrollmentResponseDto>> GetByStudentIdAsync(int studentId, CancellationToken ct){
        return await context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.CourseId,
                e.EnrolledAt,
                new CourseInfo(e.Course.Code, e.Course.Title)
             
            ))
            .ToListAsync(ct);
    }
    public async Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct)
    {
        return await context.Enrollments
            .AnyAsync(e => e.StudentId == studentId && e.Course.Code == courseCode, ct);
    }
}