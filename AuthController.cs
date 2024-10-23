using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

// Load RSA public key from appsettings.json
var rsaPublicKey = Convert.FromBase64String(builder.Configuration["Authentication:RsaPublicKey"]);
var rsa = RSA.Create();
rsa.ImportRSAPublicKey(rsaPublicKey, out _);
var rsaSecurityKey = new RsaSecurityKey(rsa);

// Add services to the container.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Authentication:Issuer"],  // Issuer validation
        ValidAudience = builder.Configuration["Authentication:Audience"],  // Audience validation
        IssuerSigningKey = rsaSecurityKey  // Use the RSA public key for token validation
    };
});

// Enable authorization
builder.Services.AddAuthorization();

// Build the application
var app = builder.Build();

// Use authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
app.MapControllers();

app.Run();


[ApiController]
[Route("api/protected")]
public class ProtectedController : ControllerBase
{
    // This endpoint is locked down and requires a valid token to access
    [HttpGet]
    [Authorize]
    public IActionResult GetUserName()
    {
        // Extract the name claim from the token
        var userName = User.Identity?.Name;

        return Ok(new { Name = userName });
    }
}



