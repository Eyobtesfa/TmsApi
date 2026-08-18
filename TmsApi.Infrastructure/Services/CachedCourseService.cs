using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Caching;

namespace TmsApi.Infrastructure.Services;

public class CachedCourseService(
    HybridCache cache,
    ICourseService service,
    ILogger<CachedCourseService> logger) : ICachedCourseService
{
    public async Task<CourseDto?> GetCourseAsync(string code, CancellationToken ct)
    {
        var key = CacheKeys.Course(code);
        var dbHit = false;

        var dto = await cache.GetOrCreateAsync(
            key,
            (service, code),
            async (state, token) =>
            {
                dbHit = true;
                logger.LogInformation("Cache MISS for {Key} fetching from DB", key);
                var course = await state.service.GetByCodeAsync(state.code, token);


                return course is null
                    ? null
                    : new CourseDto
                    (
                        course.Id,
                        course.Code,
                        course.Title,
                        course.MaxCapacity,
                        course.EnrollmentCount

                    );
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct);

        if (dbHit)
            TmsMeters.CacheMisses.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));
        else
            TmsMeters.CacheHits.Add(1, new KeyValuePair<string, object?>("key.kind", "course"));

        return dto;
    }

    // CachedCourseService.GetAllCoursesAsync
    public async Task<List<CourseDto>> GetAllCoursesAsync(CancellationToken ct)
    {
        var key = CacheKeys.CoursesAll;
        var dbHit = false;

        var list = await cache.GetOrCreateAsync(
            key,
            service,
            async (state, token) =>
            {
                dbHit = true;
                logger.LogInformation("Cache MISS for {Key} fetching from DB", key);

                var courses = await state.GetAllAsync(token);
                return courses.Select(c => new CourseDto(
                    c.Id, c.Code, c.Title,
                    c.MaxCapacity, c.EnrollmentCount)).ToList();
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct);

        if (!dbHit)
            logger.LogInformation("Cache HIT for {Key}", key);

        return list;
    }

    public async Task<PagedResponse<CourseDto>> GetCoursesAsync(PagedRequest request, CancellationToken ct)
    {
        var key = CacheKeys.Courses(request.Page, request.PageSize, request.Search, request.OrderBy, request.Descending);
        var dbHit = false;

        var result = await cache.GetOrCreateAsync(
            key,
            (service, request),
            async (state, token) =>
            {
                dbHit = true;
                logger.LogInformation("Cache MISS for {Key} fetching from DB", key);

                var paged = await state.service.GetCoursesAsync(state.request, token);

                var items = paged.Items
                    .Select(c => new CourseDto(c.Id, c.Code, c.Title, c.MaxCapacity, c.EnrollmentCount))
                    .ToList();

                return new PagedResponse<CourseDto>
                {
                    Items = items,
                    TotalCount = paged.TotalCount,
                    Page = paged.Page,
                    PageSize = paged.PageSize
                };
            },
            tags: [CacheKeys.CoursesTag],
            cancellationToken: ct);

        if (!dbHit)
            logger.LogInformation("Cache HIT for {Key}", key);

        return result;
    }
    public async Task InvalidateCourseCacheAsync(CancellationToken ct)
    {
        logger.LogInformation("Invalidating cache tag {Tag}", CacheKeys.CoursesTag);
        await cache.RemoveByTagAsync(CacheKeys.CoursesTag, ct);
    }
}