using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartTripPlanner.Tests.Helpers;

internal static class TestJwtTokenFactory
{
    private const string SecretKey = "dev-secret-key-that-is-at-least-32-bytes-long-for-hs256";
    private const string Issuer = "smart-trip-planner";
    private const string Audience = "smart-trip-planner-api";

    internal static string GetSecret() => SecretKey;

    internal static string CreateToken(string userId, int expiryHours = 1)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
