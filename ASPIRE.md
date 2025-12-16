# .NET Aspire Orchestration

This document describes the .NET Aspire setup for the ADComms Person Tracking System.

## Overview

The solution uses .NET Aspire 13 to orchestrate two main components:
1. **ADCommsPersonTracking.Api** - ASP.NET Core Web API for person detection and tracking
2. **ADCommsPersonTracking.Web** - Blazor Web App with Interactive Server rendering for visualization

Additionally, Aspire manages infrastructure services including Azure Storage emulator (Azurite), Redis cache, and a one-time YOLO model export container.

## Project Structure

```
ADCommsPersonTracking/
├── ADCommsPersonTracking.AppHost/           # Aspire orchestration host
│   ├── Program.cs                           # Service configuration
│   ├── appsettings.json                     # Aspire settings
│   └── Properties/launchSettings.json       # Launch profiles
├── ADCommsPersonTracking.ServiceDefaults/   # Shared Aspire configurations
│   └── Extensions.cs                        # OpenTelemetry, health checks, service discovery
├── ADCommsPersonTracking.Api/               # API service
├── ADCommsPersonTracking.Web/               # Blazor Web App with Interactive Server
└── ADCommsPersonTracking.Tests/             # Unit and integration tests
```

## Components

### AppHost Project

The AppHost project (`ADCommsPersonTracking.AppHost`) is the entry point for running the entire application stack. It:

- Defines all service dependencies
- Configures Docker containers
- Sets up service discovery endpoints
- Manages environment variables for inter-service communication

Key features:
- **Service Discovery**: Automatic endpoint resolution between services
- **Container Orchestration**: Manages the YOLO11 Docker container lifecycle
- **Configuration Management**: Injects environment variables for service-to-service communication

### ServiceDefaults Project

The ServiceDefaults project (`ADCommsPersonTracking.ServiceDefaults`) provides shared configurations for all services:

#### OpenTelemetry Integration
- **Tracing**: Distributed tracing across all services
- **Metrics**: Runtime, HTTP, and ASP.NET Core metrics
- **Logging**: Structured logging with OpenTelemetry Protocol (OTLP) export

#### Health Checks
- `/health` - Overall health status
- `/alive` - Liveness probe for Kubernetes/container orchestration

#### Resilience Patterns
- **Standard Resilience Handler**: Automatic retry policies and circuit breakers for HTTP clients
- **Service Discovery**: Automatic endpoint resolution for inter-service calls

#### Service Discovery
- Enables automatic service endpoint resolution
- Configures HTTP clients to use service discovery by default

### Model Export Containers

The application uses two model export containers:

#### YOLO11 Model Export Container
- **Image**: `yolo-model-export` (custom built image)
- **Purpose**: One-time export of YOLO11 model to ONNX format
- **Operation**: Runs once, exports `yolo11x.onnx` to the shared `models/` directory, then exits
- **Model**: YOLO11x trained on COCO dataset for person and accessory detection

#### Fashion Model Export Container
- **Image**: `fashion-model-export` (custom built image)
- **Purpose**: One-time export of Fashionpedia-trained YOLO model to ONNX format
- **Operation**: Runs once, exports `fashion-yolo.onnx` to the shared `models/` directory, then exits
- **Model**: keremberke/yolov8m-fashion-detection from HuggingFace
  - **Dataset**: Fashionpedia
  - **Classes**: 27 fashion categories (shirt, jacket, pants, dress, hat, glasses, bag, etc.)
  - **Size**: ~50 MB
  - **Confidence Threshold**: 0.3 (optimized for fashion detection)

The API service uses local ONNX Runtime for inference, loading both models from the shared volume.

### Enhanced Color+Clothing Detection Flow

The system implements an advanced pipeline for accurate color+clothing queries:

1. **Person Detection** (YOLO11):
   - Detects persons in the frame
   - Also detects accessories (backpack, handbag, suitcase, etc.)

2. **Clothing Detection** (Fashionpedia Model):
   - Runs on cropped person image
   - Detects clothing items **with bounding boxes**
   - Returns 27 possible clothing/accessory categories

3. **Color Analysis on Clothing Regions**:
   - For each detected clothing item:
     - Analyzes colors **specifically on that item's bounding box**
     - Returns top 3 dominant colors
   - Creates `ClothingWithColors` objects linking each item to its colors

4. **Intelligent Matching**:
   - For queries like "blue jacket":
     - Checks if "jacket" was detected by fashion model
     - Checks if "blue" is in the jacket's color list
     - Matches only if **both** criteria are met on the same item
   - For queries with just color OR just clothing, uses original matching logic
   - For accessory queries (backpack, handbag), uses YOLO11 detections

This approach is much more accurate than analyzing colors on the entire person, especially for queries combining color and clothing type.

## Running the Application

### Prerequisites

1. **.NET 10 SDK** installed
2. **Docker Desktop** running (for YOLO11 container)
3. **Git** (for cloning the repository)

### Steps

1. **Clone the repository**:
   ```bash
   git clone https://github.com/ilumar589/ADCommsPersonTracking.git
   cd ADCommsPersonTracking
   ```

2. **Start Docker Desktop** (ensure it's running)

3. **Run the AppHost**:
   ```bash
   cd ADCommsPersonTracking.AppHost
   dotnet run
   ```

4. **Access the services**:
   - **Aspire Dashboard**: Opens automatically in your browser (usually `http://localhost:15000`)
   - **API Service**: Check the dashboard for the assigned endpoint
   - **Web UI**: Check the dashboard for the assigned endpoint
   - **Model Export Containers**: Check the dashboard for the container status (YOLO11 and fashion model)

### What Happens When You Run

1. **Aspire Dashboard starts**: A web-based dashboard for monitoring all services
2. **Infrastructure services launch**: Redis cache and Azure Storage emulator (Azurite) start
3. **Model export containers run**: 
   - **YOLO model export container**: Exports the YOLO11x model to ONNX format, then exits
   - **Fashion model export container**: Exports the fashion-trained YOLO model to ONNX format, then exits
4. **API service starts**: The ASP.NET Core API with health checks and telemetry, using local ONNX inference for both models
5. **Web UI starts**: The Blazor Web App with Interactive Server rendering
6. **Service discovery configures endpoints**: All services can communicate via named endpoints

## Aspire Dashboard Features

The Aspire Dashboard provides:

### Resources View
- Lists all running services and containers
- Shows health status (healthy/unhealthy)
- Displays resource utilization (CPU, memory)
- Container status and logs

### Traces View
- Distributed tracing across all services
- Request flow visualization
- Performance bottleneck identification

### Metrics View
- HTTP request rates and latencies
- Runtime metrics (GC, thread pool)
- Custom application metrics

### Logs View
- Centralized logging from all services
- Filterable by service, log level, and time
- Real-time log streaming

### Environment Variables
- View all environment variables injected into services
- Service discovery endpoints
- Configuration values

## Service Communication

### API → ONNX Models

The API service receives model paths via environment variables:

#### YOLO11 Model (Person Detection)
```
ObjectDetection__ModelPath = /path/to/models/yolo11x.onnx
```

The API loads the YOLO11 ONNX model and performs local inference using ONNX Runtime for person detection and limited accessory detection (backpack, handbag, suitcase, tie).

#### Fashion Model (Clothing Detection)
```
ClothingDetection__Enabled = true
ClothingDetection__ModelPath = /path/to/models/fashion-yolo.onnx
ClothingDetection__ConfidenceThreshold = 0.5
```

The API also loads an optional fashion-trained YOLO model for detecting specific clothing items (jacket, shirt, pants, dress, etc.). This model runs inference on cropped person images for accurate clothing classification.

**How Both Models Work Together:**
- **YOLO11 model** (`ObjectDetectionService`) detects persons and limited accessories (backpack, handbag, suitcase, tie)
- **Fashion model** (`ClothingDetectionService`) detects specific clothing items on cropped person regions
- **AccessoryDetectionService** combines results from both models to match search criteria
- When searching for criteria like "person with blue jacket", both models contribute to finding matches:
  - YOLO11 finds the person bounding box
  - Fashion model identifies the jacket on that person
  - Color analysis determines if the jacket is blue

### Web UI → API

The Web UI uses Aspire service discovery to connect to the API:
- The Blazor Web App runs on the server with Interactive Server rendering
- HttpClient is configured with service discovery using the service name `http://adcommspersontracking-api`
- API calls are made server-side, allowing proper use of Aspire's internal service URLs
- SignalR maintains real-time communication between server and browser for interactive components

## Configuration Files

### AppHost/appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Aspire.Hosting.Dcp": "Warning"
    }
  }
}
```

### ServiceDefaults/Extensions.cs

Key extension methods:
- `AddServiceDefaults()` - Adds OpenTelemetry, health checks, and service discovery
- `ConfigureOpenTelemetry()` - Configures tracing, metrics, and logging
- `MapDefaultEndpoints()` - Maps health check endpoints

## Troubleshooting

### Model Export Containers Not Starting

**Problem**: YOLO or fashion model export containers fail to start or complete

**Solutions**:
1. Ensure Docker Desktop is running
2. Build the export images:
   ```bash
   cd docker/yolo-model-export
   docker build -t yolo-model-export .
   cd ../fashion-model-export
   docker build -t fashion-model-export .
   ```
3. Check Aspire Dashboard logs for error messages
4. Verify the `models/` directory is created and accessible

### Fashion Model Not Working

**Problem**: Clothing detection is not working or returns empty results

**Solutions**:
1. Check that the fashion model container has run and exported the model:
   - Look in `models/fashion-yolo.onnx` to verify the file exists
   - Check the Aspire Dashboard logs for the fashion-model-export container
2. Verify the environment variables are set in the Aspire Dashboard:
   - `ClothingDetection__Enabled` should be `true`
   - `ClothingDetection__ModelPath` should point to the fashion model
   - `ClothingDetection__ConfidenceThreshold` should be set (default: `0.5`)
3. Check the API logs for clothing detection initialization messages
4. Note: The placeholder fashion model (YOLOv8n) will NOT detect clothing correctly. For production use, you need a fashion-trained YOLO model trained on DeepFashion2 or similar datasets.

### Service Discovery Not Working

**Problem**: Services cannot communicate with each other

**Solutions**:
1. Check the Aspire Dashboard for endpoint assignments
2. Verify environment variables in the Dashboard
3. Restart the AppHost
4. Check for port conflicts

### Build Errors

**Problem**: Build fails with Aspire-related errors

**Solutions**:
1. Ensure .NET 10 SDK is installed: `dotnet --version`
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Check NuGet package versions are compatible
4. Restore packages: `dotnet restore`

### Aspire Dashboard Not Opening

**Problem**: Dashboard doesn't open in browser

**Solutions**:
1. Manually navigate to `http://localhost:15000` or `https://localhost:17000`
2. Check console output for the actual dashboard URL
3. Verify no other service is using ports 15000/17000

## Advanced Configuration

### Different YOLO Model

The default model is YOLO11x (extra-large) for best accuracy. To use a different YOLO11 model size, update the model export script to export a different model variant (yolo11n, yolo11s, yolo11m, or yolo11l), then update the `ModelPath` configuration in `appsettings.json`:

```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11s.onnx"  // Example: using smaller model for faster inference
  }
}
```

### Environment-Specific Configuration

Create `appsettings.Development.json` or `appsettings.Production.json` in the AppHost project for environment-specific settings.

### Add Additional Services

To add a new service (e.g., a database):

```csharp
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var apiService = builder.AddProject("api", "../ADCommsPersonTracking.Api/ADCommsPersonTracking.Api.csproj")
    .WithReference(postgres)  // Add reference to the database
    .WithExternalHttpEndpoints();
```

## Benefits of Using Aspire

1. **Simplified Local Development**: One command starts all services
2. **Built-in Observability**: Tracing, metrics, and logging out-of-the-box
3. **Service Discovery**: No need to hardcode endpoints
4. **Health Monitoring**: Automatic health checks for all services
5. **Container Management**: Automatic Docker container lifecycle management
6. **Configuration Management**: Centralized environment variable injection
7. **Production Ready**: Same patterns work in production with Kubernetes

## Migration from Standalone

If you were running services independently before:

### Before (Standalone)
```bash
# Terminal 1: Start API
cd ADCommsPersonTracking.Api
dotnet run

# Terminal 2: Start Web
cd ADCommsPersonTracking.Web
dotnet run

# Terminal 3: Export YOLO model (one-time)
cd docker/yolo-model-export
docker build -t yolo-model-export .
docker run --rm -v "$(pwd)/../../models:/models" yolo-model-export:latest
```

### After (Aspire)
```bash
# Single terminal
cd ADCommsPersonTracking.AppHost
dotnet run
```

All services start automatically, the model is exported if needed, and services communicate via service discovery!

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire GitHub Repository](https://github.com/dotnet/aspire)
- [OpenTelemetry](https://opentelemetry.io/)
- [YOLO11 Documentation](https://docs.ultralytics.com/)
