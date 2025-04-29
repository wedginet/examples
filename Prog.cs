using LookupAdminApp.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("app");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// register your lookup service
builder.Services.AddScoped<ILookupSpeciesService, LookupSpeciesService>();

await builder.Build().RunAsync();
