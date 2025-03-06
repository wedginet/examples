public async Task<SyncResponse?> Sync(SyncRequest req)
{
    try
    {
        // Use relative path since _client.BaseAddress is already set from DI
        var relativePath = "ContactSync";
        
        // Serialize the request object into JSON
        var jsonPayload = JsonConvert.SerializeObject(req);
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
        
        // Configure the client settings
        _client.Timeout = TimeSpan.FromMinutes(10);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _session.MyAccessToken);
        
        // Make the POST call using the relative path
        var response = await _client.PostAsync(relativePath, content);
        
        if (response.IsSuccessStatusCode)
        {
            var resultContent = await response.Content.ReadAsStringAsync();
            // Deserialize the response if needed
            var syncResponse = JsonConvert.DeserializeObject<SyncResponse>(resultContent);
            return syncResponse;
        }
        else
        {
            // Handle non-success status codes as appropriate
            return null;
        }
    }
    catch (Exception ex)
    {
        // Handle exceptions (e.g., logging)
        return null;
    }
}
