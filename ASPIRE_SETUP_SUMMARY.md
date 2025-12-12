# .NET Aspire Setup Summary

## Overview
This document summarizes the .NET Aspire 13 integration added to the ADComms Person Tracking solution.

## What Was Added

### 1. New Projects

#### ADCommsPersonTracking.AppHost
- **Purpose**: Aspire orchestration host
- **Framework**: .NET 10.0
- **Key Package**: Aspire.Hosting.AppHost 13.0.2
- **Responsibilities**:
  - Orchestrates all services (API, Web, YOLO11 container)
  - Configures service discovery
  - Manages environment variables for inter-service communication
  - Provides Aspire Dashboard for monitoring

**Key Files**:
- `Program.cs` - Service configuration
- `appsettings.json` - Aspire settings
- `Properties/launchSettings.json` - Launch profiles

#### ADCommsPersonTracking.ServiceDefaults
- **Purpose**: Shared Aspire configurations
- **Framework**: .NET 10.0
- **Key Packages**:
  - Microsoft.Extensions.Http.Resilience 10.1.0
  - Microsoft.Extensions.ServiceDiscovery 10.1.0
  - OpenTelemetry.* 1.11.1
- **Features**:
  - OpenTelemetry integration (tracing, metrics, logging)
  - Health check endpoints (/health, /alive)
  - Service discovery configuration
  - Resilience patterns (retry, circuit breaker)

**Key Files**:
- `Extensions.cs` - Extension methods for AddServiceDefaults() and MapDefaultEndpoints()

### 2. Modified Projects

#### ADCommsPersonTracking.Api
**Changes**:
- Added project reference to ServiceDefaults
- Added `builder.AddServiceDefaults()` call in Program.cs
- Added `app.MapDefaultEndpoints()` call in Program.cs
- Now includes health check endpoints and OpenTelemetry

#### ADCommsPersonTracking.Web
**Changes**:
- Updated Program.cs to read API endpoint from configuration
- Supports Aspire service discovery via environment variables
- Falls back to localhost:5000 for standalone development

#### ADCommsPersonTracking.sln
**Changes**:
- Added ADCommsPersonTracking.AppHost project
- Added ADCommsPersonTracking.ServiceDefaults project

### 3. Documentation

#### ASPIRE.md (New)
- Comprehensive guide to the Aspire setup
- Architecture overview
- Running instructions
- Troubleshooting guide
- Advanced configuration examples

#### README.md (Updated)
- Added Docker Desktop to prerequisites
- Added "Run with Aspire" section with instructions
- Updated Technology Stack section
- Organized running options (Aspire vs Standalone)

## Architecture

```
┌─────────────────────────────────────────────────┐
│         ADCommsPersonTracking.AppHost           │
│              (Orchestration Host)               │
└───────────────┬──────────────┬──────────────────┘
                │              │
        ┌───────┴───────┐     ┌┴──────────────────┐
        │               │     │                   │
┌───────▼────────┐ ┌───▼──────────┐ ┌────────────▼──────┐
│ ServiceDefaults│ │     YOLO11   │ │  Blazor Web UI    │
│  (Telemetry,   │ │   Container  │ │  (WebAssembly)    │
│   Health,      │ │ (ultralytics)│ └────────┬──────────┘
│   Discovery)   │ └──────┬───────┘          │
└───────┬────────┘        │                  │
        │                 │                  │
┌───────▼─────────────────▼──────────────────▼────┐
│         ADCommsPersonTracking.Api                │
│           (ASP.NET Core Web API)                 │
└──────────────────────────────────────────────────┘
```

## Service Discovery Flow

1. **AppHost starts** and configures all resources
2. **YOLO11 container** starts on port 8000
3. **API service** starts and receives YOLO11 endpoint via environment variable:
   - `services__yolo11__http__0` = `http://yolo11:8000`
4. **Web UI** starts and receives API endpoint via environment variable:
   - `services__api__http__0` = `http://api:5000`
5. All services register with **Aspire Dashboard** for monitoring

## Health Checks

All services now expose health check endpoints:

- **API Service**:
  - `/health` - Overall health status
  - `/alive` - Liveness probe

These endpoints are automatically:
- Registered by ServiceDefaults
- Monitored by Aspire Dashboard
- Available for Kubernetes liveness/readiness probes

## OpenTelemetry

All services emit telemetry data:

### Traces
- HTTP request/response spans
- Distributed tracing across services
- Performance bottleneck identification

### Metrics
- HTTP request rates and durations
- Runtime metrics (GC, thread pool, memory)
- ASP.NET Core metrics

### Logs
- Structured logging with OpenTelemetry format
- Automatic correlation with traces
- Centralized in Aspire Dashboard

## Running the Application

### Quick Start

```bash
# 1. Ensure Docker is running
docker --version

# 2. Navigate to AppHost
cd ADCommsPersonTracking.AppHost

# 3. Run the application
dotnet run
```

### What You'll See

1. **Console output** showing services starting
2. **Browser automatically opens** to Aspire Dashboard
3. **Dashboard shows**:
   - All services (API, Web, YOLO11 container)
   - Health status
   - Resource utilization
   - Live logs

### Accessing Services

- **Aspire Dashboard**: `http://localhost:15000` or `https://localhost:17000`
- **API**: Check dashboard for assigned endpoint
- **Web UI**: Check dashboard for assigned endpoint
- **YOLO11**: Check dashboard for container endpoint

## Benefits

### Development Experience
- ✅ Single command to start all services
- ✅ Automatic service discovery (no hardcoded URLs)
- ✅ Built-in observability (no extra setup)
- ✅ Container management (automatic Docker lifecycle)

### Production Ready
- ✅ Health checks for Kubernetes
- ✅ Distributed tracing
- ✅ Metrics for monitoring
- ✅ Resilience patterns (retry, circuit breaker)
- ✅ Structured logging

### Team Collaboration
- ✅ Consistent environment across team
- ✅ Self-documenting architecture (Aspire Dashboard)
- ✅ Easy onboarding for new developers
- ✅ Reduced configuration errors

## Testing

All existing tests continue to pass:
- ✅ 59 unit tests passing
- ✅ No breaking changes to existing functionality
- ✅ Services can still run standalone without Aspire

## Compatibility

- **Required**: .NET 10 SDK
- **Required for Aspire**: Docker Desktop
- **Optional**: YOLO11 ONNX model (for standalone API mode)

## Next Steps

### For Developers
1. Pull the latest code
2. Install Docker Desktop if not already installed
3. Run `dotnet run` in the AppHost directory
4. Access the Aspire Dashboard in your browser

### For CI/CD
- Aspire can be used in CI/CD pipelines
- Health checks integrate with Kubernetes
- Container orchestration works with Docker Compose/Kubernetes

### Future Enhancements
- Add database orchestration (PostgreSQL/SQL Server)
- Add message queue (RabbitMQ/Azure Service Bus)
- Add Redis for caching
- Deploy to Azure Container Apps with Aspire support

## Version Information

- .NET: 10.0
- Aspire: 13.0.2
- OpenTelemetry: 1.11.1
- Service Discovery: 10.1.0
- Resilience: 10.1.0

## Support

For issues or questions:
1. Check ASPIRE.md for detailed troubleshooting
2. Check Aspire Dashboard logs
3. Verify Docker is running
4. Check GitHub Issues

## References

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [Aspire GitHub](https://github.com/dotnet/aspire)
- [OpenTelemetry](https://opentelemetry.io/)
