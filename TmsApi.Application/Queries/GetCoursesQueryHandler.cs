using MediatR;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Queries;
using TmsApi.Application.DTOs;
public class GetCoursesQueryHandler(ICachedCourseService cachedCourseService)
    : IRequestHandler<GetCoursesQuery, PagedResponse<CourseDto>>
{
    public Task<PagedResponse<CourseDto>> Handle(GetCoursesQuery request, CancellationToken ct) =>
        cachedCourseService.GetCoursesAsync(request.Paging, ct);
}