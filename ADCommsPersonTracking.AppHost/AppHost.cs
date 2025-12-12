using System.IO;

var builder = DistributedApplication.CreateBuilder(args);

// Read YOLO11 configuration from appsettings
var yoloConfig = builder.Configuration.GetSection("Yolo11");
var yoloModel = yoloConfig["Model"] ?? "yolo11n.onnx";

// Define shared model directory path (relative to solution root)
var modelsPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "models"));
Directory.CreateDirectory(modelsPath);

// Add YOLO11 model export container (runs once to export the model to shared volume)
var yoloModelExport = builder.AddContainer("yolo-model-export", "yolo-model-export")
    .WithBindMount(modelsPath, "/models");

// Add Azure Blob Storage emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Add Redis for caching
var redis = builder.AddRedis("redis");

// Add API with model path configured via environment variable
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithEnvironment("ObjectDetection__ModelPath", Path.Combine(modelsPath, yoloModel))
    .WithReference(blobs)
    .WithReference(redis)
    .WaitFor(yoloModelExport)
    .WithExternalHttpEndpoints();

// Add Web UI with reference to API
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
