# ADComms Person Tracking System

A .NET 10 web application for real-time person tracking across train cameras using YOLO11 object detection and Claude Opus 4.5 for intelligent prompt-based matching.

## Features

- **Video Frame Processing**: Receives and processes video frames from multiple train cameras
- **Person Detection**: Uses YOLO11 ONNX model for lightweight, efficient person detection
- **AI-Powered Matching**: Uses Claude Opus 4.5 to match detected persons against natural language prompts
- **Cross-Camera Tracking**: Tracks persons as they move between different camera views
- **Bounding Box Output**: Returns precise bounding boxes for all matched detections
- **RESTful API**: Easy-to-use HTTP API for integration with camera systems

## Architecture

### Components

1. **Object Detection Service**: Uses YOLO11 (ONNX Runtime) for fast person detection
2. **Claude Service**: Integrates Claude Opus 4.5 for:
   - Parsing natural language tracking prompts
   - Extracting visual features from descriptions
   - Matching detected persons to search criteria
3. **Person Tracking Service**: Coordinates detection and tracking across cameras
4. **REST API**: Exposes endpoints for frame submission and track queries

### Technology Stack

- .NET 10
- ASP.NET Core Web API
- Microsoft.ML.OnnxRuntime (YOLO11)
- Anthropic Claude SDK (Claude Opus 4.5)
- SixLabors.ImageSharp (Image processing)

## Prerequisites

- .NET 10 SDK
- YOLO11 ONNX model (yolo11n.onnx, yolo11s.onnx, or other variants)
- Anthropic API key for Claude Opus 4.5

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/ilumar589/ADCommsPersonTracking.git
cd ADCommsPersonTracking
```

### 2. Download YOLO11 Model

Download a YOLO11 ONNX model (recommended: yolo11n.onnx for lightweight operation):

```bash
mkdir -p ADCommsPersonTracking.Api/models
# Download from Ultralytics or export your own YOLO11 model to ONNX format
# Place the model at: ADCommsPersonTracking.Api/models/yolo11n.onnx
```

To export YOLO11 to ONNX format using Ultralytics:

```python
from ultralytics import YOLO

# Load YOLO11 model
model = YOLO('yolo11n.pt')

# Export to ONNX
model.export(format='onnx')
```

### 3. Configure API Keys

Update `appsettings.json` with your Anthropic API key:

```json
{
  "Anthropic": {
    "ApiKey": "your-anthropic-api-key-here"
  },
  "ObjectDetection": {
    "ModelPath": "models/yolo11n.onnx"
  }
}
```

Or use environment variables:

```bash
export Anthropic__ApiKey="your-anthropic-api-key-here"
```

### 4. Build and Run

```bash
cd ADCommsPersonTracking.Api
dotnet restore
dotnet build
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

## API Usage

### 1. Track Persons in a Frame

**Endpoint**: `POST /api/persontracking/track`

**Request Body**:
```json
{
  "cameraId": "train-cam-01",
  "imageBase64": "base64-encoded-image-data",
  "prompt": "find a person in a yellow jacket and black hat with a suitcase",
  "timestamp": "2025-12-11T11:00:00Z"
}
```

**Response**:
```json
{
  "cameraId": "train-cam-01",
  "timestamp": "2025-12-11T11:00:00Z",
  "detections": [
    {
      "trackingId": "track_train-cam-01_abc123",
      "boundingBox": {
        "x": 245.5,
        "y": 180.2,
        "width": 120.0,
        "height": 280.0,
        "confidence": 0.89,
        "label": "person"
      },
      "description": "yellow jacket, black hat, suitcase",
      "matchScore": 0.89
    }
  ],
  "processingMessage": "Processed frame with 3 detections, 1 matches found"
}
```

### 2. Get Active Tracks

**Endpoint**: `GET /api/persontracking/tracks`

**Response**:
```json
[
  {
    "trackingId": "track_train-cam-01_abc123",
    "cameraId": "train-cam-01",
    "firstSeen": "2025-12-11T10:58:00Z",
    "lastSeen": "2025-12-11T11:00:00Z",
    "lastKnownPosition": {
      "x": 245.5,
      "y": 180.2,
      "width": 120.0,
      "height": 280.0,
      "confidence": 0.89,
      "label": "person"
    },
    "description": "yellow jacket, black hat, suitcase",
    "features": ["yellow jacket", "black hat", "suitcase"]
  }
]
```

### 3. Get Specific Track

**Endpoint**: `GET /api/persontracking/tracks/{trackingId}`

**Response**: Returns a single `PersonTrack` object

### 4. Health Check

**Endpoint**: `GET /api/persontracking/health`

**Response**:
```json
{
  "status": "healthy",
  "timestamp": "2025-12-11T11:00:00Z"
}
```

## Example Usage with C#

A complete example client application is provided in `ADCommsPersonTracking.ExampleClient`.

### Using the Example Client

```bash
cd ADCommsPersonTracking.ExampleClient
dotnet run http://localhost:5000 frame.jpg train-cam-01 "find a person wearing a red shirt"
```

### C# Code Example

```csharp
using System.Net.Http.Json;

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5000") };

// Read and encode image
var imageBytes = await File.ReadAllBytesAsync("frame.jpg");
var imageBase64 = Convert.ToBase64String(imageBytes);

// Submit tracking request
var request = new
{
    cameraId = "train-cam-01",
    imageBase64 = imageBase64,
    prompt = "find a person wearing a red shirt and carrying a backpack",
    timestamp = DateTime.UtcNow
};

var response = await httpClient.PostAsJsonAsync("api/persontracking/track", request);
var result = await response.Content.ReadFromJsonAsync<TrackingResponse>();

Console.WriteLine($"Found {result.Detections.Count} matches");

foreach (var detection in result.Detections)
{
    var bbox = detection.BoundingBox;
    Console.WriteLine($"Track ID: {detection.TrackingId}");
    Console.WriteLine($"Position: ({bbox.X}, {bbox.Y})");
    Console.WriteLine($"Size: {bbox.Width}x{bbox.Height}");
    Console.WriteLine($"Confidence: {bbox.Confidence}");
}
```

## Testing with cURL

For testing the API endpoints:

```bash
# Encode image to base64
IMAGE_BASE64=$(base64 -w 0 frame.jpg)

# Submit tracking request
curl -X POST http://localhost:5000/api/persontracking/track \
  -H "Content-Type: application/json" \
  -d "{
    \"cameraId\": \"train-cam-01\",
    \"imageBase64\": \"$IMAGE_BASE64\",
    \"prompt\": \"find a person in a yellow jacket and black hat with a suitcase\",
    \"timestamp\": \"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"
  }"

# Get active tracks
curl http://localhost:5000/api/persontracking/tracks

# Check health
curl http://localhost:5000/api/persontracking/health
```

## Configuration

### Model Selection

The system supports different YOLO11 model sizes:

- **yolo11n.onnx**: Nano - Fastest, lowest accuracy (recommended for old hardware)
- **yolo11s.onnx**: Small - Good balance
- **yolo11m.onnx**: Medium - Better accuracy
- **yolo11l.onnx**: Large - High accuracy
- **yolo11x.onnx**: Extra Large - Best accuracy, slowest

Update the `ModelPath` in `appsettings.json` to use different models.

### Performance Tuning

For old hardware, consider:

1. Use `yolo11n.onnx` (nano model)
2. Reduce input frame resolution before sending to API
3. Process frames at lower FPS (e.g., 2-5 FPS instead of 30 FPS)
4. Use CPU optimization flags in ONNX Runtime

## How It Works

1. **Frame Reception**: Camera sends video frame (base64 encoded) with search prompt
2. **Object Detection**: YOLO11 detects all persons in the frame
3. **Feature Extraction**: Claude Opus 4.5 extracts visual features from the prompt
4. **Matching**: Claude analyzes detections and matches them to the prompt criteria
5. **Tracking**: System assigns tracking IDs and maintains person trajectories
6. **Response**: Returns bounding boxes for matched persons

## Cross-Camera Tracking

The system maintains person tracks across cameras by:

1. Assigning unique tracking IDs to detected persons
2. Matching persons across frames using spatial proximity
3. Maintaining track history for 5 minutes
4. Associating descriptions with tracks for cross-camera matching

## Performance Considerations

### For Old Hardware

- **Model**: Use YOLO11n (nano) for best performance
- **Resolution**: Process frames at 640x640 or lower
- **Frame Rate**: 2-5 FPS is sufficient for tracking
- **Batch Processing**: Process multiple frames from different cameras in batches

### Memory Usage

- YOLO11n: ~6MB model size, ~200MB runtime memory
- Claude API: Network calls only, no local memory impact
- Tracking: ~1KB per active track

## Troubleshooting

### Model Not Found

If you see "YOLO11 ONNX model not found", the system will use mock detections. Download and place the YOLO11 model at the configured path.

### Claude API Errors

Ensure your Anthropic API key is valid and has access to Claude Opus 4.5. Check logs for specific error messages.

### Low Detection Accuracy

- Ensure adequate lighting in camera feeds
- Use higher resolution frames
- Consider using a larger YOLO11 model (s, m, l variants)

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please submit pull requests with clear descriptions of changes.