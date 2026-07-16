using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
//MODULE 6
using TmsApi.Infrastructure.Persistence;
using TmsApi.Api.Filters;
using TmsApi.Domain.Entities;

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

builder.Services.AddDbContext<TmsDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
.LogTo(Console.WriteLine, LogLevel.Information) // Log SQL to output window
.EnableSensitiveDataLogging());

/*builder.Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();*/
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentServices, EnrollmentServices>();


//MODULE 6 
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

var app = builder.Build();


/*var students = new List<Student>{
    new Student("S-001", "Abeba"),
    new Student("S-002", "Abebe"),
    new Student("S-003", "Jack"),
};

var courses = new List<Course>
{
    new Course("C-01", "C#"),
    new Course("C-02", "TypeScript")
};
*/

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

}


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



/*app.MapGet("/api/students", () =>
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
});*/


using (var scope = app.Services.CreateScope())
{
    // 1. Specify your actual DbContext class name inside the angle brackets <>
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();

    // Applies any pending migrations on startup
    context.Database.Migrate();

    if (!context.Students.Any())
    {
        // 2. Fixed explicit generic collections or simplified target-typed new()
        var students = new List<Student>
        {
            new() { RegistrationNumber = "TMS-2026-0001", Name = "Alice Smith", GPA = 3.8m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0002", Name = "Bob Jones", GPA = 2.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0003", Name = "Charlie Brown", GPA = 3.4m, IsActive = false },
            new() { RegistrationNumber = "TMS-2026-0004", Name = "Diana Prince", GPA = 3.9m, IsActive = true },
            new() { RegistrationNumber = "TMS-2026-0005", Name = "Evan Wright", GPA = 2.5m, IsActive = true }
        };
        context.Students.AddRange(students);

        var courses = new List<Course>
        {
            new() { Code = "CS-101", Title = "Introduction to Computer Science", MaxCapacity = 30 },
            new() { Code = "CS-201", Title = "Data Structures and Algorithms", MaxCapacity = 25 },
            new() { Code = "MAT-101", Title = "Calculus I", MaxCapacity = 40 }
        };
        context.Courses.AddRange(courses);

        // 3. This saves students & courses, forcing Postgres to generate and return their 'Id' values
        context.SaveChanges();

        var enrollments = new List<Enrollment>
        {
            new() { StudentId = students[0].Id, CourseId = courses[0].Id, Grade = 4.0m },
            new() { StudentId = students[0].Id, CourseId = courses[1].Id, Grade = 3.6m },
            new() { StudentId = students[1].Id, CourseId = courses[0].Id, Grade = 2.8m },
            new() { StudentId = students[3].Id, CourseId = courses[1].Id, Grade = 3.9m }
        };
        context.Enrollments.AddRange(enrollments);

        // 4. Final save to record the new relationship data
        context.SaveChanges();
    }
}







if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(context);
}
app.Run();

//public record Student(string Id, string Name);
//public record Course(string CourseCode, string Name);