var builder = DistributedApplication.CreateBuilder(args);

// Add YOLO11 container using ultralytics image
var yolo11 = builder.AddContainer("yolo11", "ultralytics/ultralytics")
    .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "http")
    .WithArgs("yolo", "serve", "model=yolo11n.pt", "imgsz=640");

// Add API with reference to YOLO11
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithReference(yolo11.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

// Add Web UI with reference to API
builder.AddProject<Projects.ADCommsPersonTracking_Web>("adcommspersontracking-web")
    .WithReference(api)
    .WithExternalHttpEndpoints();

builder.Build().Run();
