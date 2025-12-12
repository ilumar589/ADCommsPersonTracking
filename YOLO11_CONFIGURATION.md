# YOLO11 Configuration Guide

This document describes how to configure and use the YOLO11 object detection service in the ADComms Person Tracking System.

## Overview

The system uses **local ONNX inference** for YOLO11 object detection. The YOLO11 model is loaded from a shared volume and runs directly within the API service using ONNX Runtime.

## Configuration

### API Service Configuration (`appsettings.json`)

```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11n.onnx",
    "ConfidenceThreshold": 0.45,
    "IouThreshold": 0.5
  }
}
```

#### Configuration Options

**ObjectDetection:ModelPath**
- Path to the local YOLO11 ONNX model file
- Required for object detection to work
- Default: `models/yolo11n.onnx`

**ObjectDetection:ConfidenceThreshold**
- Minimum confidence score for detections (0.0 to 1.0)
- Default: `0.45`

**ObjectDetection:IouThreshold**
- Intersection over Union threshold for Non-Maximum Suppression
- Default: `0.5`

### Model Export Configuration

The system uses a Docker container to export YOLO11 models to ONNX format. This is a one-time operation that creates the model file in the shared `models/` directory.

**Model Options:**
- `yolo11n.onnx` (nano) - Fastest, smallest (~6MB)
- `yolo11s.onnx` (small) - Good balance (~22MB)
- `yolo11m.onnx` (medium) - Better accuracy (~50MB)
- `yolo11l.onnx` (large) - High accuracy (~87MB)
- `yolo11x.onnx` (extra large) - Best accuracy (~136MB)

Update the `ModelPath` in `appsettings.json` to use different model sizes.

## Usage

### Running with Aspire (Recommended)

When running via Aspire AppHost, the system uses local ONNX inference.

```bash
cd ADCommsPersonTracking.AppHost
dotnet run
```

**What happens:**
1. Aspire starts the infrastructure services (Redis, Azure Storage Emulator)
2. The YOLO model export container runs once to create the ONNX model file
3. API service loads the ONNX model from the shared `models/` directory
4. All detection runs locally within the API service using ONNX Runtime

**Configuration:**
- The API receives the model path via environment variables from Aspire
- No manual endpoint configuration needed
- Model must exist in the `models/` directory for detection to work

### Standalone API (Without Aspire)

Run the API independently without Aspire orchestration.

```bash
cd ADCommsPersonTracking.Api
dotnet run
```

**What happens:**
1. API starts and looks for the ONNX model at the configured path
2. If the model exists, local ONNX inference is used
3. If the model doesn't exist, the service will throw an error

**Requirements:**
- ONNX model must exist at the configured `ModelPath`
- Export the model manually using the Python script or Docker container

**Configuration:**
```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11n.onnx"
  }
}
```

## Service Architecture

### ObjectDetectionService

The system uses a single detection service that performs local ONNX inference:

```
┌─────────────────────────────────────────┐
│   ObjectDetectionService                │
│   (IObjectDetectionService)             │
│                                         │
│   - Loads YOLO11 ONNX model            │
│   - Runs inference via ONNX Runtime     │
│   - Returns bounding boxes              │
└─────────────────────────────────────────┘
```

### Logging

The system provides logging for detection operations:

```
[Information] YOLO11 ONNX model loaded successfully from models/yolo11n.onnx
[Information] Detected 3 persons in frame
[Warning] YOLO11 ONNX model not found at models/yolo11n.onnx. Detection will use mock data.
```

## Performance

### ONNX Mode (Local Inference)
- **Performance**: ~100-200ms per image on CPU
- **Pros**:
  - No network latency
  - Works offline
  - Predictable performance
  - No additional services required
- **Cons**:
  - CPU-only on most systems (GPU acceleration available with CUDA)
  - Manual model management

## Troubleshooting

### ONNX Model Not Found

**Symptom**: Logs show "YOLO11 model not found at models/yolo11n.onnx"

**Solutions**:
1. Download the model: `python download-model.py`
2. Or manually export using Python: 
   ```bash
   pip install ultralytics
   python -c "from ultralytics import YOLO; model = YOLO('yolo11n.pt'); model.export(format='onnx')"
   mv yolo11n.onnx models/
   ```
3. Or use the Docker export container:
   ```bash
   cd docker/yolo-model-export
   docker build -t yolo-model-export .
   docker run --rm -v "$(pwd)/../../models:/models" yolo-model-export:latest
   ```

### Detection Fails

**Symptom**: "Inference session is not initialized" error

**Solutions**:
1. Verify the ONNX model exists at the configured path
2. Check file permissions on the model file
3. Ensure the model file is not corrupted (should be ~6MB for yolo11n)
4. Check application logs for specific error messages

### Performance Issues

**Symptom**: Slow detection times

**Solutions**:
1. Use smaller model variant (yolo11n.onnx) for faster inference
2. Reduce input image resolution before processing
3. Check system resources (CPU/Memory)
4. Consider using ONNX Runtime with GPU support if available

## Testing

### Test ONNX Service

Run unit tests (requires ONNX model):
```bash
dotnet test --filter "Category!=Integration"
```

### Run Full Test Suite

Run all tests including integration tests:
```bash
# Download model first
python download-model.py

# Run all tests
dotnet test
```

## Best Practices

1. **Model Selection**:
   - `yolo11n.onnx`: Development, low-resource environments (~6MB)
   - `yolo11s.onnx`: Balanced performance/accuracy (~22MB)
   - `yolo11m.onnx`: Production with good hardware (~50MB)
   - `yolo11l.onnx` / `yolo11x.onnx`: Maximum accuracy (~87MB / ~136MB)

2. **Image Processing**:
   - Process at 640x640 resolution (recommended)
   - Use lower resolutions (320x320) for faster inference on old hardware
   - Use higher resolutions (1280x1280) for better accuracy if performance allows

3. **Monitoring**: Check Aspire Dashboard for:
   - API service health status
   - Request traces
   - Performance metrics
   - Error rates

4. **Deployment**:
   - Ensure the ONNX model file is included in your deployment
   - Verify model file path is correctly configured for your environment
   - Pre-warm the model by running a test inference on startup

## Additional Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/dotnet/aspire/)
- [YOLO11 Documentation](https://docs.ultralytics.com/)
- [ONNX Runtime Documentation](https://onnxruntime.ai/)
