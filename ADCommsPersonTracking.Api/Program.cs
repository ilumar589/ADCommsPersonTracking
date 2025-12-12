using ADCommsPersonTracking.Api.Services;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Wait for YOLO model file to be available before proceeding
// The yolo-model-export container will export the model and exit
await WaitForModelFileAsync(builder.Configuration, builder);

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

static async Task WaitForModelFileAsync(IConfiguration configuration, WebApplicationBuilder builder)
{
    var modelPath = configuration["ObjectDetection__ModelPath"];
    if (string.IsNullOrEmpty(modelPath))
    {
        return; // No model path configured, skip validation
    }

    // Create a logger for startup validation
    using var loggerFactory = LoggerFactory.Create(logBuilder => 
    {
        logBuilder.AddConsole();
        logBuilder.SetMinimumLevel(LogLevel.Information);
    });
    var logger = loggerFactory.CreateLogger("Startup");

    var timeout = TimeSpan.FromMinutes(5); // Reasonable timeout for model export
    var checkInterval = TimeSpan.FromSeconds(2);
    var stopwatch = Stopwatch.StartNew();

    logger.LogInformation("Waiting for YOLO model file at: {ModelPath}", modelPath);

    while (!File.Exists(modelPath))
    {
        if (stopwatch.Elapsed > timeout)
        {
            logger.LogWarning(
                "Model file not found at {ModelPath} after {Timeout} seconds. " +
                "The API will start but object detection will not work until the model is available. " +
                "Ensure the yolo-model-export container has completed successfully.",
                modelPath, timeout.TotalSeconds);
            return;
        }

        logger.LogInformation(
            "Model file not yet available at {ModelPath}. Waiting... (elapsed: {Elapsed:0.0}s)",
            modelPath, stopwatch.Elapsed.TotalSeconds);
        
        await Task.Delay(checkInterval);
    }

    var fileInfo = new FileInfo(modelPath);
    logger.LogInformation(
        "Model file found at {ModelPath} ({Size:F1} MB) after {Elapsed:0.1}s",
        modelPath, fileInfo.Length / 1024.0 / 1024.0, stopwatch.Elapsed.TotalSeconds);
}
