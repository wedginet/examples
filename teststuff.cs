public async Task<User> Authenticate()
{
    if (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS)
    {
        // Use WebAuthenticator for iOS and Android
        var authResult = await WebAuthenticator.AuthenticateAsync(new WebAuthenticatorOptions
        {
            Url = new Uri("https://your-auth-url.com"), // replace with your Auth URL
            CallbackUrl = new Uri("your-app-scheme://callback") // replace with your callback scheme
        });

        // Extracting tokens and claims from the authResult
        var user = new User();
        
        if (authResult.Properties.TryGetValue("id_token", out var identityToken))
        {
            user.IdentityToken = identityToken;
        }

        if (authResult.Properties.TryGetValue("access_token", out var accessToken))
        {
            user.AccessToken = accessToken;
        }

        // Assuming the eAuthId and claims are also returned in properties:
        if (authResult.Properties.TryGetValue("eAuthId", out var eAuthId))
        {
            user.EAuthId = eAuthId;
        }

        // Parse claims based on your expected indices (if claims are available as properties)
        var claims = authResult.Properties;
        user.FirstName = claims.TryGetValue("first_name", out var firstName) ? firstName : string.Empty;
        user.LastName = claims.TryGetValue("last_name", out var lastName) ? lastName : string.Empty;
        user.EmailName = claims.TryGetValue("email", out var email) ? email : string.Empty;

        return user;
    }
    else
    {
        // Use OidcClient for Windows
        var result = await _oidcClient.LoginAsync();
        var user = new User
        {
            IdentityToken = result.IdentityToken,
            AccessToken = result.AccessToken,
            EAuthId = result.User.Claims.FirstOrDefault()?.Value
        };

        // Retrieve other claims
        var claims = result.Claims.ToArray();
        user.FirstName = claims.Length > 3 ? claims[3].Value : string.Empty;
        user.LastName = claims.Length > 4 ? claims[4].Value : string.Empty;
        user.EmailName = claims.Length > 5 ? claims[5].Value : string.Empty;

        return user;
    }
}
