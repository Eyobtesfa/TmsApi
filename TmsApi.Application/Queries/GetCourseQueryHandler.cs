// TmsApi.Application/Queries/GetCourseQueryHandler.cs
using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Queries;

public class GetCourseQueryHandler(ICachedCourseService cachedCourseService)
    : IRequestHandler<GetCourseQuery, CourseDto?>
{
    public Task<CourseDto?> Handle(GetCourseQuery request, CancellationToken ct) =>
        cachedCourseService.GetCourseAsync(request.Code, ct);
}