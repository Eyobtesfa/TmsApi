using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Controllers;

[ApiController]
[Route("api/test")]
public class TestController(TmsDbContext context) : ControllerBase
{
    private static bool IsHonorRoll(decimal gpa)
    {
        return gpa >= 3.5m;
    }
    [HttpGet("deferred")]
    public IActionResult TestDeferred()
    {
        Console.WriteLine("\n>>> STEP 1: Building the query object (no database contact)...");
        var query = context.Students.Where(s => s.GPA >= 3.0m);

        Console.WriteLine(">>> STEP 2: Appending a sorting clause...");
        var orderedQuery = query.OrderBy(s => s.Name);

        Console.WriteLine(">>> STEP 3: Materializing query into a C# List...");
        var results = orderedQuery.ToList(); // Execution is triggered here on Postgres

        Console.WriteLine(">>> STEP 4: Materialization finished. List populated.\n");
        return Ok(results);
    }
    [HttpGet("translation-fail")]
    public IActionResult TestTranslationFail()
    {
        Console.WriteLine("\n>>> STEP 1: Running non-translatable query...");
        try
        {
            var students = context.Students
                .Where(s => IsHonorRoll(s.GPA)) // This line relies on the method above
                .ToList();

            return Ok(students);
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>> EXCEPTION CAUGHT: {ex.Message}\n");
            return BadRequest(new { Message = ex.Message });
        }
    }
    [HttpGet("client-side")]
    public IActionResult TestClientSide()
    {
        Console.WriteLine("\n>>> STEP 1: Fetching ALL rows from the database via AsEnumerable()...");
        var queryableDataset = context.Students.AsEnumerable();
        Console.WriteLine(">>> STEP 2: Applying the un-translatable C# method in memory...");
        var filteredResults = queryableDataset
                .Where(s => IsHonorRoll(s.GPA))
                .ToList();
        Console.WriteLine(">>> STEP 3: In-memory filtering finished.\n");
        return Ok(filteredResults);

    }
}