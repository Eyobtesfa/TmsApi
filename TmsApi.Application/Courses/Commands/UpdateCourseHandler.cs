using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Courses.Commands;


public record UpdateCourseCommand(int Id, string Title, int MaxCapacity)
    : IRequest<Result<bool, EnrollmentError>>;


public class UpdateCourseHandler(
    ICourseService service,
    ICachedCourseService cachedService)
    : IRequestHandler<UpdateCourseCommand, Result<bool, EnrollmentError>>
{
    public async Task<Result<bool, EnrollmentError>> Handle(
        UpdateCourseCommand command,
        CancellationToken ct)
    {

        var updated = await service.UpdateAsync(command, ct);


        if (!updated)
        {
            return Result<bool, EnrollmentError>.Failure(
                EnrollmentError.CourseNotFound(command.Id.ToString()));
        }

        await cachedService.InvalidateCourseCacheAsync(ct);


        return Result<bool, EnrollmentError>.Success(true);
    }
}