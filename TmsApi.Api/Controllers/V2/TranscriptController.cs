using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v2/transcripts")]
public class TranscriptsController : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("transcripts")] // Enforces the max 5 active constraint!
    public IActionResult RequestTranscript([FromBody] object? _)
    {
        return Ok();
    }
}