var builder = DistributedApplication.CreateBuilder(args);

// Read YOLO11 configuration from appsettings
var yoloConfig = builder.Configuration.GetSection("Yolo11");
var yoloImage = yoloConfig["Image"] ?? "ultralytics/ultralytics";
var yoloModel = yoloConfig["Model"] ?? "yolo11n.pt";
var yoloImageSize = yoloConfig["ImageSize"] ?? "640";
var yoloPort = int.TryParse(yoloConfig["Port"], out var port) ? port : 8000;

// Add YOLO11 container using ultralytics image with configurable settings
var yolo11 = builder.AddContainer("yolo11", yoloImage)
    .WithHttpEndpoint(port: yoloPort, targetPort: yoloPort, name: "http")
    .WithArgs("yolo", "serve", $"model={yoloModel}", $"imgsz={yoloImageSize}");

// Add Azure Blob Storage emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Add Redis for caching
var redis = builder.AddRedis("redis");

// Add API with YOLO11 service discovery via connection string
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithEnvironment("ConnectionStrings__yolo11", yolo11.GetEndpoint("http"))
    .WithReference(blobs)
    .WithReference(redis)
    .WithExternalHttpEndpoints();

// Add Web UI with reference to API
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
