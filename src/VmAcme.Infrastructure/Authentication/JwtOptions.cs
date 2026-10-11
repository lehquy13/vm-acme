using System.Text;
using Microsoft.Extensions.Configuration;

namespace VmAcme.Authentication;

public sealed record JwtOptions(string Issuer, string Audience, string SigningKey, int ExpirationMinutes)
{
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Authentication:Jwt");
        var issuer = section["Issuer"];
        var audience = section["Audience"];
        var key = section["SigningKey"];
        var expiration = section.GetValue("ExpirationMinutes", 60);
        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32 || expiration is < 1 or > 1440)
            throw new InvalidOperationException(
                "Authentication:Jwt requires Issuer, Audience, a SigningKey of at least 32 UTF-8 bytes, and ExpirationMinutes between 1 and 1440.");
        return new(issuer, audience, key, expiration);
    }
}