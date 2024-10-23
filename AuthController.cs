public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container
        builder.Services.AddControllers();

        // Add Authentication services and configure JWT Bearer Authentication
        builder.Services.AddAuthentication("Bearer")
            .AddJwtBearer("Bearer", options =>
            {
                options.Authority = builder.Configuration["Authentication:OpenIdIssuer"];  // Authority of the OpenID provider
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = false,  // Set to true if your tokens have an audience
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });

        // Add Authorization policies if needed
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("TokenValidation", policy =>
                policy.RequireAuthenticatedUser());
        });

        var app = builder.Build();

        // Enable Authentication & Authorization middleware
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}


using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class TokenTestController : ControllerBase
{
    // This endpoint is protected and requires a validated token
    [HttpGet("test")]
    [Authorize(Policy = "TokenValidation")]  // Ensure only authenticated users can access
    public IActionResult GetNameFromToken()
    {
        // Retrieve the name claim from the token
        var nameClaim = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value;

        if (string.IsNullOrEmpty(nameClaim))
        {
            return Unauthorized("Token does not contain a valid 'name' claim");
        }

        return Ok(new { Name = nameClaim });
    }
}



