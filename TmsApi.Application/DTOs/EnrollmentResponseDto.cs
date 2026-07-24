

namespace TmsApi.Application.DTOs;

public record CourseInfo(string Code, string Title);
public record EnrollmentResponseDto
(
    int Id,
    int CourseId,
    int StudentId,
    DateTime EnrolledAt,
    CourseInfo Course
);