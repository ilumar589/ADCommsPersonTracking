# ADComms Person Tracking System

A .NET 10 web application for real-time person tracking across train cameras using YOLO11 object detection with rule-based prompt feature extraction and image annotation capabilities.

## Features

- **Video Frame Processing**: Receives and processes video frames from multiple train cameras
- **Person Detection**: Uses YOLO11 ONNX model for lightweight, efficient person detection
- **Clothing Detection**: Optional fashion-trained YOLO model for detecting specific clothing items (jacket, shirt, pants, dress, etc.)
- **Rule-Based Feature Extraction**: Parses natural language prompts to extract colors, clothing, accessories, and physical attributes
- **Image Annotation**: Returns annotated images with bounding boxes drawn around detected persons
- **Cross-Camera Tracking**: Tracks persons as they move between different camera views
- **Bounding Box Output**: Returns precise bounding boxes with annotated images for all detections
- **RESTful API**: Easy-to-use HTTP API for integration with camera systems
- **Comprehensive Testing**: 156+ unit tests + Docker-based integration tests with real YOLO11 model inference

## Architecture

### Components

1. **Object Detection Service**: Uses YOLO11 (ONNX Runtime) for fast person detection
2. **Clothing Detection Service**: Optional fashion-trained YOLO model for detecting clothing items on persons
   - Supports detection of: shirts, jackets, coats, pants, jeans, shorts, skirts, dresses, sweaters, hoodies, vests
   - Runs inference on cropped person images for accurate clothing classification
   - Disabled by default (can be enabled in configuration)
3. **Prompt Feature Extractor**: Parses natural language prompts to extract:
   - Colors (red, blue, green, yellow, black, white, etc.)
   - Clothing items (jacket, coat, shirt, pants, etc.)
   - Accessories (bag, backpack, suitcase, umbrella, etc.)
   - Physical attributes (height, build)
4. **Image Annotation Service**: Draws bounding boxes on images with optional labels
5. **Person Tracking Service**: Coordinates detection and tracking across cameras
6. **REST API**: Exposes endpoints for frame submission and track queries

### Technology Stack

- .NET 10
- .NET Aspire 13 (Orchestration, Service Discovery, Observability)
- ASP.NET Core Web API
- Blazor WebAssembly UI
- Microsoft.ML.OnnxRuntime (YOLO11 local inference)
- SixLabors.ImageSharp (Image processing and annotation)

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for running with Aspire)
- YOLO11 ONNX model (yolo11x.onnx recommended, or other variants) - required for person detection
- **FFmpeg binaries are automatically downloaded** on first video upload (no manual installation required)

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/ilumar589/ADCommsPersonTracking.git
cd ADCommsPersonTracking
```

### 2. Get the YOLO11 ONNX Model

#### Option 1: Export using Python (Recommended)

1. Install Python 3.8+ and pip
2. Install Ultralytics:
   ```bash
   pip install ultralytics
   ```
3. Create and run export script:
   ```python
   from ultralytics import YOLO
   
   # Download and load YOLO11 extra-large model (best accuracy)
   model = YOLO('yolo11x.pt')
   
   # Export to ONNX format
   model.export(format='onnx', simplify=True, dynamic=False, imgsz=640)
   ```
4. Move the generated `yolo11x.onnx` to `ADCommsPersonTracking.Api/models/`:
   ```bash
   mkdir -p ADCommsPersonTracking.Api/models
   mv yolo11x.onnx ADCommsPersonTracking.Api/models/
   ```

#### Option 2: Download Pre-exported Model

Download from Ultralytics GitHub releases or HuggingFace:
- **yolo11n.onnx** (~6MB) - Nano, fastest (for old hardware)
- **yolo11s.onnx** (~22MB) - Small
- **yolo11m.onnx** (~50MB) - Medium
- **yolo11l.onnx** (~87MB) - Large
- **yolo11x.onnx** (~136MB) - Extra Large (recommended, default)

Place the downloaded model in `ADCommsPersonTracking.Api/models/`

### 3. Get the Fashion Detection Model

For enhanced clothing detection and color+clothing queries (e.g., "blue jacket"), the system uses a Fashionpedia-trained YOLO model:

#### Automatic Download (Recommended)

```bash
python3 download-fashion-model.py
```

This script automatically downloads the **keremberke/yolov8m-fashion-detection** model from HuggingFace and exports it to ONNX format.

**Model Details**:
- **Source**: [keremberke/yolov8m-fashion-detection](https://huggingface.co/keremberke/yolov8m-fashion-detection)
- **Dataset**: Fashionpedia
- **Size**: ~50 MB (YOLOv8m)
- **Classes**: 27 fashion categories

#### Supported Fashion Categories

**Clothing**:
- Upper body: shirt, top, sweater, cardigan, jacket, vest, coat
- Lower body: pants, shorts, skirt, tights
- Full body: dress, jumpsuit, cape

**Accessories**:
- Headwear: hat, headband
- Eyewear: glasses
- Footwear: shoe, sock
- Other: tie, glove, watch, belt, bag, scarf, umbrella, leg warmer

#### Configuration

The model is enabled by default with optimized settings:

```json
{
  "ClothingDetection": {
    "Enabled": true,
    "ModelPath": "models/fashion-yolo.onnx",
    "ConfidenceThreshold": 0.3
  }
}
```

**Note**: The confidence threshold is set to 0.3 (lower than YOLO11's 0.45) because fashion detection is more challenging and requires a more sensitive threshold.

#### Enhanced Color+Clothing Detection

The system now analyzes colors **specifically on detected clothing regions**:

1. YOLO11 detects persons
2. Fashion model detects clothing items **with bounding boxes**
3. Color analysis runs **on each clothing item's region**
4. Queries like "blue jacket" will:
   - Find "jacket" detections from fashion model
   - Check if "blue" is present **on the jacket region specifically**
   - Match only if both criteria are met

This is much more accurate than analyzing colors on the entire person.

### 4. Build and Run

#### Option A: Run with .NET Aspire (Recommended)

.NET Aspire orchestrates all services (API and Web UI) together with built-in observability, service discovery, and health checks.

```bash
# Ensure Docker is running for Aspire infrastructure (Redis, Azure Storage Emulator)
docker --version

# Build the YOLO model export Docker image
cd docker/yolo-model-export
docker build -t yolo-model-export .

# Build the fashion model export Docker image
cd ../fashion-model-export
docker build -t fashion-model-export .
cd ../..

# Set the AppHost as the startup project and run
cd ADCommsPersonTracking.AppHost
dotnet run
```

This will:
- Start the Aspire Dashboard (opens in browser automatically)
- Launch the API service with health checks and telemetry
- Launch the Blazor Web UI
- Start the YOLO11 model export container (one-time export to ONNX format)
- Start the fashion model export container (one-time export to ONNX format)
- Configure automatic service discovery between components

The model export containers will:
- **YOLO11 model export**: Download the YOLO11x model if not already present, export it to ONNX format, save to `models/yolo11x.onnx`, then exit
- **Fashion model export**: Download a placeholder YOLOv8n model, export it to ONNX format, save to `models/fashion-yolo.onnx`, then exit
  - **Note**: The placeholder fashion model will NOT detect clothing correctly. For production use, you need a fashion-trained YOLO model (see section 3 above)

Access the services:
- **Aspire Dashboard**: `http://localhost:15000` or `https://localhost:17000` (for monitoring all services)
- **API**: Service discovery endpoint (viewable in Aspire Dashboard)
- **Web UI**: Service discovery endpoint (viewable in Aspire Dashboard)

#### Option B: Run Standalone (Without Aspire)

```bash
cd ADCommsPersonTracking.Api
dotnet restore
dotnet build
dotnet run
```

The API will start on `https://localhost:5001` (or `http://localhost:5000`).

### 5. Run Tests

#### Unit Tests Only
```bash
cd /path/to/ADCommsPersonTracking
dotnet test --filter "Category!=Integration"
```

All 156+ unit tests should pass.

#### Integration Tests (Optional)
Integration tests use Docker and Testcontainers to test the YOLO11 model with real inference. See [INTEGRATION_TESTS.md](INTEGRATION_TESTS.md) for details.

```bash
# Requires Docker to be running and YOLO11 model downloaded
python download-model.py
dotnet test --filter "Category=Integration"
```

#### All Tests
```bash
dotnet test
```

## Model Export Docker Images

When running the application with .NET Aspire, both the YOLO11 model and fashion model need to be exported to ONNX format. This section provides detailed instructions for building and running the Docker images that handle these export processes.

### Prerequisites

- **Docker Desktop** must be installed and running
  - Download from: https://www.docker.com/products/docker-desktop/
  - Verify installation: `docker --version`

### Building and Running the Docker Images

Follow these steps to build the Docker images and export both models:

#### 1. Navigate to the Docker Directory

```bash
cd docker/yolo-model-export
```

#### 2. Build the YOLO11 Model Export Docker Image

```bash
docker build -t yolo-model-export:latest .
```

This creates a Docker image named `yolo-model-export:latest` that contains the YOLO11 export script.

#### 2b. Build the Fashion Model Export Docker Image

```bash
cd ../fashion-model-export
docker build -t fashion-model-export:latest .
cd ..
```

This creates a Docker image named `fashion-model-export:latest` that contains the fashion model export script.

#### 3. Create the Models Directory

```bash
mkdir -p ../models
cd yolo-model-export
```

This creates the `models/` directory in the repository root where the exported models will be saved.

#### 4. Run the Containers to Export the Models

Run the appropriate commands for your operating system to start the containers with a volume mount.

**Note:** These commands assume you are in the `docker/yolo-model-export` directory. The relative paths `../../models` will mount the `models/` directory from the repository root.

##### Export YOLO11 Model

**Linux/macOS:**
```bash
docker run --rm -v "$(pwd)/../../models:/models" yolo-model-export:latest
```

**Windows PowerShell:**
```powershell
docker run --rm -v "${PWD}/../../models:/models" yolo-model-export:latest
```

**Windows Command Prompt:**
```cmd
docker run --rm -v "%cd%/../../models:/models" yolo-model-export:latest
```

##### Export Fashion Model

**Linux/macOS:**
```bash
cd ../fashion-model-export
docker run --rm -v "$(pwd)/../../models:/models" fashion-model-export:latest
cd ..
```

**Windows PowerShell:**
```powershell
cd ../fashion-model-export
docker run --rm -v "${PWD}/../../models:/models" fashion-model-export:latest
cd ..
```

**Windows Command Prompt:**
```cmd
cd ..\fashion-model-export
docker run --rm -v "%cd%/../../models:/models" fashion-model-export:latest
cd ..
```

#### 5. Verify the Models Were Created

**Linux/macOS:**
```bash
ls -la ../models/yolo11x.onnx
ls -la ../models/fashion-yolo.onnx
```

**Windows PowerShell:**
```powershell
Get-ChildItem ../models/yolo11x.onnx
Get-ChildItem ../models/fashion-yolo.onnx
```

**Windows Command Prompt:**
```cmd
dir ..\models\yolo11x.onnx
dir ..\models\fashion-yolo.onnx
```

You should see:
- `yolo11x.onnx` - approximately 136MB
- `fashion-yolo.onnx` - approximately 6MB (placeholder YOLOv8n)

### What the Containers Do

#### YOLO11 Model Export Container

The `yolo-model-export` container performs the following operations:

1. **Downloads YOLO11x Pre-trained Weights** (~136MB) from Ultralytics if not already present
2. **Exports to ONNX Format** using the Ultralytics library with optimizations:
   - Simplified model structure
   - Fixed input size (640x640)
   - Static batch size for better performance
3. **Saves to Models Directory** - The exported `yolo11x.onnx` file is saved to the `models/` directory through the volume mount
4. **Exits Automatically** - The container completes and exits after the export is finished

The entire process typically takes 2-5 minutes on the first run when downloading weights from the internet.

#### Fashion Model Export Container

The `fashion-model-export` container performs the following operations:

1. **Downloads YOLOv8n Pre-trained Weights** (~6MB) as a placeholder from Ultralytics if not already present
2. **Exports to ONNX Format** using the Ultralytics library with the same optimizations as YOLO11
3. **Saves to Models Directory** - The exported `fashion-yolo.onnx` file is saved to the `models/` directory through the volume mount
4. **Exits Automatically** - The container completes and exits after the export is finished

**Important Note**: The fashion model export script currently exports a placeholder YOLOv8n model that will **NOT** detect clothing items correctly. For production use, you need to:
- Train a fashion-specific YOLO model on DeepFashion2 or similar datasets
- Replace the placeholder model export with your trained model
- The expected classes are: shirt, jacket, pants, dress, shorts, skirt, etc.

See the "Get the Fashion Detection Model" section (section 3 above) for more details on obtaining a proper fashion model.

### Running with .NET Aspire

After building the Docker image and exporting the model, you can run the application with .NET Aspire:

```bash
# Navigate to the AppHost directory
cd ../../ADCommsPersonTracking.AppHost

# Run the Aspire orchestrator
dotnet run
```

.NET Aspire will:
- Start the Aspire Dashboard (opens automatically in your browser)
- Run the YOLO11 model export container (if model doesn't exist)
- Run the fashion model export container (if model doesn't exist)
- Launch the API service with both YOLO11 and fashion models
- Launch the Blazor Web UI
- Configure service discovery and health monitoring

Access the services:
- **Aspire Dashboard**: `http://localhost:15000` or `https://localhost:17000`
- **API and Web UI**: Endpoints are shown in the Aspire Dashboard

### Troubleshooting

#### "Unable to find image 'yolo-model-export:latest' or 'fashion-model-export:latest' locally"

**Error Message:**
```
Unable to find image 'yolo-model-export:latest' locally
Error response from daemon: pull access denied for yolo-model-export, repository does not exist or may require 'docker login'
```
or
```
Unable to find image 'fashion-model-export:latest' locally
Error response from daemon: pull access denied for fashion-model-export, repository does not exist or may require 'docker login'
```

**Solution:** The Docker images need to be built locally first. Follow the build instructions above:
```bash
cd docker/yolo-model-export
docker build -t yolo-model-export:latest .
cd ../fashion-model-export
docker build -t fashion-model-export:latest .
```

#### "Permission denied" When Mounting Volumes

**Error Message:**
```
docker: Error response from daemon: error while creating mount source path: mkdir permission denied
```

**Solution:** 
1. Open Docker Desktop settings
2. Go to **Resources** > **File Sharing** (on Windows/Mac)
3. Ensure the repository directory is in the list of shared paths
4. Click **Apply & Restart**

Alternatively, use an absolute path for the volume mount:
```bash
docker run --rm -v "/absolute/path/to/models:/models" yolo-model-export:latest
```

#### Model Export Takes Too Long

**Issue:** The first run may take several minutes.

**Explanation:** On the first run, the containers download pre-trained weights from the internet:
- YOLO11 model: ~136MB
- Fashion model placeholder: ~6MB

Subsequent runs will be much faster if the models already exist.

**Solution:** 
- Ensure you have a stable internet connection
- Wait for the downloads to complete (typically 2-5 minutes for YOLO11, less than 1 minute for fashion model)
- If the download fails, delete any partial files in the `models/` directory and try again

#### Docker Daemon Not Running

**Error Message:**
```
Cannot connect to the Docker daemon. Is the docker daemon running?
```

**Solution:**
- Start Docker Desktop
- Wait for Docker to fully start (check the system tray icon)
- Verify with: `docker ps`

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
      "description": "yellow, jacket, black, hat, suitcase",
      "matchScore": 0.89
    }
  ],
  "annotatedImageBase64": "base64-encoded-jpeg-image-with-bounding-boxes-drawn",
  "processingMessage": "Processed frame with 1 person detections"
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

Edit `appsettings.json` to configure the application:

```json
{
  "ObjectDetection": {
    "ModelPath": "models/yolo11x.onnx",
    "ConfidenceThreshold": 0.45,
    "AccessoryConfidenceThreshold": 0.25,
    "IouThreshold": 0.5
  },
  "ClothingDetection": {
    "Enabled": false,
    "ModelPath": "models/fashion-yolo.onnx",
    "ConfidenceThreshold": 0.5
  },
  "ImageAnnotation": {
    "BoxColor": "#00FF00",
    "BoxThickness": 2,
    "ShowLabels": true,
    "FontSize": 12
  }
}
```

### Clothing Detection Configuration

The `ClothingDetection` section configures the optional fashion-trained YOLO model:

- **Enabled**: Set to `true` to enable clothing detection (default: `false`)
- **ModelPath**: Path to the fashion ONNX model (default: `models/fashion-yolo.onnx`)
- **ConfidenceThreshold**: Minimum confidence for clothing detections (default: `0.5`)

**Note**: Clothing detection requires a fashion-trained YOLO model. The standard COCO-trained YOLO11 model only detects a very limited set of clothing items (tie). See the "Get the Fashion Detection Model" section above for setup instructions.

### Model Selection

The system supports different YOLO11 model sizes:

- **yolo11n.onnx**: Nano - Fastest, lowest accuracy (for old hardware)
- **yolo11s.onnx**: Small - Good balance
- **yolo11m.onnx**: Medium - Better accuracy
- **yolo11l.onnx**: Large - High accuracy
- **yolo11x.onnx**: Extra Large - Best accuracy, best for accessory detection (default, recommended)

Update the `ModelPath` in `appsettings.json` to use different models.

### Performance Tuning

For old hardware, consider:

1. Use `yolo11n.onnx` (nano model) or `yolo11s.onnx` (small model)
2. Reduce input frame resolution before sending to API
3. Process frames at lower FPS (e.g., 2-5 FPS instead of 30 FPS)
4. Use CPU optimization flags in ONNX Runtime

## How It Works

1. **Frame Reception**: Camera sends video frame (base64 encoded) with search prompt
2. **Object Detection**: YOLO11 detects all persons in the frame
3. **Feature Extraction**: Rule-based extractor parses the prompt to identify:
   - Colors (green, blue, red, etc.)
   - Clothing items (jacket, pants, hat, etc.)
   - Accessories (backpack, suitcase, umbrella, etc.)
   - Physical attributes (height, build)
4. **Attribute Analysis**: For each detected person:
   - Color analysis on person bounding box regions
   - YOLO11 detects accessories (backpack, handbag, suitcase)
   - Optional fashion model detects clothing items (if enabled)
   - Physical attribute estimation (height, build)
5. **Matching**: Filters persons based on extracted criteria from prompt
6. **Image Annotation**: Draws bounding boxes on the original image
7. **Tracking**: System assigns tracking IDs and maintains person trajectories
8. **Response**: Returns annotated image with bounding boxes and detection data

## Important Note: Clothing Detection Limitations

**Standard YOLO Models and Clothing Detection**:
- The standard YOLO11 model (trained on COCO dataset) can only detect a very limited set of clothing/accessory items:
  - Accessories: backpack, handbag, suitcase
  - Clothing: tie (that's the only clothing item!)
- Items like jacket, shirt, pants, dress, shorts, etc. are **NOT detectable** with the standard COCO-trained YOLO model.

**Fashion Model Required for Clothing Detection**:
- To enable detection of actual clothing items (jacket, shirt, pants, dress, etc.), you need to set up a fashion-trained YOLO model.
- The system includes a `ClothingDetectionService` that can use fashion-trained models.
- By default, clothing detection is **disabled** (set `ClothingDetection:Enabled: false` in configuration).
- See the "Get the Fashion Detection Model" section for setup instructions.

**What Works Without Fashion Model**:
- Person detection
- Color analysis (dominant colors on person's clothing)
- Accessory detection (backpack, handbag, suitcase via YOLO11)
- Physical attribute estimation (height, build)

**What Requires Fashion Model**:
- Specific clothing item detection (jacket, shirt, pants, dress, etc.)
- Searching by clothing criteria like "person with blue jacket"

## Testing

### Unit Tests
The project includes 156+ comprehensive unit tests covering:
- Object detection service (YOLO11 integration)
- Person tracking and ID assignment
- Image annotation and bounding box drawing
- Color analysis and feature extraction
- Controller endpoints

Run unit tests only:
```bash
dotnet test --filter "Category!=Integration"
```

### Integration Tests
Real integration tests using **Testcontainers** with an actual YOLO11 ONNX model running in Docker:
- Container health checks
- Person detection on real images
- Confidence threshold validation
- Bounding box format verification
- Model information queries

**Requirements**: Docker must be running and YOLO11 model downloaded.

See [INTEGRATION_TESTS.md](INTEGRATION_TESTS.md) for detailed instructions.

Run integration tests:
```bash
python download-model.py  # Download model first
dotnet test --filter "Category=Integration"
```

## Cross-Camera Tracking

The system maintains person tracks across cameras by:

1. Assigning unique tracking IDs to detected persons
2. Matching persons across frames using spatial proximity
3. Maintaining track history for 5 minutes
4. Associating descriptions with tracks for cross-camera matching

## Performance Considerations

### For Old Hardware

- **Model**: Use YOLO11n (nano) or YOLO11s (small) for best performance
- **Resolution**: Process frames at 640x640 or lower
- **Frame Rate**: 2-5 FPS is sufficient for tracking
- **Batch Processing**: Process multiple frames from different cameras in batches

### Memory Usage

- YOLO11m: ~50MB model size, ~400MB runtime memory
- YOLO11n: ~6MB model size, ~200MB runtime memory (for older hardware)
- Tracking: ~1KB per active track

## Troubleshooting

### Model Not Found

If you see "YOLO11 ONNX model not found", the system will use mock detections. Download and place the YOLO11 model at the configured path as described in the installation section.

### Fashion Model Issues

**Fashion model not loading:**
1. Check that `ClothingDetection:Enabled` is set to `true` in `appsettings.json`
2. Verify the fashion model exists at `models/fashion-yolo.onnx`
3. Run `python3 download-fashion-model.py` to download the model
4. Check logs for model loading errors

**No clothing detected:**
1. Lower the confidence threshold: `ClothingDetection:ConfidenceThreshold` (try 0.25)
2. Ensure persons are clearly visible in frames
3. Check that the fashion model successfully loaded (look for log message)
4. Fashion detection runs on cropped person images - ensure person bounding boxes are accurate

**Color+Clothing queries not matching:**
1. Verify fashion model is enabled and loaded
2. Check that both color AND clothing are specified (e.g., "blue jacket")
3. The system falls back to original matching if fashion model returns no results
4. Try lowering the confidence threshold if clothing items aren't being detected
5. Check diagnostics to see what colors are detected on each clothing item

### Low Detection Accuracy

- Ensure adequate lighting in camera feeds
- Use higher resolution frames
- Consider using a larger YOLO11 model (s, m, l variants)
- Adjust the `ConfidenceThreshold` in `appsettings.json` (default: 0.45 for YOLO11, 0.3 for fashion)

### No Bounding Boxes Visible

- Check that `ImageAnnotation:ShowLabels` is set to `true`
- Verify the `BoxColor` is visible against your image background
- Increase `BoxThickness` if boxes are too thin

### Aspire Container Issues

**Fashion model export container fails:**
1. Check Docker is running: `docker ps`
2. Ensure internet access for HuggingFace download
3. Check container logs: `docker logs fashion-model-export`
4. Manually run export: `cd docker/fashion-model-export && docker build -t fashion-model-export . && docker run -v /path/to/models:/models fashion-model-export`

**Model not shared between containers:**
1. Verify the `models/` directory exists in the solution root
2. Check volume mounts in Aspire configuration
3. Ensure both export containers use the same mount path: `/models`

## License

See LICENSE file for details.

## Contributing

Contributions are welcome! Please submit pull requests with clear descriptions of changes.