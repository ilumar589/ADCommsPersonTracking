using ADCommsPersonTracking.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure HttpClient for YOLO11 service
builder.Services.AddHttpClient("Yolo11", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register detection services
// Register both HTTP and ONNX services as concrete types
builder.Services.AddSingleton<Yolo11HttpService>();
builder.Services.AddSingleton<ObjectDetectionService>();

// Register the composite service as the IObjectDetectionService implementation
// This provides automatic fallback from HTTP to ONNX
builder.Services.AddSingleton<IObjectDetectionService, CompositeObjectDetectionService>();

// Register other application services
builder.Services.AddSingleton<IPromptFeatureExtractor, PromptFeatureExtractor>();
builder.Services.AddSingleton<IImageAnnotationService, ImageAnnotationService>();
builder.Services.AddSingleton<IColorAnalysisService, ColorAnalysisService>();
builder.Services.AddSingleton<IPersonTrackingService, PersonTrackingService>();

// Add Azure Blob Storage client
builder.AddAzureBlobServiceClient("blobs");

// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Register video processing services
builder.Services.AddSingleton<IVideoProcessingService, VideoProcessingService>();
builder.Services.AddSingleton<IFrameStorageService, FrameStorageService>();
builder.Services.AddSingleton<IVideoCacheService, VideoCacheService>();

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
