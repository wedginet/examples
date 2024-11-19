using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

public class TokenService
{
    private readonly RSA _rsaPrivateKey;

    public TokenService(RSA rsaPrivateKey)
    {
        _rsaPrivateKey = rsaPrivateKey ?? throw new ArgumentNullException(nameof(rsaPrivateKey));
    }

    public string? GenerateAccessToken(string sub, string issuer, string audience, string firstName, string lastName, string email, string nameId, string nonce)
    {
        try
        {
            if (_rsaPrivateKey == null)
            {
                return null;
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var rsaKey = new RsaSecurityKey(_rsaPrivateKey);

            // Get the current UTC time
            var now = DateTime.UtcNow;

            // Create claims for the token
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, sub),               // Subject
                new Claim(JwtRegisteredClaimNames.Aud, audience),          // Audience
                new Claim(JwtRegisteredClaimNames.Iss, issuer),            // Issuer
                new Claim(JwtRegisteredClaimNames.Exp, 
                    new DateTimeOffset(now.AddHours(1)).ToUnixTimeSeconds().ToString()), // Expiration
                new Claim(JwtRegisteredClaimNames.Iat, 
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString()), // Issued At
                new Claim(JwtRegisteredClaimNames.AuthTime, 
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString()), // Authentication Time
                new Claim(JwtRegisteredClaimNames.Email, email),           // Email
                new Claim("nameid", nameId),                               // Name ID
                new Claim("nonce", nonce),                                 // Nonce
                new Claim("firstName", firstName),                         // First Name
                new Claim("lastName", lastName),                           // Last Name
                new Claim("isSystem", "false"),                            // isSystem
                new Claim("application", "[]"),                            // Application
                new Claim("role", "[]"),                                   // Role
                new Claim("subRole", "[]"),                                // SubRole
                new Claim("tertiaryRole", "[]")                            // TertiaryRole
            };

            // Create token descriptor
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = now.AddHours(1),
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha512)
            };

            // Create and return the token
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            // Log the exception as needed
            Console.WriteLine($"Token generation failed: {ex.Message}");
            return null;
        }
    }
}
