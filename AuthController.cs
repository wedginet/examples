using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

[ApiController]
[Route("api/authentication")]
public class AuthenticationController : ControllerBase
{
    private readonly TokenService _tokenService;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthenticationController(IOptions<AuthenticationSettings> authSettings)
    {
        // Retrieve RSA private and public keys from appsettings.json
        var rsaPrivateKeyBase64 = authSettings.Value.RsaPrivateKey;
        var rsaPrivateKeyBytes = Convert.FromBase64String(rsaPrivateKeyBase64);

        var rsaPublicKeyBase64 = authSettings.Value.RsaPublicKey;
        var rsaPublicKeyBytes = Convert.FromBase64String(rsaPublicKeyBase64);

        // Initialize RSA and import the private key
        using (var rsa = RSA.Create())
        {
            rsa.ImportRSAPrivateKey(rsaPrivateKeyBytes, out _);  // Import private key
            _tokenService = new TokenService(rsa);               // Pass RSA to TokenService
        }

        // Initialize the token validation parameters (for verifying tokens)
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = authSettings.Value.Issuer,
            ValidAudience = authSettings.Value.Audience,
            IssuerSigningKey = new RsaSecurityKey(RSA.Create()) // Use RSA public key for validation
        };
    }

    [HttpPost("exchange")]
    public IActionResult ExchangeToken([FromBody] TokenRequest request)
    {
        var (validatedToken, claims) = ValidateIdentityToken(request);

        try
        {
            if (validatedToken != null && claims != null)
            {
                if (claims.Identity.IsAuthenticated)
                {
                    string userId = claims.Identity.Name;
                    var newAccessToken = _tokenService.GenerateAccessToken(userId);  // Generate token
                    return Ok(new { newAccessToken });
                }
            }
            return Unauthorized();
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }
}
