using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class PersonTrackingServiceTests
{
    private readonly Mock<IObjectDetectionService> _detectionServiceMock;
    private readonly Mock<IPromptFeatureExtractor> _featureExtractorMock;
    private readonly Mock<IImageAnnotationService> _annotationServiceMock;
    private readonly Mock<IColorAnalysisService> _colorAnalysisServiceMock;
    private readonly Mock<IAccessoryDetectionService> _accessoryDetectionServiceMock;
    private readonly Mock<IClothingDetectionService> _clothingDetectionServiceMock;
    private readonly Mock<IPhysicalAttributeEstimator> _physicalAttributeEstimatorMock;
    private readonly PersonTrackingService _service;

    public PersonTrackingServiceTests()
    {
        _detectionServiceMock = new Mock<IObjectDetectionService>();
        _featureExtractorMock = new Mock<IPromptFeatureExtractor>();
        _annotationServiceMock = new Mock<IImageAnnotationService>();
        _colorAnalysisServiceMock = new Mock<IColorAnalysisService>();
        _accessoryDetectionServiceMock = new Mock<IAccessoryDetectionService>();
        _clothingDetectionServiceMock = new Mock<IClothingDetectionService>();
        _physicalAttributeEstimatorMock = new Mock<IPhysicalAttributeEstimator>();
        
        // Create a real configuration for testing with parallelism disabled
        var configDict = new Dictionary<string, string?>
        {
            { "Processing:MaxDegreeOfParallelism", "1" }
        };
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();
        
        var logger = Mock.Of<ILogger<PersonTrackingService>>();
        var diagnosticsServiceMock = new Mock<IInferenceDiagnosticsService>();
        diagnosticsServiceMock.Setup(d => d.IsEnabled).Returns(false);

        _service = new PersonTrackingService(
            _detectionServiceMock.Object,
            _featureExtractorMock.Object,
            _annotationServiceMock.Object,
            _colorAnalysisServiceMock.Object,
            _accessoryDetectionServiceMock.Object,
            _clothingDetectionServiceMock.Object,
            _physicalAttributeEstimatorMock.Object,
            diagnosticsServiceMock.Object,
            configuration,
            logger);
    }

    private void SetupNewServiceMocks()
    {
        _accessoryDetectionServiceMock
            .Setup(s => s.DetectAccessoriesAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(new AccessoryDetectionResult());

        _accessoryDetectionServiceMock
            .Setup(s => s.DetectAccessoriesFromYolo(It.IsAny<BoundingBox>(), It.IsAny<List<DetectedObject>>()))
            .Returns(new AccessoryDetectionResult
            {
                ClothingItems = new List<DetectedItem> { new DetectedItem("jacket", 0.8f) }
            });

        _accessoryDetectionServiceMock
            .Setup(s => s.MatchesCriteria(It.IsAny<AccessoryDetectionResult>(), It.IsAny<List<string>>(), It.IsAny<List<string>>()))
            .Returns(true);

        _clothingDetectionServiceMock
            .Setup(s => s.DetectClothingAsync(It.IsAny<byte[]>(), It.IsAny<float?>()))
            .ReturnsAsync(new List<DetectedItem>());

        _physicalAttributeEstimatorMock
            .Setup(s => s.EstimateAttributesAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new PhysicalAttributes());

        _physicalAttributeEstimatorMock
            .Setup(s => s.MatchesCriteria(It.IsAny<PhysicalAttributes>(), It.IsAny<List<string>>(), It.IsAny<HeightInfo?>()))
            .Returns(true);
    }

    private void SetupDetectionServiceMock(List<BoundingBox> detections)
    {
        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _detectionServiceMock
            .Setup(s => s.DetectObjectsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections.Select(d => new DetectedObject
            {
                ClassId = 0,
                ObjectType = "person",
                BoundingBox = d
            }).ToList());
    }

    [Fact]
    public async Task ProcessFrameAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Timestamp.Should().Be(request.Timestamp);
        result.Results.Should().NotBeEmpty();
        result.Results[0].AnnotatedImageBase64.Should().Be(annotatedImage);
        result.Results[0].Detections.Should().NotBeEmpty();
        result.ProcessingMessage.Should().Contain("person detections");
    }

    [Fact]
    public async Task ProcessFrameAsync_WithMultipleDetections_ReturnsAllDetections()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = new List<BoundingBox>
        {
            new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" },
            new BoundingBox { X = 300, Y = 200, Width = 110, Height = 260, Confidence = 0.92f, Label = "person" },
            new BoundingBox { X = 450, Y = 180, Width = 115, Height = 270, Confidence = 0.78f, Label = "person" }
        };
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Detections.Should().HaveCount(3);
        result.Results[0].Detections.Should().AllSatisfy(d =>
        {
            d.TrackingId.Should().NotBeNullOrEmpty();
            d.BoundingBox.Should().NotBeNull();
            d.MatchScore.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public async Task ProcessFrameAsync_AssignsTrackingIds()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results[0].Detections.Should().AllSatisfy(d =>
        {
            d.TrackingId.Should().StartWith("track_");
        });
    }

    [Fact]
    public async Task ProcessFrameAsync_ReusesTrackingIdForSamePersonInConsecutiveFrames()
    {
        // Arrange
        var request1 = CreateTestRequest();
        var detection = new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" };
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(new List<BoundingBox> { detection });

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act - First frame
        var result1 = await _service.ProcessFrameAsync(request1);
        var trackingId1 = result1.Results[0].Detections.First().TrackingId;

        // Act - Second frame with same position (simulating same person)
        var request2 = CreateTestRequest();
        request2.Timestamp = request1.Timestamp.AddSeconds(1);
        var result2 = await _service.ProcessFrameAsync(request2);
        var trackingId2 = result2.Results[0].Detections.First().TrackingId;

        // Assert - Same tracking ID should be reused
        trackingId2.Should().Be(trackingId1);
    }

    [Fact]
    public async Task GetActiveTracksAsync_ReturnsActiveTracks()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        await _service.ProcessFrameAsync(request);
        var tracks = await _service.GetActiveTracksAsync();

        // Assert
        tracks.Should().NotBeEmpty();
        tracks.Should().AllSatisfy(t =>
        {
            t.TrackingId.Should().NotBeNullOrEmpty();
            t.LastKnownPosition.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task GetTrackByIdAsync_WithValidId_ReturnsTrack()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        var result = await _service.ProcessFrameAsync(request);
        var trackingId = result.Results[0].Detections.First().TrackingId;

        // Act
        var track = await _service.GetTrackByIdAsync(trackingId);

        // Assert
        track.Should().NotBeNull();
        track!.TrackingId.Should().Be(trackingId);
    }

    [Fact]
    public async Task GetTrackByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Act
        var track = await _service.GetTrackByIdAsync("nonexistent_id");

        // Assert
        track.Should().BeNull();
    }

    [Fact]
    public async Task ProcessFrameAsync_WithNoDetections_ReturnsEmptyDetectionList()
    {
        // Arrange
        var request = CreateTestRequest();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";

        SetupDetectionServiceMock(new List<BoundingBox>());

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results.Should().HaveCount(1);
        result.Results[0].Detections.Should().BeEmpty();
        result.ProcessingMessage.Should().Contain("0 person detections");
    }

    private TrackingRequest CreateTestRequest()
    {
        var imageBytes = CreateTestImage(640, 480);
        return new TrackingRequest
        {
            ImagesBase64 = new List<string> { Convert.ToBase64String(imageBytes) },
            Prompt = "Find a person wearing a green jacket",
            Timestamp = DateTime.UtcNow
        };
    }

    private PersonColorProfile CreateTestColorProfile()
    {
        return new PersonColorProfile
        {
            UpperBodyColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "green", Confidence = 0.7f, HexValue = "#00FF00" }
            },
            LowerBodyColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "blue", Confidence = 0.6f, HexValue = "#0000FF" }
            },
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "green", Confidence = 0.5f, HexValue = "#00FF00" },
                new DetectedColor { ColorName = "blue", Confidence = 0.3f, HexValue = "#0000FF" }
            }
        };
    }

    private List<BoundingBox> CreateTestDetections()
    {
        return new List<BoundingBox>
        {
            new BoundingBox
            {
                X = 100,
                Y = 150,
                Width = 120,
                Height = 280,
                Confidence = 0.85f,
                Label = "person"
            }
        };
    }

    private SearchCriteria CreateTestSearchCriteria()
    {
        return new SearchCriteria
        {
            Colors = new List<string> { "green" },
            ClothingItems = new List<string> { "jacket" }
        };
    }

    private byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgb24((byte)(x % 256), (byte)(y % 256), 128);
            }
        }

        using var memoryStream = new MemoryStream();
        image.SaveAsJpeg(memoryStream);
        return memoryStream.ToArray();
    }

    [Fact]
    public async Task ProcessFrameAsync_WithColorCriteria_FiltersMatchingDetections()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = new List<BoundingBox>
        {
            new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" },
            new BoundingBox { X = 300, Y = 200, Width = 110, Height = 260, Confidence = 0.92f, Label = "person" }
        };
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var matchingColorProfile = CreateTestColorProfile(); // Has green
        var nonMatchingColorProfile = new PersonColorProfile
        {
            OverallColors = new List<DetectedColor>
            {
                new DetectedColor { ColorName = "red", Confidence = 0.5f, HexValue = "#FF0000" }
            }
        };

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // First detection matches, second doesn't
        _colorAnalysisServiceMock
            .SetupSequence(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(matchingColorProfile)
            .ReturnsAsync(nonMatchingColorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(matchingColorProfile, It.IsAny<List<string>>()))
            .Returns(true);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(nonMatchingColorProfile, It.IsAny<List<string>>()))
            .Returns(false);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results[0].Detections.Should().HaveCount(1); // Only the matching detection
        result.ProcessingMessage.Should().Contain("Filtered to 1 persons matching criteria");
    }

    [Fact]
    public async Task ProcessFrameAsync_WithoutColorCriteria_ReturnsAllDetections()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = new List<BoundingBox>
        {
            new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" },
            new BoundingBox { X = 300, Y = 200, Width = 110, Height = 260, Confidence = 0.92f, Label = "person" }
        };
        var searchCriteria = new SearchCriteria
        {
            Colors = new List<string>(), // No color criteria
            ClothingItems = new List<string> { "jacket" }
        };
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results[0].Detections.Should().HaveCount(2); // All detections returned
        result.ProcessingMessage.Should().Contain("Filtered to 2 persons matching criteria");
    }

    [Fact]
    public async Task ProcessFrameAsync_WithMultipleImages_ProcessesAllImages()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var request = new TrackingRequest
        {
            ImagesBase64 = new List<string> 
            { 
                Convert.ToBase64String(imageBytes),
                Convert.ToBase64String(imageBytes)
            },
            Prompt = "Find a person wearing a green jacket",
            Timestamp = DateTime.UtcNow
        };
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";
        var colorProfile = CreateTestColorProfile();

        SetupDetectionServiceMock(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        _colorAnalysisServiceMock
            .Setup(s => s.AnalyzePersonColorsAsync(It.IsAny<byte[]>(), It.IsAny<BoundingBox>()))
            .ReturnsAsync(colorProfile);

        _colorAnalysisServiceMock
            .Setup(s => s.MatchesColorCriteria(It.IsAny<PersonColorProfile>(), It.IsAny<List<string>>()))
            .Returns(true);

        SetupNewServiceMocks();

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Results.Should().HaveCount(2);
        result.Results[0].ImageIndex.Should().Be(0);
        result.Results[1].ImageIndex.Should().Be(1);
        result.ProcessingMessage.Should().Contain("Processed 2 images");
    }
}
