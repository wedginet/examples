using System.Text.Json;

var filePath = "users.json";
List<User> users = [];

if (File.Exists(filePath))
    users = JsonSerializer.Deserialize<List<User>>(File.ReadAllText(filePath)) ?? [];

Console.Write("Enter your name: ");
var name = Console.ReadLine()?.Trim() ?? "";

var user = users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

if (user is not null)
{
    Console.WriteLine($"Welcome back, {user.Name}! Your score is {user.Score}.");
}
else
{
    user = new User(name, 20);
    users.Add(user);
    Console.WriteLine($"Welcome, {name}! Starting score: 20.");
}

// ...play happens here, potentially modifying user.Score...

File.WriteAllText(filePath, JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine("Progress saved.");

record User(string Name, int Score);
