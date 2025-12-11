using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;

namespace ADCommsPersonTracking.Api.Services;

public class PersonTrackingService : IPersonTrackingService
{
    private readonly IObjectDetectionService _detectionService;
    private readonly IPromptFeatureExtractor _featureExtractor;
    private readonly IImageAnnotationService _annotationService;
    private readonly ILogger<PersonTrackingService> _logger;
    private readonly ConcurrentDictionary<string, PersonTrack> _activeTracks = new();
    private readonly TimeSpan _trackTimeout = TimeSpan.FromMinutes(5);

    public PersonTrackingService(
        IObjectDetectionService detectionService,
        IPromptFeatureExtractor featureExtractor,
        IImageAnnotationService annotationService,
        ILogger<PersonTrackingService> logger)
    {
        _detectionService = detectionService;
        _featureExtractor = featureExtractor;
        _annotationService = annotationService;
        _logger = logger;
    }

    public async Task<TrackingResponse> ProcessFrameAsync(TrackingRequest request)
    {
        try
        {
            _logger.LogInformation("Processing frame from camera {CameraId} with prompt: {Prompt}", 
                request.CameraId, request.Prompt);

            // Decode base64 image
            var imageBytes = Convert.FromBase64String(request.ImageBase64);

            // Detect persons in the frame using YOLO11
            var detections = await _detectionService.DetectPersonsAsync(imageBytes);
            _logger.LogInformation("Detected {Count} persons in frame", detections.Count);

            // Extract search features from the prompt using rule-based extraction
            var searchCriteria = _featureExtractor.ExtractFeatures(request.Prompt);
            var searchFeatures = new List<string>();
            searchFeatures.AddRange(searchCriteria.Colors);
            searchFeatures.AddRange(searchCriteria.ClothingItems);
            searchFeatures.AddRange(searchCriteria.Accessories);
            searchFeatures.AddRange(searchCriteria.PhysicalAttributes);
            if (searchCriteria.Height != null)
            {
                searchFeatures.Add($"height: {searchCriteria.Height.OriginalText}");
            }
            _logger.LogInformation("Extracted {Count} search features from prompt", searchFeatures.Count);

            // For now, return all detected persons since YOLO11 detects persons but not specific attributes
            // In a production system, you would need a separate attribute detection model
            var matchedDetections = detections;

            // Annotate the image with bounding boxes
            var annotatedImageBase64 = await _annotationService.AnnotateImageAsync(imageBytes, matchedDetections);

            // Build response with matched detections
            var response = new TrackingResponse
            {
                CameraId = request.CameraId,
                Timestamp = request.Timestamp,
                AnnotatedImageBase64 = annotatedImageBase64,
                ProcessingMessage = $"Processed frame with {detections.Count} person detections. Note: Currently returns all detected persons; attribute-based filtering requires additional ML model."
            };

            // Add all detected persons to response
            foreach (var detection in matchedDetections)
            {
                var trackingId = GetOrCreateTrackingId(request.CameraId, detection, request.Timestamp);

                response.Detections.Add(new Detection
                {
                    TrackingId = trackingId,
                    BoundingBox = detection,
                    Description = string.Join(", ", searchFeatures),
                    MatchScore = detection.Confidence
                });

                // Update tracking information
                UpdateTrack(trackingId, request.CameraId, detection, searchFeatures, request.Timestamp);
            }

            // Clean up old tracks
            CleanupOldTracks();

            _logger.LogInformation("Returning {Count} detections with annotated image", response.Detections.Count);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame");
            return new TrackingResponse
            {
                CameraId = request.CameraId,
                Timestamp = request.Timestamp,
                ProcessingMessage = $"Error: {ex.Message}"
            };
        }
    }

    public Task<List<PersonTrack>> GetActiveTracksAsync()
    {
        var now = DateTime.UtcNow;
        var activeTracks = _activeTracks.Values
            .Where(t => now - t.LastSeen < _trackTimeout)
            .OrderByDescending(t => t.LastSeen)
            .ToList();

        return Task.FromResult(activeTracks);
    }

    public Task<PersonTrack?> GetTrackByIdAsync(string trackingId)
    {
        _activeTracks.TryGetValue(trackingId, out var track);
        return Task.FromResult(track);
    }

    private string GetOrCreateTrackingId(string cameraId, BoundingBox detection, DateTime timestamp)
    {
        // Simple tracking: try to match with existing tracks based on spatial proximity
        var existingTrack = _activeTracks.Values
            .Where(t => t.CameraId == cameraId)
            .Where(t => timestamp - t.LastSeen < TimeSpan.FromSeconds(10))
            .OrderBy(t => CalculateDistance(t.LastKnownPosition, detection))
            .FirstOrDefault();

        if (existingTrack != null && CalculateDistance(existingTrack.LastKnownPosition, detection) < 100)
        {
            return existingTrack.TrackingId;
        }

        // Create new tracking ID
        return $"track_{cameraId}_{Guid.NewGuid():N}";
    }

    private void UpdateTrack(string trackingId, string cameraId, BoundingBox position, 
        List<string> features, DateTime timestamp)
    {
        _activeTracks.AddOrUpdate(trackingId,
            _ => new PersonTrack
            {
                TrackingId = trackingId,
                CameraId = cameraId,
                FirstSeen = timestamp,
                LastSeen = timestamp,
                LastKnownPosition = position,
                Description = string.Join(", ", features),
                Features = features
            },
            (_, existing) =>
            {
                existing.LastSeen = timestamp;
                existing.LastKnownPosition = position;
                existing.CameraId = cameraId;
                return existing;
            });
    }

    private void CleanupOldTracks()
    {
        var now = DateTime.UtcNow;
        var oldTracks = _activeTracks
            .Where(kvp => now - kvp.Value.LastSeen > _trackTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var trackId in oldTracks)
        {
            _activeTracks.TryRemove(trackId, out _);
            _logger.LogDebug("Removed old track {TrackId}", trackId);
        }
    }

    private float CalculateDistance(BoundingBox box1, BoundingBox box2)
    {
        var cx1 = box1.X + box1.Width / 2;
        var cy1 = box1.Y + box1.Height / 2;
        var cx2 = box2.X + box2.Width / 2;
        var cy2 = box2.Y + box2.Height / 2;

        return (float)Math.Sqrt(Math.Pow(cx1 - cx2, 2) + Math.Pow(cy1 - cy2, 2));
    }
}
