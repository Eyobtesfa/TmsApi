namespace TmsApi.Application.Hubs;

public interface ITmsHubClient
{
    Task ReceiveEnrollmentStatusUpdated(string enrollmentId, string status);
}