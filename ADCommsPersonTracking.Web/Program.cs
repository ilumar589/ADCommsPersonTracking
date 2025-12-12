using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ADCommsPersonTracking.Web;
using ADCommsPersonTracking.Web.Services;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
builder.Services.AddScoped<IPersonTrackingApiService, PersonTrackingApiService>();
builder.Services.AddHttpClient<IPersonTrackingApiService, PersonTrackingApiService>(client =>
{
    // For Aspire, the base address will be set via configuration at runtime
    // Fallback to localhost:5000 for standalone development
    var apiBaseAddress = builder.Configuration["services:api:http:0"] ?? builder.Configuration["services:api:https:0"] ?? "http://localhost:5000";
    client.BaseAddress = new Uri(apiBaseAddress);
});

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
