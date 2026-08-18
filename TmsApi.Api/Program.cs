using Microsoft.AspNetCore.Mvc;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.Interfaces;
//MODULE 6
using TmsApi.Infrastructure.Persistence;
using TmsApi.Api.Filters;
using TmsApi.Domain.Entities;
//MODULE 7
using Asp.Versioning;
using TmsApi.Api.Middleware;
using TmsApi.Application.Enrollments.Commands;
using FluentValidation;
using MediatR;
using TmsApi.Application.Behaviors;
using TmsApi.Api.ExceptionHandlers;
using Microsoft.Extensions.Caching.Hybrid;
using TmsApi.Infrastructure.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using TmsApi.Api.RateLimiting;
using Microsoft.AspNetCore.Cors;
using System.Threading.Channels;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Workers;

using TmsApi.Application.Hubs;
using TmsApi.Infrastructure.Transcripts;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using TmsApi.Infrastructure.ExternalServices;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.Runtime;

var builder = WebApplication.CreateBuilder(args);

const string ServiceName = "tms-api";

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



//MODULE 7

builder.Services.AddOpenApi("v1", options =>
{
    options.ShouldInclude = description =>
    description.GroupName == "v1";

});

builder.Services.AddOpenApi("v2", options =>
{
    options.ShouldInclude = description =>
    description.GroupName == "v2";
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = ApiVersionReader.Combine(
        new UrlSegmentApiVersionReader(),
        new HeaderApiVersionReader("X-Api-Version")
    );

})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});


builder.Services.AddMediatR(cfg =>
cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly));
builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();


//MODULE 7 SESSION 2

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };
});

builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext);

        return tier switch
        {
            ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"paid:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 100,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    AutoReplenishment = true
                }
            ),

            ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"free:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    AutoReplenishment = true
                }
            ),

            _ => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"anon:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    AutoReplenishment = true
                }
            )
        };
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        var retryAfter = "10";
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ts))
            retryAfter = ((int)ts.TotalSeconds).ToString(); // Dynamic wait calculation!

        context.HttpContext.Response.Headers.RetryAfter = retryAfter;
        context.HttpContext.Response.ContentType = "application/problem+json";

        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "Rate limit exceeded",
            Detail = $"Too many requests. Retry after {retryAfter} seconds.",
            Status = StatusCodes.Status429TooManyRequests,
            Type = "https://tms.local/errors/rate_limit_exceeded"
        }, ct);
    };

    options.AddConcurrencyLimiter("transcripts", opt =>
{
    opt.PermitLimit = 5;                        // Max 5 running at the same time
    opt.QueueLimit = 20;                       // Max 20 waiting in line
    opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
});
    options.AddTokenBucketLimiter("search", opt =>
    {
        opt.TokenLimit = 10;
        opt.TokensPerPeriod = 5;
        opt.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        opt.QueueLimit = 2;
    });


});

//MODULE 7-3

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();
builder.Services.AddSingleton(Channel.CreateBounded<TranscriptRequest>(
    new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait
    }
));

builder.Services.AddHostedService<TranscriptWorker>();


//MODULE 7-4

builder.Services.AddResiliencePipeline("certificate-api", pipeline =>
{
    pipeline
        // Outer: per-request hard timeout  protects against hangs 
        .AddTimeout(TimeSpan.FromSeconds(5))
        // Middle: circuit breaker  protects against sustained outage 
        .AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 10,
            SamplingDuration = TimeSpan.FromSeconds(30),
            BreakDuration = TimeSpan.FromSeconds(15),
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnOpened = args =>
            {
                Console.WriteLine("Circuit OPENED  stopping requests to certificate service");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                Console.WriteLine("Circuit CLOSED  certificate service recovered");
                return ValueTask.CompletedTask;
            }
        })
        // Inner: retry with jitter  only for transient failures 
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>(),
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"Retry #{args.AttemptNumber} after {args.RetryDelay.TotalMilliseconds:F0}ms ({args.Outcome.Exception?.GetType().Name})");
                return ValueTask.CompletedTask;
            }
        });
});

builder.Services.AddHttpClient<ICertificateService, CertificateService>((sp,
client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>().GetValue<string>("TmsApi:PublicBaseUrl")
        ?? "https://localhost:5294";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpClient("SmsService", client =>
{
    client.BaseAddress = new Uri("https://sms.tms.internal");
})
.AddStandardResilienceHandler();


builder.Services.AddHealthChecks()
.AddCheck("self", () => HealthCheckResult.Healthy("alive"),
tags: ["live"])
.AddNpgSql(
connectionString: builder.Configuration.GetConnectionString("Tms")!,
name: "postgres",
tags: ["ready"]);

builder.Logging.AddJsonConsole(options =>
{
    options.IncludeScopes = true;
    options.JsonWriterOptions = new() { Indented = false };
});




builder.Services.AddOpenTelemetry()
.ConfigureResource(r => r.AddService(serviceName: ServiceName,
serviceVersion: "1.0.0"))
.WithTracing(t => t
        .AddSource(ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(m => m
        .AddMeter(ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddSignalR();
//MODULE 8





var app = builder.Build();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
}).DisableRateLimiting();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).DisableRateLimiting();

app.MapHub<TmsHub>("/hubs/tms");

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
app.UseMiddleware<V1DeprecationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle("TMS API Reference")
        .WithTheme(ScalarTheme.DeepSpace)
        .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);

        options.AddDocument("v1", "API Version 1.0")
        .AddDocument("v2", "API Version");
    });

}


app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
//app.MapHealthChecks("/health/live").DisableRateLimiting();
//app.MapHealthChecks("/health/ready").DisableRateLimiting();


//module 8



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

var attempts = 0;
app.MapPost("/fake/certificates", async () =>
{
    var n = Interlocked.Increment(ref attempts);
    if (n % 7 == 0)
    {
        await Task.Delay(TimeSpan.FromSeconds(20));
        return Results.Ok(new { Status = "issued", Attempt = n });
    }
    if (n % 3 != 0)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    if (n % 11 == 0)
    {
        return Results.BadRequest(new { error = "validation_failed" });
    }
    return Results.Ok(new { Status = "issued", Attempt = n });
}).WithTags("lab-fixtures");

app.Run();

//public record Student(string Id, string Name);
//public record Course(string CourseCode, string Name);