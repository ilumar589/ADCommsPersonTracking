using ADCommsPersonTracking.Web;
using ADCommsPersonTracking.Web.Components;
using ADCommsPersonTracking.Web.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (service discovery, telemetry, health checks)
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Configure HttpClient for API calls with Aspire service discovery
builder.Services.AddScoped<IPersonTrackingApiService, PersonTrackingApiService>();
builder.Services.AddHttpClient<IPersonTrackingApiService, PersonTrackingApiService>(client =>
{
    // Use service discovery to resolve the API endpoint
    // The "adcommspersontracking-api" name matches the one defined in AppHost
    client.BaseAddress = new Uri("http://adcommspersontracking-api");
})
.AddServiceDiscovery();

// Add MudBlazor services
builder.Services.AddMudServices();

var app = builder.Build();

// Map Aspire health check endpoints
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(ADCommsPersonTracking.Web._Imports).Assembly);

app.Run();
