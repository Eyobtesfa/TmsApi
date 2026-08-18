// TmsApi.Application/Queries/GetCourseQuery.cs
using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Queries;

public record GetCourseQuery(string Code) : IRequest<CourseDto?>;