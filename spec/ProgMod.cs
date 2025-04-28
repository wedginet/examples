builder.Services.AddScoped<ILookupSpeciesService, LookupSpeciesService>();
builder.Services.AddHttpClient<ILookupSpeciesService, LookupSpeciesService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]));
