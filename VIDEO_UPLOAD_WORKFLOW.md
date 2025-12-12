# Video Upload Workflow Documentation

## Overview

The video upload workflow allows users to upload video files, extract frames, store them in Azure Blob Storage, and process them using person tracking with a search prompt.

## Architecture

### Components

1. **Video Processing Service** (`VideoProcessingService`)
   - Extracts frames from video files using FFMpegCore
   - Supports configurable frame extraction (max frames, interval)
   - Automatically cleans up temporary files

2. **Frame Storage Service** (`FrameStorageService`)
   - Stores extracted frames in Azure Blob Storage (Azurite emulator)
   - Organizes frames by tracking ID: `{trackingId}/frame_0000.png`
   - Provides retrieval and existence checking

3. **Video Cache Service** (`VideoCacheService`)
   - Caches video name → tracking ID mappings in Redis
   - 5-minute TTL prevents duplicate processing
   - Returns cached results for repeated uploads

### Flow Diagram

```
User Uploads Video
       ↓
Check Redis Cache (by video name)
       ↓
   ┌───────────┐
   │ Cached?   │
   └─────┬─────┘
         │
    Yes ←┴→ No
     │       │
     │       ↓
     │   Extract Frames (FFMpegCore)
     │       ↓
     │   Store in Blob Storage
     │       ↓
     │   Cache tracking ID
     │       │
     └───────┴────→ Return Tracking ID
                            ↓
                    User provides prompt
                            ↓
                    Retrieve frames from Blob Storage
                            ↓
                    Process with PersonTrackingService
                            ↓
                    Return annotated images & detections
```

## API Endpoints

### 1. Upload Video

**Endpoint**: `POST /api/persontracking/video/upload`

**Request**: Multipart form data with video file

```bash
curl -X POST http://localhost:5000/api/persontracking/video/upload \
  -F "video=@sample_video.mp4"
```

**Response**:
```json
{
  "trackingId": "video_abc123...",
  "frameCount": 45,
  "wasCached": false,
  "message": "Video processed successfully. Extracted 45 frames."
}
```

**Response (Cached)**:
```json
{
  "trackingId": "video_abc123...",
  "frameCount": 0,
  "wasCached": true,
  "message": "Video already processed. Returning existing tracking ID."
}
```

### 2. Track by ID

**Endpoint**: `POST /api/persontracking/track-by-id`

**Request Body**:
```json
{
  "trackingId": "video_abc123...",
  "prompt": "find a person in a yellow jacket and black hat",
  "timestamp": "2025-12-12T12:00:00Z"
}
```

**Response**: Same as `/track` endpoint (TrackingResponse)

## Configuration

### appsettings.json

```json
{
  "VideoProcessing": {
    "MaxFrames": 100,
    "FrameInterval": 1
  },
  "BlobStorage": {
    "ContainerName": "video-frames"
  }
}
```

### AppHost Configuration

```csharp
// Azure Storage (Azurite emulator)
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator();
var blobs = storage.AddBlobs("blobs");

// Redis for caching
var redis = builder.AddRedis("redis");

// API with references
var api = builder.AddProject<Projects.ADCommsPersonTracking_Api>("adcommspersontracking-api")
    .WithReference(blobs)
    .WithReference(redis);
```

## Web UI Usage

### Video Upload Page

1. Navigate to **Video Upload** from the main menu
2. Click **Select Video File** and choose a video
3. Click **Upload and Process Video**
4. View results: tracking ID and frame count
5. Click **Proceed to Tracking** to analyze frames

### Track Submission Page

**Tab 1: Upload Images** (existing functionality)
- Upload individual images for immediate processing

**Tab 2: Use Tracking ID** (new functionality)
1. Enter a tracking ID from a previous video upload
2. Enter a search prompt describing the person to find
3. Click **Submit Tracking Request**
4. View annotated images with detected persons

## Deduplication Strategy

Videos are deduplicated based on filename:
- **Cache Key**: `video:{fileName}`
- **Cache Value**: `{trackingId}`
- **TTL**: 5 minutes

If the same video file is uploaded within 5 minutes, the cached tracking ID is returned immediately without re-processing.

## Storage Structure

### Azure Blob Storage (Azurite)

```
video-frames/                    (container)
  ├── video_abc123.../           (tracking ID)
  │   ├── frame_0000.png
  │   ├── frame_0001.png
  │   ├── frame_0002.png
  │   └── ...
  └── video_def456.../
      ├── frame_0000.png
      └── ...
```

### Redis Cache

```
video:sample_video.mp4 → video_abc123...
video:another_video.mp4 → video_def456...
```

## Performance Considerations

1. **Frame Extraction**
   - FFMpegCore processes videos sequentially
   - Large videos may take time to process
   - Progress indication in UI keeps users informed

2. **Blob Storage**
   - Frames are uploaded in batches
   - Each frame is a separate blob
   - Retrieval is optimized with parallel downloads

3. **Caching**
   - Redis cache reduces redundant processing
   - 5-minute TTL balances freshness and efficiency
   - Cache misses gracefully fall back to processing

## Error Handling

- **Invalid Video Format**: Returns 400 Bad Request
- **Video Too Large**: Client-side limit of 500MB
- **Processing Errors**: Logged and return 500 with message
- **Missing Tracking ID**: Returns 404 Not Found
- **Cache Failures**: Non-critical, logged but not thrown

## Testing

### Unit Tests

All existing tests updated to include new mock services:
```csharp
var controller = new PersonTrackingController(
    trackingServiceMock.Object,
    logger,
    videoProcessingServiceMock.Object,
    frameStorageServiceMock.Object,
    videoCacheServiceMock.Object
);
```

### Manual Testing

1. **Upload a video**: Verify frames are extracted and stored
2. **Re-upload same video**: Verify cached response
3. **Wait 5+ minutes and re-upload**: Verify fresh processing
4. **Track by ID**: Verify frames are retrieved and processed
5. **Invalid tracking ID**: Verify 404 response

## Dependencies

- **FFMpegCore**: Video frame extraction
- **Aspire.Azure.Storage.Blobs**: Blob storage client
- **Aspire.StackExchange.Redis.DistributedCaching**: Redis caching
- **FFmpeg**: System dependency (must be installed)

### Installing FFmpeg

**Ubuntu/Debian**:
```bash
sudo apt-get install ffmpeg
```

**macOS**:
```bash
brew install ffmpeg
```

**Windows**:
Download from https://ffmpeg.org/download.html

## Security

- **Input Validation**: All endpoints validate required parameters
- **File Size Limits**: 500MB max enforced client-side
- **Error Messages**: Generic messages prevent information disclosure
- **CodeQL Scan**: 0 vulnerabilities detected
- **Blob Access**: Private containers, no public access

## Future Enhancements

Potential improvements:
1. Background job processing for large videos
2. Progress callbacks during frame extraction
3. Video format conversion support
4. Thumbnail generation
5. Frame sampling strategies (keyframes, motion detection)
6. Batch video processing
7. Video metadata extraction
8. Support for live video streams
