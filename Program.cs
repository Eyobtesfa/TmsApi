
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthentication("Training").AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();
var app = builder.Build();


app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler("/error");

app.UseHttpsRedirection();
app.UseRouting();



app.MapPost("/api/enrollments/smoke-test", async (IEnrollmentService enrollmentService) =>
{
    // Try a successful initial enrollment
    var firstEnroll = await enrollmentService.EnrollAsync("S-001", "CS-101");

    // Intentionally trigger the duplicate path
    var duplicateEnroll = await enrollmentService.EnrollAsync("S-001", "CS-101");

    // Intentionally trigger a 'Not Found' lookup path
    var missingRecord = await enrollmentService.GetByIdAsync("invalid-id");

    return Results.Ok(new
    {
        Message = "Check your console logs for structured logging verification!",
        FirstId = firstEnroll.Id
    });
});



app.MapGet("/api/assessments/results", () => Results.Ok(new
{
    courseCode = "CS-101",
    studentId = "S-001",
    letterGrade = "A"
})).RequireAuthorization();
app.UseAuthentication();
app.UseAuthorization();




app.Run();