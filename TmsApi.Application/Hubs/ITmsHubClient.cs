namespace TmsApi.Application.Hubs; 
 
public interface ITmsHubClient 
{ 

    Task ReceiveEnrollmentStatusUpdated(string enrollmentId, string status);


    Task ReceiveTranscriptReady(string reportId, string downloadUrl); 
    Task ReceiveCourseUpdate(string courseCode, string message); 
    Task ReceiveGradePosted(string courseCode, int studentId, decimal grade); 
}