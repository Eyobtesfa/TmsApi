// TmsApi.Application/Queries/GetCoursesQuery.cs
using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Queries;

public record GetCoursesQuery(PagedRequest Paging) : IRequest<PagedResponse<CourseDto>>;