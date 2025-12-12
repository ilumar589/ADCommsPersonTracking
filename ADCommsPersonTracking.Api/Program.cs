using ADCommsPersonTracking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults (Aspire)
builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Register application services
builder.Services.AddSingleton<IPromptFeatureExtractor, PromptFeatureExtractor>();
builder.Services.AddSingleton<IImageAnnotationService, ImageAnnotationService>();
builder.Services.AddSingleton<IObjectDetectionService, ObjectDetectionService>();
builder.Services.AddSingleton<IColorAnalysisService, ColorAnalysisService>();
builder.Services.AddSingleton<IPersonTrackingService, PersonTrackingService>();

// Add CORS for web clients
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Map default endpoints (Aspire)
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();
