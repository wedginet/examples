using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

// GraphQL query request class
public class GraphqlQuery
{
    public string Query { get; set; }
    public object Variables { get; set; }
}

public async Task<string> CallGraphqlServiceAsync(string token, string graphqlEndpoint)
{
    // Prepare HttpClient
    using (var httpClient = new HttpClient())
    {
        // Set up the authorization header with the Bearer token
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Define your GraphQL query
        var query = new GraphqlQuery
        {
            Query = @"query GetData($param1: String!) { 
                         data(param: $param1) { 
                            field1 
                            field2 
                         } 
                      }",
            Variables = new { param1 = "value1" }
        };

        // Serialize the query object to JSON
        var content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");

        // Post the query to the GraphQL endpoint
        var response = await httpClient.PostAsync(graphqlEndpoint, content);

        // Check if the response is successful
        response.EnsureSuccessStatusCode();

        // Return the JSON result as a string
        return await response.Content.ReadAsStringAsync();
    }
}
