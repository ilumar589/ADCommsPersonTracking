# YOLO11 Configuration Guide

This document describes how to configure and use the YOLO11 object detection service in the ADComms Person Tracking System.

## Overview

The system supports three modes of YOLO11 object detection:

1. **HTTP Mode**: Uses the YOLO11 Docker container via REST API (fastest, best for production)
2. **ONNX Mode**: Uses local ONNX model inference (standalone, no Docker required)
3. **Auto Mode** (Default): Tries HTTP first, automatically falls back to ONNX if HTTP is unavailable

## Configuration

### API Service Configuration (`appsettings.json`)

```json
{
  "ObjectDetection": {
    "Mode": "auto",
    "ModelPath": "models/yolo11n.onnx",
    "ConfidenceThreshold": 0.45,
    "IouThreshold": 0.5
  },
  "Yolo11": {
    "Endpoint": "http://localhost:8000"
  }
}
```

#### Configuration Options

**ObjectDetection:Mode**
- `auto` (default): Automatic fallback from HTTP to ONNX
- `http`: HTTP-only mode (fails if container unavailable)
- `onnx`: ONNX-only mode (local inference only)

**ObjectDetection:ModelPath**
- Path to the local YOLO11 ONNX model file
- Required for ONNX and Auto modes
- Default: `models/yolo11n.onnx`

**ObjectDetection:ConfidenceThreshold**
- Minimum confidence score for detections (0.0 to 1.0)
- Default: `0.45`

**ObjectDetection:IouThreshold**
- Intersection over Union threshold for Non-Maximum Suppression
- Default: `0.5`

**Yolo11:Endpoint**
- HTTP endpoint for the YOLO11 container
- When using Aspire, this is automatically injected via service discovery
- Fallback: `http://localhost:8000`

### AppHost Configuration (`appsettings.json`)

```json
{
  "Yolo11": {
    "Image": "ultralytics/ultralytics",
    "Model": "yolo11n.pt",
    "ImageSize": 640,
    "Port": 8000
  }
}
```

#### Configuration Options

**Yolo11:Image**
- Docker image to use for YOLO11 container
- Default: `ultralytics/ultralytics`

**Yolo11:Model**
- YOLO11 model variant to use
- Options: `yolo11n.pt` (nano), `yolo11s.pt` (small), `yolo11m.pt` (medium), `yolo11l.pt` (large), `yolo11x.pt` (extra large)
- Default: `yolo11n.pt`
- Larger models are more accurate but slower

**Yolo11:ImageSize**
- Input image size for inference
- Common values: 320, 480, 640, 1280
- Default: `640`
- Larger sizes are more accurate but slower

**Yolo11:Port**
- Port to expose the YOLO11 HTTP service
- Default: `8000`

## Usage Scenarios

### Scenario 1: Running with Aspire (Recommended)

When running via Aspire AppHost, the YOLO11 container is automatically started and configured.

```bash
cd ADCommsPersonTracking.AppHost
dotnet run
```

**What happens:**
1. Aspire starts the YOLO11 Docker container
2. The container endpoint is injected into the API service via `ConnectionStrings__yolo11`
3. API automatically uses HTTP mode with fallback to ONNX if needed
4. All services communicate via Aspire service discovery

**Configuration:**
- The API automatically receives the YOLO11 endpoint from Aspire
- No manual endpoint configuration needed
- Falls back to local ONNX if container fails to start

### Scenario 2: Standalone API (Without Aspire)

Run the API independently without Aspire orchestration.

```bash
cd ADCommsPersonTracking.Api
dotnet run
```

**What happens:**
1. API starts without YOLO11 container
2. HTTP mode fails (no container running)
3. Automatically falls back to local ONNX inference
4. Works as long as ONNX model is available

**Configuration:**
```json
{
  "ObjectDetection": {
    "Mode": "onnx"
  }
}
```

### Scenario 3: Manual Docker Container

Run the YOLO11 container manually and connect the API to it.

```bash
# Terminal 1: Start YOLO11 container
docker run -p 8000:8000 ultralytics/ultralytics yolo serve model=yolo11n.pt imgsz=640

# Terminal 2: Start API
cd ADCommsPersonTracking.Api
dotnet run
```

**Configuration:**
```json
{
  "ObjectDetection": {
    "Mode": "http"
  },
  "Yolo11": {
    "Endpoint": "http://localhost:8000"
  }
}
```

### Scenario 4: Force ONNX Mode

Force the API to always use local ONNX inference, even if HTTP container is available.

**Configuration:**
```json
{
  "ObjectDetection": {
    "Mode": "onnx",
    "ModelPath": "models/yolo11n.onnx"
  }
}
```

**Use cases:**
- Testing ONNX performance
- Environments without Docker
- Offline operation
- Debugging model issues

## Service Architecture

### CompositeObjectDetectionService

The composite service intelligently manages detection:

```
┌─────────────────────────────────────────┐
│   CompositeObjectDetectionService       │
│   (IObjectDetectionService)             │
└───────────┬─────────────────────────────┘
            │
            ├──> Auto Mode
            │    ├──> Try HTTP
            │    └──> Fallback to ONNX (if HTTP fails)
            │
            ├──> HTTP Mode
            │    └──> Yolo11HttpService only
            │
            └──> ONNX Mode
                 └──> ObjectDetectionService only
```

### Automatic Fallback Logic

When in Auto mode:

1. **First HTTP Attempt**: Tries to use HTTP service
2. **If HTTP Fails**: Automatically switches to ONNX
3. **Retry Logic**: Waits 5 minutes before retrying HTTP
4. **Recovery**: Automatically switches back to HTTP when available

### Logging

The system provides comprehensive logging for detection mode:

```
[Information] Object detection mode: Auto (HTTP with ONNX fallback)
[Information] YOLO11 HTTP service configured at http://yolo11:8000
[Debug] Attempting HTTP-based detection
[Warning] HTTP-based detection failed, falling back to local ONNX inference
[Information] HTTP service recovered and is now available
```

## Performance Comparison

### HTTP Mode (YOLO11 Container)
- **Pros**: 
  - Fastest inference (~50-100ms per image)
  - Automatic model download
  - Easy updates (just change model parameter)
  - GPU acceleration (if available)
- **Cons**: 
  - Requires Docker
  - Network latency
  - Container startup time (~30 seconds)

### ONNX Mode (Local Inference)
- **Pros**:
  - No Docker required
  - No network latency
  - Works offline
  - Predictable performance
- **Cons**:
  - Slower than HTTP (~100-200ms per image)
  - Manual model management
  - CPU-only on most systems

### Auto Mode (Recommended)
- **Pros**:
  - Best of both worlds
  - Automatic failover
  - Self-healing
  - Flexible deployment
- **Cons**:
  - Slightly more complex
  - First request after HTTP failure may be slow

## Troubleshooting

### HTTP Service Not Available

**Symptom**: Logs show "YOLO11 HTTP service is not available"

**Solutions**:
1. Check if Docker is running: `docker ps`
2. Check if YOLO11 container is running: `docker ps | grep yolo11`
3. Verify endpoint configuration in `appsettings.json`
4. Check Aspire Dashboard for container status

### ONNX Model Not Found

**Symptom**: Logs show "YOLO11 model not found at models/yolo11n.onnx"

**Solutions**:
1. Download the model: `python download-model.py`
2. Or manually: 
   ```bash
   pip install ultralytics
   python -c "from ultralytics import YOLO; model = YOLO('yolo11n.pt'); model.export(format='onnx')"
   mv yolo11n.onnx models/
   ```

### Both Services Fail

**Symptom**: "All detection methods failed"

**Solutions**:
1. Ensure at least one detection method is available
2. Check ONNX model exists for fallback
3. Verify Docker container is running for HTTP mode
4. Check application logs for specific error messages

### Performance Issues

**Symptom**: Slow detection times

**Solutions**:
1. Use HTTP mode with Docker for best performance
2. For HTTP: Consider GPU-enabled Docker container
3. For ONNX: Use smaller model variant (yolo11n.pt)
4. Reduce image size in AppHost configuration
5. Check system resources (CPU/Memory)

## Testing

### Test HTTP Service Manually

```bash
# Start container
docker run -p 8000:8000 ultralytics/ultralytics yolo serve model=yolo11n.pt imgsz=640

# Test with curl
curl -X POST http://localhost:8000/inference \
  -F "image=@test-image.jpg"
```

### Test ONNX Service

Run unit tests (requires ONNX model):
```bash
dotnet test --filter "Category!=Integration"
```

### Test Composite Service

Run full integration tests:
```bash
# Download model first
python download-model.py

# Run all tests
dotnet test
```

## Best Practices

1. **Production**: Use Auto mode for resilience
2. **Development**: Use HTTP mode for faster iteration
3. **CI/CD**: Use ONNX mode for consistent testing
4. **Offline**: Use ONNX mode only

5. **Model Selection**:
   - `yolo11n.pt`: Development, low-resource environments
   - `yolo11s.pt`: Balanced performance/accuracy
   - `yolo11m.pt`: Production with good hardware
   - `yolo11l.pt` / `yolo11x.pt`: Maximum accuracy

6. **Image Size**:
   - 320: Fast, lower accuracy
   - 640: Balanced (recommended)
   - 1280: Slower, higher accuracy

7. **Monitoring**: Check Aspire Dashboard for:
   - Container health status
   - Request traces
   - Performance metrics
   - Error rates

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [YOLO11 Documentation](https://docs.ultralytics.com/)
- [ONNX Runtime Documentation](https://onnxruntime.ai/)
- [Docker Documentation](https://docs.docker.com/)
