# Setup Guide for ADComms Person Tracking System

## Quick Start

### 1. Prerequisites

- .NET 10 SDK installed
- Python 3.8+ with pip (for YOLO11 model export)
- **Note:** FFmpeg binaries are automatically downloaded on first video upload

### 2. Install Ultralytics YOLO (for model export)

```bash
pip install ultralytics
```

### 3. Export YOLO11 Model to ONNX

Create a Python script `export_yolo11.py`:

```python
from ultralytics import YOLO

# Download and load YOLO11 nano model (smallest, fastest)
model = YOLO('yolo11n.pt')

# Export to ONNX format
model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)

print("YOLO11 model exported to yolo11n.onnx")
```

Run the script:

```bash
python export_yolo11.py
```

This will download the YOLO11n model and export it to ONNX format.

### 4. Move Model to Application

```bash
mkdir -p ADCommsPersonTracking.Api/models
mv yolo11n.onnx ADCommsPersonTracking.Api/models/
```

### 5. Build and Run

```bash
cd ADCommsPersonTracking.Api
dotnet restore
dotnet build
dotnet run
```

The API will be available at:
- HTTPS: https://localhost:5001
- HTTP: http://localhost:5000

## Verify Installation

### Test Health Endpoint

```bash
curl http://localhost:5000/api/persontracking/health
```

Expected response:
```json
{
  "status": "healthy",
  "timestamp": "2025-12-11T11:00:00.000Z"
}
```

### Test with Sample Image

```bash
# Create a test image (or use your own)
curl -o test_frame.jpg https://via.placeholder.com/640x480

# Encode to base64
IMAGE_BASE64=$(base64 -w 0 test_frame.jpg)

# Submit tracking request
curl -X POST http://localhost:5000/api/persontracking/track \
  -H "Content-Type: application/json" \
  -d "{
    \"cameraId\": \"test-cam\",
    \"imageBase64\": \"$IMAGE_BASE64\",
    \"prompt\": \"find any person in the frame\",
    \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
  }"
```

## Model Options

### YOLO11 Variants

| Model | Size | Speed | mAP | Use Case |
|-------|------|-------|-----|----------|
| yolo11n | 6 MB | Fastest | 44.2 | Old hardware, real-time |
| yolo11s | 22 MB | Fast | 49.3 | Balanced |
| yolo11m | 50 MB | Medium | 54.7 | Better accuracy |
| yolo11l | 102 MB | Slow | 57.2 | High accuracy |
| yolo11x | 195 MB | Slowest | 58.8 | Best accuracy |

**Recommendation for old hardware**: Use `yolo11n` or `yolo11s`

### Export Any Variant

```python
from ultralytics import YOLO

# Replace 'n' with s, m, l, or x for other variants
model = YOLO('yolo11s.pt')
model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
```

Update `appsettings.json` accordingly:

```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11s.onnx"
  }
}
```

## Hardware Requirements

### Minimum (with YOLO11n)
- CPU: 2 cores, 2+ GHz
- RAM: 2 GB
- Storage: 100 MB

### Recommended
- CPU: 4 cores, 2.5+ GHz
- RAM: 4 GB
- Storage: 500 MB

### GPU Support (Optional)

To enable GPU acceleration with ONNX Runtime:

```bash
dotnet add package Microsoft.ML.OnnxRuntime.Gpu
```

Update service registration in `Program.cs` to use GPU execution provider.

## Configuration

The system can be configured via `appsettings.json`:

```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11n.onnx",
    "ConfidenceThreshold": 0.45,
    "IouThreshold": 0.5
  },
  "ImageAnnotation": {
    "BoxColor": "#00FF00",
    "BoxThickness": 2,
    "ShowLabels": true,
    "FontSize": 12
  }
}
```

- **ModelPath**: Path to the YOLO11 ONNX model file
- **ConfidenceThreshold**: Minimum confidence for person detection (0.0 - 1.0)
- **IouThreshold**: Threshold for Non-Maximum Suppression overlap
- **BoxColor**: Hex color for bounding boxes (e.g., "#00FF00" for green)
- **BoxThickness**: Thickness of bounding box lines in pixels
- **ShowLabels**: Whether to show confidence labels on boxes
- **FontSize**: Size of label text

## Troubleshooting

### Issue: "Could not load ONNX model"

**Solution**: Verify model path in `appsettings.json` and ensure the file exists:

```bash
ls -la ADCommsPersonTracking.Api/models/yolo11n.onnx
```

If the model is missing, the system will use mock detections for testing.

### Issue: Out of memory on old hardware

**Solutions**:
1. Use YOLO11n (smallest model)
2. Reduce frame resolution before sending to API
3. Process fewer frames per second
4. Increase system swap space

### Issue: Slow processing

**Solutions**:
1. Use YOLO11n model
2. Reduce input image size to 320x320 or 416x416
3. Process frames at 2-5 FPS instead of real-time
4. Enable GPU support if available

## Production Deployment

### Docker Deployment

Create `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["ADCommsPersonTracking.Api/ADCommsPersonTracking.Api.csproj", "ADCommsPersonTracking.Api/"]
RUN dotnet restore "ADCommsPersonTracking.Api/ADCommsPersonTracking.Api.csproj"
COPY . .
WORKDIR "/src/ADCommsPersonTracking.Api"
RUN dotnet build "ADCommsPersonTracking.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ADCommsPersonTracking.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# Copy YOLO11 model
COPY ADCommsPersonTracking.Api/models /app/models
ENTRYPOINT ["dotnet", "ADCommsPersonTracking.Api.dll"]
```

Build and run:

```bash
docker build -t adcomms-tracking .
docker run -p 5000:80 -e Anthropic__ApiKey="your-key" adcomms-tracking
```

### Environment Variables for Production

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://+:5000"
export ObjectDetection__ModelPath="/app/models/yolo11n.onnx"
export ObjectDetection__ConfidenceThreshold="0.45"
export ImageAnnotation__BoxColor="#00FF00"
```

## Integration with Camera Systems

### RTSP Stream Integration

For integration with RTSP camera streams, use a frame extraction service:

```python
import cv2
import requests
import base64
from datetime import datetime

# Connect to RTSP stream
cap = cv2.VideoCapture('rtsp://camera-ip:554/stream')

while True:
    ret, frame = cap.read()
    if not ret:
        break
    
    # Encode frame
    _, buffer = cv2.imencode('.jpg', frame)
    frame_base64 = base64.b64encode(buffer).decode('utf-8')
    
    # Submit to tracking API
    response = requests.post(
        'http://localhost:5000/api/persontracking/track',
        json={
            'cameraId': 'train-cam-01',
            'imageBase64': frame_base64,
            'prompt': 'find a person in a yellow jacket',
            'timestamp': datetime.utcnow().isoformat() + 'Z'
        }
    )
    
    # Process response...
    
    # Wait before next frame (2 FPS)
    time.sleep(0.5)
```

## Support

For issues and questions:
1. Check the [README.md](README.md) for API documentation
2. Review logs in the console output
3. Open an issue on GitHub

## Security Notes

1. Never commit API keys to source control
2. Use HTTPS in production
3. Implement authentication/authorization for production APIs
4. Rate limit API endpoints to prevent abuse
5. Validate and sanitize all input data
