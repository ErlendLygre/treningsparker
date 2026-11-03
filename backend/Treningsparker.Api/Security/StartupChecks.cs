using System.Text;

namespace Treningsparker.Api.Security;

public static class StartupChecks
{
    // Values that are published in this repository and must never protect a real deployment.
    private const string PublicJwtKey = "CHANGE_ME_development_only_key_at_least_32_chars";
    private const string PublicAdminPassword = "Admin123!";

    /// <summary>
    /// Refuses to start outside Development when the signing key or admin password is missing or is one of
    /// the public defaults, so a missing secret can't silently turn into "anyone can become admin".
    /// Set AllowInsecureDefaults=true to opt out (local docker compose only).
    /// </summary>
    public static void EnsureSecretsConfigured(IConfiguration config, IHostEnvironment env)
    {
        if (env.IsDevelopment() || config.GetValue<bool>("AllowInsecureDefaults")) return;

        var problems = new List<string>();
        var jwtKey = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey == PublicJwtKey)
            problems.Add("Jwt__Key is missing or uses the public default");
        else if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
            problems.Add("Jwt__Key must be at least 32 characters");

        var adminPassword = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(adminPassword) || adminPassword == PublicAdminPassword)
            problems.Add("Admin__Password is missing or uses the public default");

        if (problems.Count > 0)
            throw new InvalidOperationException(
                $"Refusing to start: {string.Join("; ", problems)}. Set these as secrets in the environment.");
    }
}
