using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Treningsparker.Api.Data;

namespace Treningsparker.Api.Security;

/// <summary>Claim names as they appear in the token; inbound claim mapping is off, so code reads these directly.</summary>
public static class AppClaims
{
    public const string UserId = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = "name";
    public const string Role = "role";
    public const string AdminRole = "Admin";

    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(UserId), out var id) ? id : null;
}

public class TokenService(IConfiguration config, TimeProvider time)
{
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey SigningKey(IConfiguration config) =>
        new(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));

    public string CreateToken(User user)
    {
        var claims = new Dictionary<string, object>
        {
            [AppClaims.UserId] = user.Id.ToString(),
            [AppClaims.Email] = user.Email,
            [AppClaims.Name] = user.DisplayName,
        };
        if (user.IsAdmin) claims[AppClaims.Role] = AppClaims.AdminRole;

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            Claims = claims,
            Expires = time.GetUtcNow().UtcDateTime.AddHours(config.GetValue("Jwt:LifetimeHours", 12)),
            SigningCredentials = new SigningCredentials(SigningKey(config), SecurityAlgorithms.HmacSha256),
        });
    }
}
