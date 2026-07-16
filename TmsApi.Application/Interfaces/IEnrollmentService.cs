using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentServices
{
    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(int courseId, EnrollStudentRequest request, CancellationToken ct);
}