using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VmAcme.Users;

namespace VmAcme.Authentication;

public sealed class TokenProvider(JwtOptions options, TimeProvider time) : ITokenProvider
{
    public AccessToken Create(User user)
    {
        var now = time.GetUtcNow();
        var expires = now.AddMinutes(options.ExpirationMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ],
            now.UtcDateTime, expires.UtcDateTime, credentials);
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}