using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;

namespace ADCommsPersonTracking.Api.Services;

public class PersonTrackingService : IPersonTrackingService
{
    private readonly IObjectDetectionService _detectionService;
    private readonly IPromptFeatureExtractor _featureExtractor;
    private readonly IImageAnnotationService _annotationService;
    private readonly IColorAnalysisService _colorAnalysisService;
    private readonly ILogger<PersonTrackingService> _logger;
    private readonly ConcurrentDictionary<string, PersonTrack> _activeTracks = new();
    private readonly TimeSpan _trackTimeout = TimeSpan.FromMinutes(5);

    public PersonTrackingService(
        IObjectDetectionService detectionService,
        IPromptFeatureExtractor featureExtractor,
        IImageAnnotationService annotationService,
        IColorAnalysisService colorAnalysisService,
        ILogger<PersonTrackingService> logger)
    {
        _detectionService = detectionService;
        _featureExtractor = featureExtractor;
        _annotationService = annotationService;
        _colorAnalysisService = colorAnalysisService;
        _logger = logger;
    }

    public async Task<TrackingResponse> ProcessFrameAsync(TrackingRequest request)
    {
        try
        {
            _logger.LogInformation("Processing {Count} images with prompt: {Prompt}", 
                request.ImagesBase64.Count, request.Prompt);

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
            _logger.LogInformation("Extracted {Count} search features from prompt (colors: {Colors})", 
                searchFeatures.Count, string.Join(", ", searchCriteria.Colors));

            var response = new TrackingResponse
            {
                Timestamp = request.Timestamp,
                Results = new List<ImageDetectionResult>()
            };

            var totalDetections = 0;
            var totalMatchedDetections = 0;

            // Process each image
            for (int imageIndex = 0; imageIndex < request.ImagesBase64.Count; imageIndex++)
            {
                var imageBase64 = request.ImagesBase64[imageIndex];
                var imageBytes = Convert.FromBase64String(imageBase64);

                // Detect persons in the frame using YOLO11
                var detections = await _detectionService.DetectPersonsAsync(imageBytes);
                totalDetections += detections.Count;
                _logger.LogInformation("Detected {Count} persons in image {Index}", detections.Count, imageIndex);

                // Filter detections based on color criteria
                var matchedDetections = new List<BoundingBox>();
                var detectionResults = new List<Detection>();

                foreach (var detection in detections)
                {
                    // Analyze colors for this person
                    var colorProfile = await _colorAnalysisService.AnalyzePersonColorsAsync(imageBytes, detection);

                    // Check if person matches color criteria
                    var hasColorCriteria = searchCriteria.Colors.Count > 0;
                    var matchesColors = _colorAnalysisService.MatchesColorCriteria(colorProfile, searchCriteria.Colors);

                    // If no color criteria specified, include all detections
                    // If color criteria specified, only include matching detections
                    if (!hasColorCriteria || matchesColors)
                    {
                        matchedDetections.Add(detection);
                        totalMatchedDetections++;

                        var trackingId = GetOrCreateTrackingId(detection, request.Timestamp);
                        var matchedCriteria = new List<string>();
                        
                        if (matchesColors && hasColorCriteria)
                        {
                            // Add the specific colors that matched from all regions (upper body, lower body, and overall)
                            var allDetectedColorNames = colorProfile.UpperBodyColors
                                .Concat(colorProfile.LowerBodyColors)
                                .Concat(colorProfile.OverallColors)
                                .Select(c => c.ColorName.ToLowerInvariant())
                                .ToHashSet();
                            matchedCriteria.AddRange(searchCriteria.Colors.Where(c => allDetectedColorNames.Contains(c.ToLowerInvariant())));
                        }

                        detectionResults.Add(new Detection
                        {
                            TrackingId = trackingId,
                            BoundingBox = detection,
                            Description = string.Join(", ", searchFeatures),
                            MatchScore = detection.Confidence,
                            MatchedCriteria = matchedCriteria
                        });

                        // Update tracking information
                        UpdateTrack(trackingId, detection, searchFeatures, request.Timestamp);
                    }
                }

                // Annotate the image with matched detections only
                var annotatedImageBase64 = await _annotationService.AnnotateImageAsync(imageBytes, matchedDetections);

                response.Results.Add(new ImageDetectionResult
                {
                    ImageIndex = imageIndex,
                    Detections = detectionResults,
                    AnnotatedImageBase64 = annotatedImageBase64
                });
            }

            // Clean up old tracks
            CleanupOldTracks();

            response.ProcessingMessage = searchCriteria.Colors.Count > 0
                ? $"Processed {request.ImagesBase64.Count} images with {totalDetections} person detections. Filtered to {totalMatchedDetections} persons matching color criteria: {string.Join(", ", searchCriteria.Colors)}."
                : $"Processed {request.ImagesBase64.Count} images with {totalDetections} person detections. No color filtering applied.";

            _logger.LogInformation("Returning {Count} matched detections out of {Total} total detections", 
                totalMatchedDetections, totalDetections);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame");
            return new TrackingResponse
            {
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

    private string GetOrCreateTrackingId(BoundingBox detection, DateTime timestamp)
    {
        // Simple tracking: try to match with existing tracks based on spatial proximity
        var existingTrack = _activeTracks.Values
            .Where(t => timestamp - t.LastSeen < TimeSpan.FromSeconds(10))
            .OrderBy(t => CalculateDistance(t.LastKnownPosition, detection))
            .FirstOrDefault();

        if (existingTrack != null && CalculateDistance(existingTrack.LastKnownPosition, detection) < 100)
        {
            return existingTrack.TrackingId;
        }

        // Create new tracking ID
        return $"track_{Guid.NewGuid():N}";
    }

    private void UpdateTrack(string trackingId, BoundingBox position, 
        List<string> features, DateTime timestamp)
    {
        _activeTracks.AddOrUpdate(trackingId,
            _ => new PersonTrack
            {
                TrackingId = trackingId,
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
