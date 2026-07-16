using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

public class TrainingAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TrainingAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // If the request doesn't have the custom header, fail authentication (401)
        if (!Request.Headers.ContainsKey("X-Training-User"))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing training user header."));
        }

        // If the header is present, read the username from it and create a user identity
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, Request.Headers["X-Training-User"]!)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // Return a successful authentication result with our temporary ticket
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}