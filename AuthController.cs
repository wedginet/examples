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


public async Task<string> TestProtected(string accessToken)
{
    try
    {
        var path = "http://localhost:1234/api/protected";
        
        // Create a new HttpClient instance
        using (var client = new HttpClient())
        {
            // Add the access token to the Authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // Make the request
            var response = await client.GetAsync(path);

            // Ensure a successful response
            response.EnsureSuccessStatusCode();

            // Read the response content
            var result = await response.Content.ReadAsStringAsync();

            return result;  // Return the response from the protected API
        }
    }
    catch (Exception ex)
    {
        // Handle any errors that occur
        Console.WriteLine($"Error calling protected API: {ex.Message}");
        return "Error";
    }
}

