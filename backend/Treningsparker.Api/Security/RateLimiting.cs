using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Treningsparker.Api.Security;

public static class RateLimiting
{
    public const string Auth = "auth";
    public const string Submissions = "submissions";

    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LoginThrottle>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "For mange forsøk. Vent litt og prøv igjen." }, token);
            };

            // Coarse limit on login/registration per client. The web container's nginx applies a stricter
            // per-visitor limit; this one also covers calls made directly to the API.
            options.AddPolicy(Auth, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.GetValue("RateLimiting:AuthRequestsPerMinute", 100),
                    Window = TimeSpan.FromMinutes(1),
                }));

            // Park suggestions per logged-in user, so one account can't flood the admin queue.
            options.AddPolicy(Submissions, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(AppClaims.UserId) ?? ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = config.GetValue("RateLimiting:SubmissionsPerHour", 10),
                    Window = TimeSpan.FromHours(1),
                }));
        });
        return services;
    }

    // The rightmost X-Forwarded-For entry is the one added by the proxy in front of us, so it can't be
    // spoofed by the client the way the leftmost entries can.
    private static string ClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
        var last = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return last ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
