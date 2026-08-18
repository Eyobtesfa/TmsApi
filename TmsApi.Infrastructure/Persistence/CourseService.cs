
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Courses.Commands;

namespace TmsApi.Infrastructure.Persistence;

public class CourseService(TmsDbContext context, ILogger<CourseService> logger) : ICourseService
{
    public Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct) =>
        context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id, c.Code, c.Title, c.MaxCapacity, c.Enrollments.Count
            )).FirstOrDefaultAsync(ct);

    public async Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct)
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity
        };
        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);
        return (await GetByIdAsync(course.Id, ct))!;
    }
    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        context.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);

    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct)
    {

        var query = context.Courses.AsNoTracking();


        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            query = query.Where(c => EF.Functions.ILike(c.Title, $"%{request.Search}%")
                                  || EF.Functions.ILike(c.Code, $"%{request.Search}%"));
        }


        var totalCount = await query.CountAsync(ct);


        query = request.OrderBy switch
        {
            "Code" => request.Descending ? query.OrderByDescending(c => c.Code) : query.OrderBy(c => c.Code),
            "MaxCapacity" => request.Descending ? query.OrderByDescending(c => c.MaxCapacity) : query.OrderBy(c => c.MaxCapacity),
            _ => request.Descending ? query.OrderByDescending(c => c.Title) : query.OrderBy(c => c.Title)
        };


        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id, c.Code, c.Title, c.MaxCapacity, c.Enrollments.Count
            ))
            .ToListAsync(ct);


        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<CourseResponseDto?> GetByCodeAsync(string courseCode, CancellationToken ct)
    {
        return await context.Courses
        .AsNoTracking()
        .Where(c => c.Code == courseCode)
        .Select(c => new CourseResponseDto(
            c.Id,
            c.Code,
            c.Title,
            c.MaxCapacity,
            c.Enrollments.Count
        ))
        .FirstOrDefaultAsync(ct);
    }


    public async Task<bool> UpdateAsync(UpdateCourseCommand command, CancellationToken ct = default)
    {
        var course = await context.Courses.FindAsync([command.Id], ct);
        if (course is null) return false;

        course.Title = command.Title;
        course.MaxCapacity = command.MaxCapacity;

        await context.SaveChangesAsync(ct);
        return true;
    }
    // CourseService.cs
    public Task<List<CourseResponseDto>> GetAllAsync(CancellationToken ct) =>
    context.Courses
        .AsNoTracking()
        .Select(c => new CourseResponseDto(
            c.Id, c.Code, c.Title, c.MaxCapacity, c.Enrollments.Count))
        .ToListAsync(ct);


}

