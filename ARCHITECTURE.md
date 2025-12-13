# Architecture Overview

## System Design

The ADComms Person Tracking System is a .NET 10 web application designed to process video frames from train cameras and track persons across multiple camera views using AI-powered detection and matching.

## Components

### 1. API Layer (`PersonTrackingController`)

**Responsibility**: HTTP API endpoints for external systems

**Endpoints**:
- `POST /api/persontracking/track` - Submit frame with natural language prompt
- `GET /api/persontracking/tracks` - Retrieve all active tracks
- `GET /api/persontracking/tracks/{id}` - Get specific track details
- `GET /api/persontracking/health` - Health check

### 2. Service Layer

#### PersonTrackingService (`IPersonTrackingService`)

**Responsibility**: Orchestrates the tracking workflow

**Key Functions**:
- Coordinates detection and matching
- Maintains tracking state across cameras
- Manages track lifecycle (creation, updates, cleanup)
- Associates tracking IDs with detected persons

**Tracking Logic**:
- Uses spatial proximity to match detections across frames
- Maintains 5-minute timeout for inactive tracks
- Supports cross-camera tracking with unique IDs

#### ObjectDetectionService (`IObjectDetectionService`)

**Responsibility**: Person detection in video frames using local ONNX inference

**Technology**: 
- ONNX Runtime with YOLO11 models
- Supports models: yolo11n (nano) to yolo11x (extra-large)
- Default: yolo11m (medium) for better accessory detection
- Local inference only - no HTTP calls

**Process**:
1. Receives frame as byte array
2. Preprocesses image (resize to 640x640)
3. Runs YOLO11 inference using ONNX Runtime
4. Filters for person class (COCO class 0)
5. Applies Non-Maximum Suppression (NMS)
6. Returns bounding boxes with confidence scores

**Error Handling**: Throws exception when model is not available

#### TrackingLlmService (`ITrackingLlmService`)

**Responsibility**: Natural language prompt processing and matching

**Technology**: Anthropic Claude API (Claude 3.5 Sonnet)

**Key Functions**:
1. **ParseTrackingPromptAsync**: Extracts structured features from prompts
2. **ExtractSearchFeaturesAsync**: Identifies visual search criteria
3. **MatchDetectionsToPromptAsync**: Matches detected persons to search criteria

**Example Flow**:
```
Input: "find a person in a yellow jacket and black hat with a suitcase"
↓
Feature Extraction: ["yellow jacket", "black hat", "suitcase"]
↓
Detection Matching: Analyzes which detected persons match the criteria
↓
Output: List of matching detection indices
```

### 3. Data Models

#### TrackingRequest
- Camera identifier
- Base64-encoded image
- Natural language prompt
- Timestamp

#### TrackingResponse
- Camera identifier
- Timestamp
- List of matched detections with bounding boxes
- Processing message

#### BoundingBox
- Position (X, Y)
- Size (Width, Height)
- Confidence score
- Label

#### PersonTrack
- Unique tracking ID
- Camera location
- First/last seen timestamps
- Last known position
- Description and features

## Data Flow

```
Camera System
    ↓
    │ HTTP POST /api/persontracking/track
    │ { image, prompt, cameraId, timestamp }
    ↓
PersonTrackingController
    ↓
PersonTrackingService
    ├─→ ObjectDetectionService (YOLO11)
    │   └─→ Returns: List<BoundingBox>
    │
    └─→ TrackingLlmService (Claude)
        ├─→ Extract features from prompt
        └─→ Match detections to criteria
    ↓
Track Management
    ├─→ Create/Update tracking IDs
    └─→ Maintain track history
    ↓
TrackingResponse
    └─→ { detections, boundingBoxes, trackingIds }
```

## Performance Considerations

### For Old Hardware

**Model Selection**: YOLO11n (6MB, fastest)
- mAP: 44.2
- Speed: ~200 FPS on modern CPU, ~20-50 FPS on old hardware
- Memory: ~200MB runtime

**Optimization Strategies**:
1. **Frame Rate**: Process at 2-5 FPS instead of real-time
2. **Resolution**: 640x480 or lower input frames
3. **Batch Processing**: Process multiple camera frames in batches
4. **Model Quantization**: Use quantized ONNX models if available

### Scalability

**Stateful Design**: 
- In-memory tracking state (ConcurrentDictionary)
- Suitable for single-instance deployment
- For multi-instance: Consider Redis or database for shared state

**Rate Limiting**: Not implemented - add if needed for production

## Configuration

### appsettings.json

```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-..."  // Required for LLM functionality
  },
  "ObjectDetection": {
    "ModelPath": "models/yolo11m.onnx"  // Path to YOLO11 ONNX model
  }
}
```

### Environment Variables

```bash
Anthropic__ApiKey="sk-ant-..."
ObjectDetection__ModelPath="/path/to/yolo11m.onnx"
```

## Deployment Options

### Single Server
- Run with `dotnet run` or as a service
- Suitable for 1-10 cameras
- Direct HTTP API calls

### Docker Container
- Containerized deployment
- Easy scaling with orchestrators
- Include YOLO11 model in image

### Cloud Deployment
- Azure App Service / AWS Elastic Beanstalk
- Auto-scaling based on load
- Store models in blob storage

## Security Considerations

1. **API Key Protection**: Never commit Anthropic API keys
2. **Input Validation**: Validate image size and format
3. **Rate Limiting**: Consider adding for production
4. **HTTPS**: Use in production environments
5. **Authentication**: Add API authentication for production

## Future Enhancements

1. **Re-identification Features**: Deep learning features for better cross-camera matching
2. **Persistent Storage**: Database for long-term track history
3. **Real-time Streaming**: WebSocket support for live updates
4. **Analytics Dashboard**: Web UI for visualization
5. **GPU Acceleration**: CUDA support for faster processing
6. **Multi-model Support**: Combine YOLO11 with SAM 2 or Grounding DINO

## Monitoring

**Key Metrics to Track**:
- Processing latency per frame
- Detection accuracy (persons detected vs. ground truth)
- Track continuity (successful cross-camera matches)
- API response times
- LLM API usage and costs

**Logging**:
- Frame processing events
- Detection counts
- Track creation/updates
- LLM request/response
- Errors and exceptions

## Testing

**Unit Tests**: Service layer logic (not included in MVP)
**Integration Tests**: Full API workflow (not included in MVP)
**Performance Tests**: Load testing with multiple cameras

**Manual Testing**:
```bash
# Start API
dotnet run

# Test health
curl http://localhost:5205/api/persontracking/health

# Submit frame
curl -X POST http://localhost:5205/api/persontracking/track \
  -H "Content-Type: application/json" \
  -d @test_request.json
```
