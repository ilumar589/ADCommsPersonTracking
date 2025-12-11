using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;

namespace ADCommsPersonTracking.Api.Services;

public class PersonTrackingService : IPersonTrackingService
{
    private readonly IObjectDetectionService _detectionService;
    private readonly ITrackingLlmService _llmService;
    private readonly ILogger<PersonTrackingService> _logger;
    private readonly ConcurrentDictionary<string, PersonTrack> _activeTracks = new();
    private readonly TimeSpan _trackTimeout = TimeSpan.FromMinutes(5);

    public PersonTrackingService(
        IObjectDetectionService detectionService,
        ITrackingLlmService llmService,
        ILogger<PersonTrackingService> logger)
    {
        _detectionService = detectionService;
        _llmService = llmService;
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

            // Detect persons in the frame
            var detections = await _detectionService.DetectPersonsAsync(imageBytes);
            _logger.LogInformation("Detected {Count} persons in frame", detections.Count);

            // Extract search features from the prompt using LLM
            var searchFeatures = await _llmService.ExtractSearchFeaturesAsync(request.Prompt);
            _logger.LogInformation("Extracted {Count} search features from prompt", searchFeatures.Count);

            // Create detection descriptions for LLM to analyze
            var detectionDescriptions = detections.Select((d, i) => 
                $"Person at position ({d.X:F0}, {d.Y:F0}), size: {d.Width:F0}x{d.Height:F0}, confidence: {d.Confidence:F2}")
                .ToList();

            // Use LLM to match detections to the prompt
            var matchingIndices = new List<string>();
            if (detectionDescriptions.Any())
            {
                matchingIndices = await _llmService.MatchDetectionsToPromptAsync(
                    request.Prompt, detectionDescriptions);
            }

            // Build response with matched detections
            var response = new TrackingResponse
            {
                CameraId = request.CameraId,
                Timestamp = request.Timestamp,
                ProcessingMessage = $"Processed frame with {detections.Count} detections, {matchingIndices.Count} matches found"
            };

            // Add matched detections to response
            foreach (var indexStr in matchingIndices)
            {
                if (int.TryParse(indexStr, out int index) && index >= 0 && index < detections.Count)
                {
                    var detection = detections[index];
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
            }

            // Clean up old tracks
            CleanupOldTracks();

            _logger.LogInformation("Returning {Count} matched detections", response.Detections.Count);
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
