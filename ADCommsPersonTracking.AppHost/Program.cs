var builder = DistributedApplication.CreateBuilder(args);

// Add YOLO11 Docker container
var yolo11 = builder.AddContainer("yolo11", "ultralytics/ultralytics")
    .WithHttpEndpoint(port: 8000, targetPort: 8000, name: "http")
    .WithArgs("yolo", "serve", "model=yolo11n.pt", "imgsz=640");

// Add API project
var apiService = builder.AddProject("api", "../ADCommsPersonTracking.Api/ADCommsPersonTracking.Api.csproj")
    .WithEnvironment("services__yolo11__http__0", yolo11.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

// Add Web project
builder.AddProject("web", "../ADCommsPersonTracking.Web/ADCommsPersonTracking.Web.csproj")
    .WithEnvironment("services__api__http__0", apiService.GetEndpoint("http"))
    .WithExternalHttpEndpoints();

builder.Build().Run();
