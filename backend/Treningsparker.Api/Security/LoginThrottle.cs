using System.Collections.Concurrent;

namespace Treningsparker.Api.Security;

/// <summary>
/// Blocks further login attempts for an account after too many failed passwords, to stop password guessing
/// even when the attacker spreads requests over many IP addresses. In memory, so per instance.
/// </summary>
public class LoginThrottle(IConfiguration config, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, (int Failures, DateTimeOffset WindowStart)> _attempts = new();
    private int MaxFailures => config.GetValue("RateLimiting:LoginFailuresPerAccount", 10);
    private TimeSpan Window => TimeSpan.FromMinutes(config.GetValue("RateLimiting:LoginLockoutMinutes", 15));

    public bool IsLockedOut(string email) =>
        _attempts.TryGetValue(email, out var entry)
        && time.GetUtcNow() - entry.WindowStart < Window
        && entry.Failures >= MaxFailures;

    public void RecordFailure(string email)
    {
        var now = time.GetUtcNow();
        _attempts.AddOrUpdate(email, (1, now), (_, entry) =>
            now - entry.WindowStart >= Window ? (1, now) : (entry.Failures + 1, entry.WindowStart));
    }

    public void Reset(string email) => _attempts.TryRemove(email, out _);
}
