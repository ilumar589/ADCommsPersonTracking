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
    // Default to localhost:5000 for development, but this can be configured via appsettings.json
    client.BaseAddress = new Uri("http://localhost:5000/");
});

// Add MudBlazor services
builder.Services.AddMudServices();

await builder.Build().RunAsync();
