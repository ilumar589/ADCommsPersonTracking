# .NET Aspire Orchestration

This document describes the .NET Aspire setup for the ADComms Person Tracking System.

## Overview

The solution uses .NET Aspire 13 to orchestrate three main components:
1. **ADCommsPersonTracking.Api** - ASP.NET Core Web API for person detection and tracking
2. **ADCommsPersonTracking.Web** - Blazor WebAssembly UI for visualization
3. **YOLO11 Container** - Docker container running the ultralytics/ultralytics image for object detection

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
├── ADCommsPersonTracking.Web/               # Blazor WebAssembly UI
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

### YOLO11 Docker Container

The YOLO11 container configuration:
- **Image**: `ultralytics/ultralytics`
- **Port**: 8000 (HTTP endpoint for YOLO inference API)
- **Command**: `yolo serve model=yolo11n.pt imgsz=640`
- **Purpose**: Provides object detection capabilities via REST API

The container is automatically started when the AppHost runs and is accessible to the API service via service discovery.

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
   - **YOLO11 Container**: Check the dashboard for the container status

### What Happens When You Run

1. **Aspire Dashboard starts**: A web-based dashboard for monitoring all services
2. **YOLO11 container launches**: Docker pulls and starts the ultralytics/ultralytics image
3. **API service starts**: The ASP.NET Core API with health checks and telemetry
4. **Web UI starts**: The Blazor WebAssembly application
5. **Service discovery configures endpoints**: All services can communicate via named endpoints

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

### API → YOLO11 Container

The API service receives the YOLO11 endpoint via environment variable:
```
services__yolo11__http__0 = http://yolo11:8000
```

The API can then make HTTP requests to `http://yolo11:8000` for object detection.

### Web UI → API

The Web UI receives the API endpoint via environment variable:
```
services__api__http__0 = http://api:5000
```

The Blazor WASM app configures its `HttpClient` to use this endpoint.

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

### Docker Container Not Starting

**Problem**: YOLO11 container fails to start

**Solutions**:
1. Ensure Docker Desktop is running
2. Check if port 8000 is available
3. Pull the image manually: `docker pull ultralytics/ultralytics`
4. Check Aspire Dashboard logs for error messages

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

### Custom Ports

To change the YOLO11 container port, modify `AppHost/Program.cs`:

```csharp
var yolo11 = builder.AddContainer("yolo11", "ultralytics/ultralytics")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "http")  // Changed from 8000
    .WithArgs("yolo", "serve", "model=yolo11n.pt", "imgsz=640");
```

### Different YOLO Model

To use a different YOLO11 model size, update the `model` argument:

```csharp
.WithArgs("yolo", "serve", "model=yolo11s.pt", "imgsz=640")  // Small model
.WithArgs("yolo", "serve", "model=yolo11m.pt", "imgsz=640")  // Medium model
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

# Terminal 3: Start YOLO11
docker run -p 8000:8000 ultralytics/ultralytics yolo serve
```

### After (Aspire)
```bash
# Single terminal
cd ADCommsPersonTracking.AppHost
dotnet run
```

All services start automatically and communicate via service discovery!

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire GitHub Repository](https://github.com/dotnet/aspire)
- [OpenTelemetry](https://opentelemetry.io/)
- [YOLO11 Documentation](https://docs.ultralytics.com/)
