using Microsoft.Extensions.Logging;
using TmsApi.Application.Interfaces;
using Polly;
using Polly.Registry;
using System.Net.Http.Json;
namespace TmsApi.Infrastructure.ExternalServices;

public class CertificateService(
    ResiliencePipelineProvider<string> pipelineProvider,
    HttpClient httpClient,
    ILogger<CertificateService> logger
) : ICertificateService
{
    public async Task<CertificateResult> IssueCertificateAsync(
        int studentId, string courseCode, CancellationToken ct)
    {
        var pipeline = pipelineProvider.GetPipeline("certificate-api");

        return await pipeline.ExecuteAsync(async token =>
        {
            logger.LogInformation(
                "Requesting certificate for student {StudentId}, course {CourseCode}",
                studentId, courseCode);

            using var response = await httpClient.PostAsJsonAsync(
                "/fake/certificates",
                new { StudentId = studentId, CourseCode = courseCode },
                token);

            // 5xx → throw (Polly retries / breaker counts). 
            // 4xx → do not throw; read body on success path only (Polly doesNOT retry). 
            if ((int)response.StatusCode >= 500)
                throw new HttpRequestException($"Upstream {(int)response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                // e.g. 400 from fake  surfaced to caller as HTTP 400 withoutretry storm 
                var err = await response.Content.ReadAsStringAsync(token);
                throw new InvalidOperationException($"Certificate service rejected: {(int)response.StatusCode} {err}");
            }

            return await response.Content.ReadFromJsonAsync<CertificateResult>
(
                cancellationToken: token)
                ?? throw new InvalidOperationException("Empty certificate response.");
        }, ct);
    }
}