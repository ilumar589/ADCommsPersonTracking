using ADCommsPersonTracking.Api.Helpers;
using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using System.Collections.Concurrent;

namespace ADCommsPersonTracking.Api.Services;

public class PersonTrackingService : IPersonTrackingService
{
    private readonly IObjectDetectionService _detectionService;
    private readonly IPromptFeatureExtractor _featureExtractor;
    private readonly IImageAnnotationService _annotationService;
    private readonly IColorAnalysisService _colorAnalysisService;
    private readonly IAccessoryDetectionService _accessoryDetectionService;
    private readonly IPhysicalAttributeEstimator _physicalAttributeEstimator;
    private readonly IInferenceDiagnosticsService _diagnosticsService;
    private readonly ILogger<PersonTrackingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly ConcurrentDictionary<string, PersonTrack> _activeTracks = new();
    private readonly TimeSpan _trackTimeout = TimeSpan.FromMinutes(5);
    private readonly int _maxDegreeOfParallelism;

    public PersonTrackingService(
        IObjectDetectionService detectionService,
        IPromptFeatureExtractor featureExtractor,
        IImageAnnotationService annotationService,
        IColorAnalysisService colorAnalysisService,
        IAccessoryDetectionService accessoryDetectionService,
        IPhysicalAttributeEstimator physicalAttributeEstimator,
        IInferenceDiagnosticsService diagnosticsService,
        IConfiguration configuration,
        ILogger<PersonTrackingService> logger)
    {
        _detectionService = detectionService;
        _featureExtractor = featureExtractor;
        _annotationService = annotationService;
        _colorAnalysisService = colorAnalysisService;
        _accessoryDetectionService = accessoryDetectionService;
        _physicalAttributeEstimator = physicalAttributeEstimator;
        _diagnosticsService = diagnosticsService;
        _configuration = configuration;
        _logger = logger;
        _maxDegreeOfParallelism = configuration.GetValue("Processing:MaxDegreeOfParallelism", Environment.ProcessorCount);
    }

    public async Task<TrackingResponse> ProcessFrameAsync(TrackingRequest request)
    {
        var diagnosticsSessionId = request.DiagnosticsSessionId;
        var hasDiagnostics = !string.IsNullOrEmpty(diagnosticsSessionId) && _diagnosticsService.IsEnabled;
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogProcessFrameStart(request.ImagesBase64.Count, request.Prompt);
            _logger.LogProcessingImages(request.ImagesBase64.Count, request.Prompt);

            // Extract search features from the prompt using rule-based extraction
            var searchCriteria = _featureExtractor.ExtractFeatures(request.Prompt);
            
            // Record extracted criteria in diagnostics
            if (hasDiagnostics)
            {
                _diagnosticsService.SetSearchCriteria(diagnosticsSessionId!, searchCriteria);
            }
            var searchFeatures = new List<string>();
            searchFeatures.AddRange(searchCriteria.Colors);
            searchFeatures.AddRange(searchCriteria.ClothingItems);
            searchFeatures.AddRange(searchCriteria.Accessories);
            searchFeatures.AddRange(searchCriteria.PhysicalAttributes);
            if (searchCriteria.Height != null)
            {
                searchFeatures.Add($"height: {searchCriteria.Height.Value.OriginalText}");
            }
            _logger.LogExtractedSearchFeatures(searchFeatures.Count, string.Join(", ", searchCriteria.Colors));

            var response = new TrackingResponse
            {
                Timestamp = request.Timestamp,
                Results = new List<ImageDetectionResult>()
            };

            var totalDetections = 0;
            var totalMatchedDetections = 0;
            var totalAccessoriesDetected = 0;
            var resultsLock = new object();

            // Check if we have any criteria
            var hasCriteria = searchCriteria.Colors.Count > 0 || 
                             searchCriteria.ClothingItems.Count > 0 || 
                             searchCriteria.Accessories.Count > 0 || 
                             searchCriteria.PhysicalAttributes.Count > 0 ||
                             searchCriteria.Height != null;

            _logger.LogCriteriaBreakdown(
                hasCriteria,
                searchCriteria.Colors.Count,
                searchCriteria.ClothingItems.Count,
                searchCriteria.Accessories.Count,
                searchCriteria.PhysicalAttributes.Count,
                searchCriteria.Height != null);

            // Process images in parallel
            var parallelOptions = new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _maxDegreeOfParallelism 
            };

            var imageResults = new ConcurrentBag<ImageDetectionResult>();

            await Parallel.ForEachAsync(
                request.ImagesBase64.Select((img, idx) => new { Image = img, Index = idx }),
                parallelOptions,
                async (imageData, cancellationToken) =>
                {
                    try
                    {
                        var imageBase64 = imageData.Image;
                        var imageIndex = imageData.Index;
                        var imageBytes = Convert.FromBase64String(imageBase64);

                        // Get image dimensions first for logging
                        int imageHeight, imageWidth;
                        using (var ms = new MemoryStream(imageBytes))
                        using (var image = await SixLabors.ImageSharp.Image.LoadAsync(ms, cancellationToken))
                        {
                            imageHeight = image.Height;
                            imageWidth = image.Width;
                        }

                        _logger.LogImageProcessingStart(imageIndex, imageWidth, imageHeight, imageBase64.Length);

                        // Create image diagnostics if enabled
                        ImageProcessingDiagnostics? imageDiagnostics = null;
                        if (hasDiagnostics)
                        {
                            imageDiagnostics = new ImageProcessingDiagnostics
                            {
                                ImageIndex = imageIndex,
                                ImageWidth = imageWidth,
                                ImageHeight = imageHeight
                            };
                        }

                        // Detect persons and accessories in the frame using YOLO11
                        // Only use new method if searching for accessories
                        List<BoundingBox> detections;
                        List<DetectedObject> allAccessories = new();
                        List<DetectedObject> allObjects = new();
                        
                        var takingAccessoryPath = searchCriteria.Accessories.Count > 0 || searchCriteria.ClothingItems.Count > 0;
                        _logger.LogAccessoryDetectionPath(imageIndex, takingAccessoryPath, searchCriteria.Accessories.Count, searchCriteria.ClothingItems.Count);
                        
                        if (takingAccessoryPath)
                        {
                            allObjects = await _detectionService.DetectObjectsAsync(imageBytes);
                            detections = allObjects.Where(o => o.ClassId == 0).Select(o => o.BoundingBox).ToList();
                            allAccessories = allObjects.Where(o => o.ClassId != 0).ToList();

                            _logger.LogYoloDetectionSummary(imageIndex, allObjects.Count, detections.Count, allAccessories.Count);
                            
                            // Log warning if searching for accessories but none found
                            if (allAccessories.Count == 0 && (searchCriteria.Accessories.Count > 0 || searchCriteria.ClothingItems.Count > 0))
                            {
                                var searchedItems = string.Join(", ", searchCriteria.Accessories.Concat(searchCriteria.ClothingItems));
                                _logger.LogNoAccessoriesDetectedWarning(imageIndex, searchedItems);
                            }
                            
                            lock (resultsLock)
                            {
                                totalAccessoriesDetected += allAccessories.Count;
                            }

                            // Log each YOLO detection
                            foreach (var obj in allObjects)
                            {
                                _logger.LogYoloDetection(
                                    obj.ClassId,
                                    obj.ObjectType,
                                    obj.BoundingBox.Confidence,
                                    obj.BoundingBox.X,
                                    obj.BoundingBox.Y,
                                    obj.BoundingBox.Width,
                                    obj.BoundingBox.Height);
                            }
                            
                            // Populate diagnostics with all YOLO detections
                            if (imageDiagnostics != null)
                            {
                                foreach (var obj in allObjects)
                                {
                                    imageDiagnostics.AllYoloDetections.Add(new YoloDetectionDiagnostics
                                    {
                                        ClassId = obj.ClassId,
                                        ClassName = obj.ObjectType,
                                        Confidence = obj.BoundingBox.Confidence,
                                        BoundingBox = new BoundingBoxDiagnostics
                                        {
                                            X = obj.BoundingBox.X,
                                            Y = obj.BoundingBox.Y,
                                            Width = obj.BoundingBox.Width,
                                            Height = obj.BoundingBox.Height,
                                            Confidence = obj.BoundingBox.Confidence
                                        }
                                    });
                                }
                            }
                        }
                        else
                        {
                            detections = await _detectionService.DetectPersonsAsync(imageBytes);
                        }
                        
                        lock (resultsLock)
                        {
                            totalDetections += detections.Count;
                        }
                        
                        _logger.LogDetectedPersonsInImage(detections.Count, imageIndex);

                    // Process detections in parallel within the image
                    var detectionResults = new ConcurrentBag<(Detection detection, BoundingBox box, int personIndex)>();

                    await Parallel.ForEachAsync(
                        detections.Select((d, idx) => new { Detection = d, Index = idx }),
                        new ParallelOptions { MaxDegreeOfParallelism = _maxDegreeOfParallelism },
                        async (detectionData, ct) =>
                        {
                            var detection = detectionData.Detection;
                            var idx = detectionData.Index;

                            // Analyze all attributes for this person in parallel
                            var colorTask = _colorAnalysisService.AnalyzePersonColorsAsync(imageBytes, detection);
                            var physicalTask = _physicalAttributeEstimator.EstimateAttributesAsync(imageBytes, detection, imageHeight, imageWidth);

                            await Task.WhenAll(colorTask, physicalTask);

                            var colorProfile = await colorTask;
                            var physicalAttributes = await physicalTask;

                            // Log color analysis results
                            _logger.LogPersonColorAnalysis(
                                idx,
                                string.Join(", ", colorProfile.UpperBodyColors.Select(c => c.ColorName)),
                                string.Join(", ", colorProfile.LowerBodyColors.Select(c => c.ColorName)),
                                string.Join(", ", colorProfile.OverallColors.Select(c => c.ColorName)));

                            // Log physical attributes
                            _logger.LogPersonPhysicalAttributes(
                                idx,
                                string.Join(", ", physicalAttributes.AllAttributes),
                                physicalAttributes.EstimatedHeightMeters);
                            
                            // Create person diagnostics if enabled
                            PersonDetectionDiagnostics? personDiagnostics = null;
                            AccessoryMatchingDiagnostics? accessoryDiagnostics = null;
                            if (imageDiagnostics != null)
                            {
                                personDiagnostics = new PersonDetectionDiagnostics
                                {
                                    PersonIndex = idx,
                                    PersonBox = new BoundingBoxDiagnostics
                                    {
                                        X = detection.X,
                                        Y = detection.Y,
                                        Width = detection.Width,
                                        Height = detection.Height,
                                        Confidence = detection.Confidence
                                    },
                                    ColorAnalysis = new ColorAnalysisDiagnostics
                                    {
                                        UpperBodyColors = colorProfile.UpperBodyColors.Select(c => c.ColorName).ToList(),
                                        LowerBodyColors = colorProfile.LowerBodyColors.Select(c => c.ColorName).ToList(),
                                        OverallColors = colorProfile.OverallColors.Select(c => c.ColorName).ToList()
                                    }
                                };
                                accessoryDiagnostics = new AccessoryMatchingDiagnostics();
                                personDiagnostics.AccessoryMatching = accessoryDiagnostics;
                            }

                            // Detect accessories using YOLO-detected accessories if available
                            AccessoryDetectionResult accessoryResult;
                            if (allAccessories.Count > 0)
                            {
                                accessoryResult = _accessoryDetectionService.DetectAccessoriesFromYolo(detection, allAccessories, accessoryDiagnostics);
                            }
                            else
                            {
                                accessoryResult = await _accessoryDetectionService.DetectAccessoriesAsync(imageBytes, detection);
                            }

                            // Check if person matches ALL criteria
                            var matchesColors = !hasCriteria || searchCriteria.Colors.Count == 0 || 
                                              _colorAnalysisService.MatchesColorCriteria(colorProfile, searchCriteria.Colors);
                            var matchesAccessories = searchCriteria.ClothingItems.Count == 0 && searchCriteria.Accessories.Count == 0 ||
                                                    _accessoryDetectionService.MatchesCriteria(accessoryResult, searchCriteria.ClothingItems, searchCriteria.Accessories);
                            var matchesPhysical = (searchCriteria.PhysicalAttributes.Count == 0 && searchCriteria.Height == null) ||
                                                 _physicalAttributeEstimator.MatchesCriteria(physicalAttributes, searchCriteria.PhysicalAttributes, searchCriteria.Height);

                            var overallMatch = !hasCriteria || (matchesColors && matchesAccessories && matchesPhysical);

                            // Log matching results
                            _logger.LogPersonMatchingResult(idx, matchesColors, matchesAccessories, matchesPhysical, overallMatch);
                            
                            // Populate criteria matching diagnostics
                            if (personDiagnostics != null)
                            {
                                // Build detailed match explanations
                                var colorMatchDetails = BuildColorMatchDetails(colorProfile, searchCriteria.Colors, matchesColors);
                                var accessoryMatchDetails = BuildAccessoryMatchDetails(accessoryResult, searchCriteria.ClothingItems, searchCriteria.Accessories, matchesAccessories);
                                var physicalMatchDetails = BuildPhysicalMatchDetails(physicalAttributes, searchCriteria.PhysicalAttributes, searchCriteria.Height, matchesPhysical);
                                
                                personDiagnostics.CriteriaMatching = new CriteriaMatchingDiagnostics
                                {
                                    MatchesColors = matchesColors,
                                    ColorMatchDetails = colorMatchDetails,
                                    MatchesAccessories = matchesAccessories,
                                    AccessoryMatchDetails = accessoryMatchDetails,
                                    MatchesPhysical = matchesPhysical,
                                    PhysicalMatchDetails = physicalMatchDetails,
                                    OverallMatch = overallMatch
                                };
                            }

                            // Include detection only if it matches all specified criteria
                            if (overallMatch)
                            {
                                var trackingId = GetOrCreateTrackingId(detection, request.Timestamp);
                                var matchedCriteria = new List<string>();

                                // Collect matched colors
                                if (matchesColors && searchCriteria.Colors.Count > 0)
                                {
                                    var allDetectedColorNames = colorProfile.UpperBodyColors
                                        .Concat(colorProfile.LowerBodyColors)
                                        .Concat(colorProfile.OverallColors)
                                        .Select(c => c.ColorName.ToLowerInvariant())
                                        .ToHashSet();
                                    matchedCriteria.AddRange(searchCriteria.Colors.Where(c => allDetectedColorNames.Contains(c.ToLowerInvariant())));
                                }

                                // Collect matched accessories
                                if (matchesAccessories && (searchCriteria.Accessories.Count > 0 || searchCriteria.ClothingItems.Count > 0))
                                {
                                    var detectedItems = accessoryResult.Accessories.Concat(accessoryResult.ClothingItems)
                                        .Select(a => a.Label.ToLowerInvariant())
                                        .ToHashSet();
                                    
                                    matchedCriteria.AddRange(searchCriteria.Accessories.Where(a => 
                                        detectedItems.Any(d => d.Contains(a.ToLowerInvariant()) || a.ToLowerInvariant().Contains(d))));
                                    matchedCriteria.AddRange(searchCriteria.ClothingItems.Where(c => 
                                        detectedItems.Any(d => d.Contains(c.ToLowerInvariant()) || c.ToLowerInvariant().Contains(d))));
                                }

                                // Collect matched physical attributes
                                if (matchesPhysical && (searchCriteria.PhysicalAttributes.Count > 0 || searchCriteria.Height != null))
                                {
                                    matchedCriteria.AddRange(physicalAttributes.AllAttributes.Where(a =>
                                        searchCriteria.PhysicalAttributes.Any(s => 
                                            a.ToLowerInvariant().Contains(s.ToLowerInvariant()) || 
                                            s.ToLowerInvariant().Contains(a.ToLowerInvariant()))));
                                    
                                    if (searchCriteria.Height != null)
                                    {
                                        matchedCriteria.Add($"height: ~{physicalAttributes.EstimatedHeightMeters:F2}m");
                                    }
                                }

                                var detectionResult = new Detection
                                {
                                    TrackingId = trackingId,
                                    BoundingBox = detection,
                                    Description = string.Join(", ", searchFeatures),
                                    MatchScore = detection.Confidence,
                                    MatchedCriteria = matchedCriteria
                                };

                                detectionResults.Add((detectionResult, detection, idx));

                                // Update tracking information
                                UpdateTrack(trackingId, detection, searchFeatures, request.Timestamp);

                                lock (resultsLock)
                                {
                                    totalMatchedDetections++;
                                }
                                
                                // Mark as included in diagnostics
                                if (personDiagnostics != null)
                                {
                                    personDiagnostics.WasIncludedInResults = true;
                                    personDiagnostics.ExclusionReason = string.Empty;
                                }
                            }
                            else
                            {
                                // Log why the person was excluded
                                var reasons = new List<string>();
                                if (!matchesColors && searchCriteria.Colors.Count > 0)
                                    reasons.Add("colors don't match");
                                if (!matchesAccessories && (searchCriteria.Accessories.Count > 0 || searchCriteria.ClothingItems.Count > 0))
                                    reasons.Add("accessories/clothing don't match");
                                if (!matchesPhysical && (searchCriteria.PhysicalAttributes.Count > 0 || searchCriteria.Height != null))
                                    reasons.Add("physical attributes don't match");

                                var exclusionReason = string.Join(", ", reasons);
                                _logger.LogPersonExcluded(idx, exclusionReason);
                                
                                // Mark as excluded in diagnostics
                                if (personDiagnostics != null)
                                {
                                    personDiagnostics.WasIncludedInResults = false;
                                    personDiagnostics.ExclusionReason = exclusionReason;
                                }
                            }
                            
                            // Add person diagnostics to image diagnostics
                            if (personDiagnostics != null && imageDiagnostics != null)
                            {
                                lock (imageDiagnostics.PersonAnalysis)
                                {
                                    imageDiagnostics.PersonAnalysis.Add(personDiagnostics);
                                }
                            }
                        });

                    // Convert detection results to lists
                    var matchedDetections = detectionResults.Select(d => d.box).ToList();
                    var detectionsList = detectionResults.Select(d => d.detection).ToList();

                        // Annotate the image with matched detections only
                        var annotatedImageBase64 = await _annotationService.AnnotateImageAsync(imageBytes, matchedDetections);

                        imageResults.Add(new ImageDetectionResult
                        {
                            ImageIndex = imageIndex,
                            Detections = detectionsList,
                            AnnotatedImageBase64 = annotatedImageBase64
                        });
                        
                        // Add image diagnostics to session
                        if (hasDiagnostics && imageDiagnostics != null)
                        {
                            _diagnosticsService.AddImageDiagnostics(diagnosticsSessionId!, imageDiagnostics);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing image {ImageIndex}", imageData.Index);
                        // Add empty result to maintain image index consistency
                        imageResults.Add(new ImageDetectionResult
                        {
                            ImageIndex = imageData.Index,
                            Detections = new List<Detection>(),
                            AnnotatedImageBase64 = string.Empty
                        });
                    }
                });

            // Sort results by image index
            response.Results = imageResults.OrderBy(r => r.ImageIndex).ToList();

            // Clean up old tracks
            CleanupOldTracks();

            // Build processing message
            var criteriaDescription = new List<string>();
            if (searchCriteria.Colors.Count > 0)
                criteriaDescription.Add($"colors: {string.Join(", ", searchCriteria.Colors)}");
            if (searchCriteria.ClothingItems.Count > 0)
                criteriaDescription.Add($"clothing: {string.Join(", ", searchCriteria.ClothingItems)}");
            if (searchCriteria.Accessories.Count > 0)
                criteriaDescription.Add($"accessories: {string.Join(", ", searchCriteria.Accessories)}");
            if (searchCriteria.PhysicalAttributes.Count > 0)
                criteriaDescription.Add($"attributes: {string.Join(", ", searchCriteria.PhysicalAttributes)}");
            if (searchCriteria.Height != null)
                criteriaDescription.Add($"height: {searchCriteria.Height.Value.OriginalText}");

            response.ProcessingMessage = hasCriteria
                ? $"Processed {request.ImagesBase64.Count} images with {totalDetections} person detections. Filtered to {totalMatchedDetections} persons matching criteria: {string.Join("; ", criteriaDescription)}."
                : $"Processed {request.ImagesBase64.Count} images with {totalDetections} person detections. No filtering applied.";

            _logger.LogMatchedDetections(totalMatchedDetections, totalDetections);
            _logger.LogProcessingComplete(totalDetections, totalMatchedDetections, string.Join("; ", criteriaDescription));
            
            // Record processing summary in diagnostics
            if (hasDiagnostics)
            {
                var processingDuration = DateTime.UtcNow - startTime;
                var summary = new ProcessingSummary
                {
                    TotalImagesProcessed = request.ImagesBase64.Count,
                    TotalPersonsDetected = totalDetections,
                    TotalAccessoriesDetected = totalAccessoriesDetected,
                    PersonsMatchingCriteria = totalMatchedDetections,
                    ProcessingDuration = processingDuration
                };
                _diagnosticsService.SetProcessingSummary(diagnosticsSessionId!, summary);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogProcessingError(ex);
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
            _logger.LogRemovedTrack(trackId);
        }
    }

    private float CalculateDistance(BoundingBox box1, BoundingBox box2)
    {
        var cx1 = box1.X + box1.Width / 2;
        var cy1 = box1.Y + box1.Height / 2;
        var cx2 = box2.X + box2.Width / 2;
        var cy2 = box2.Y + box2.Height / 2;

        // Use SIMD-optimized distance calculation
        return SimdMath.CalculateDistance(cx1, cy1, cx2, cy2);
    }

    private string BuildColorMatchDetails(PersonColorProfile colorProfile, List<string> searchColors, bool matches)
    {
        if (searchColors.Count == 0)
        {
            return "No color criteria specified";
        }

        var allDetectedColorNames = colorProfile.UpperBodyColors
            .Concat(colorProfile.LowerBodyColors)
            .Concat(colorProfile.OverallColors)
            .Select(c => c.ColorName.ToLowerInvariant())
            .ToHashSet();

        var detectedColorsStr = string.Join(", ", allDetectedColorNames);
        var searchColorsStr = string.Join(", ", searchColors);

        if (matches)
        {
            var matchedColors = searchColors.Where(c => allDetectedColorNames.Contains(c.ToLowerInvariant())).ToList();
            return $"✓ Searched for: {searchColorsStr}. Detected: {detectedColorsStr}. Matched: {string.Join(", ", matchedColors)}";
        }
        else
        {
            return $"✗ Searched for: {searchColorsStr}. Detected: {detectedColorsStr}. No matches found";
        }
    }

    private string BuildAccessoryMatchDetails(AccessoryDetectionResult accessoryResult, List<string> searchClothing, List<string> searchAccessories, bool matches)
    {
        if (searchClothing.Count == 0 && searchAccessories.Count == 0)
        {
            return "No accessory/clothing criteria specified";
        }

        var detectedItems = accessoryResult.Accessories.Concat(accessoryResult.ClothingItems)
            .Select(a => a.Label.ToLowerInvariant())
            .ToList();

        var detectedItemsStr = detectedItems.Count > 0 ? string.Join(", ", detectedItems) : "none";
        var searchItemsStr = string.Join(", ", searchClothing.Concat(searchAccessories));

        if (matches)
        {
            var matchedItems = searchAccessories.Concat(searchClothing)
                .Where(s => detectedItems.Any(d => d.Contains(s.ToLowerInvariant()) || s.ToLowerInvariant().Contains(d)))
                .ToList();
            return $"✓ Searched for: {searchItemsStr}. Detected: {detectedItemsStr}. Matched: {string.Join(", ", matchedItems)}";
        }
        else
        {
            return $"✗ Searched for: {searchItemsStr}. Detected: {detectedItemsStr}. No matches found";
        }
    }

    private string BuildPhysicalMatchDetails(PhysicalAttributes physicalAttributes, List<string> searchPhysical, HeightInfo? searchHeight, bool matches)
    {
        if (searchPhysical.Count == 0 && searchHeight == null)
        {
            return "No physical attribute criteria specified";
        }

        var detectedAttrs = string.Join(", ", physicalAttributes.AllAttributes);
        var searchCriteria = new List<string>();
        
        if (searchPhysical.Count > 0)
        {
            searchCriteria.Add($"attributes: {string.Join(", ", searchPhysical)}");
        }
        if (searchHeight != null)
        {
            searchCriteria.Add($"height: {searchHeight.Value.OriginalText}");
        }

        var searchStr = string.Join(", ", searchCriteria);

        if (matches)
        {
            return $"✓ Searched for: {searchStr}. Detected: {detectedAttrs}, height: ~{physicalAttributes.EstimatedHeightMeters:F2}m. Match found";
        }
        else
        {
            return $"✗ Searched for: {searchStr}. Detected: {detectedAttrs}, height: ~{physicalAttributes.EstimatedHeightMeters:F2}m. No match";
        }
    }
}
