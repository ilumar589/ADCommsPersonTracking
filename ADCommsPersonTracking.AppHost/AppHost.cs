var builder = DistributedApplication.CreateBuilder(args);

// Get the absolute path to the models directory
var modelsPath = Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "..", "models"));

// Add YOLO11 model export container
// This container runs once to download and export the YOLO11n model to ONNX format
// It uses a bind mount to write the model to the shared models directory
var yoloModelExport = builder.AddDockerfile("yolo-model-export", "../docker/yolo-model-export")
    .WithBindMount(modelsPath, "/models");

// Add Azure Blob Storage emulator (Azurite)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Add Redis for caching
var redis = builder.AddRedis("redis");

// Add API with reference to the model path
// The API will read the ONNX model from the shared models directory
// Since the API is a .NET project (not a container), it accesses the file system directly
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithEnvironment("ObjectDetection__ModelPath", Path.Combine(modelsPath, "yolo11n.onnx"))
    .WithReference(blobs)
    .WithReference(redis)
    .WithExternalHttpEndpoints();

// Add Web UI with reference to API
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
