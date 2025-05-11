using LookupAdmin.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// point HttpClient at your API:
builder.Services.AddHttpClient<ILookupSpeciesService, LookupSpeciesService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:5110/");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
