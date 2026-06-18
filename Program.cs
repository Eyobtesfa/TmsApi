using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();
builder.Services.AddControllers();

builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

/*builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();*/

var app = builder.Build();


var students = new List<Student>{
    new Student("S-001", "Abeba"),
    new Student("S-002", "Abebe"),
    new Student("S-003", "Jack"),
};

var courses = new List<Course>
{
    new Course("C-01", "C#"),
    new Course("C-02", "TypeScript")
};

app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseExceptionHandler();
}
else
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/api/error", () =>
{
    throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
});

app.MapPost("/api/enrollments/smoke-test", async (IEnrollmentService enrollmentService) =>
{
    var firstEnroll = await enrollmentService.EnrollAsync("S-001", "CS-101");
    var duplicateEnroll = await enrollmentService.EnrollAsync("S-001", "CS-101");
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



app.MapGet("/api/students", () =>
{
    return Results.Ok(students);
});

app.MapGet("/api/students/{id}", (string id) =>
{
    var student = students.FirstOrDefault(s => s.Id.ToLower() == id.ToLower());

    if (student is null)
    {
        return Results.NotFound(new { message = $"Student with ID '{id}' was not found." });
    }

    return Results.Ok(student);
});


app.MapGet("/api/courses", () =>
{
    return Results.Ok(courses);
});

app.MapGet("/api/courses/{courseCode}", (string courseCode) =>
{
    var course = courses.FirstOrDefault(c => c.CourseCode.ToLower() == courseCode.ToLower());
    if (course is null)
    {
        return Results.NotFound(new { message = $"Course with Course Code {courseCode} npt found" });
    }
    return Results.Ok(course);
});
app.Run();

public record Student(string Id, string Name);
public record Course(string CourseCode, string Name);