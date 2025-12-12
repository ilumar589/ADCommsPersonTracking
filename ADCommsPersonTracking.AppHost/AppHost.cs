var builder = DistributedApplication.CreateBuilder(args);

// Create a volume for sharing the ONNX model between containers
var modelsVolume = builder.AddVolume("models");

// Add YOLO model export container (init container pattern)
// This container downloads the YOLO11n model and exports it to ONNX format
var modelExport = builder.AddDockerfile("yolo-model-export", "../docker/yolo-model-export")
    .WithVolume(modelsVolume, "/models");

// Add Azure Blob Storage emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Add Redis for caching
var redis = builder.AddRedis("redis");

// Add API with reference to the model volume
// The API will read the ONNX model from the shared volume
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithReference(blobs)
    .WithReference(redis)
    .WithVolume(modelsVolume, "/app/models")
    .WithEnvironment("ObjectDetection__ModelPath", "/app/models/yolo11n.onnx")
    .WaitFor(modelExport)
    .WithExternalHttpEndpoints();

// Add Web UI with reference to API
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
