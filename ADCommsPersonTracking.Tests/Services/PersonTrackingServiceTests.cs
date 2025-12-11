using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
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
    private readonly PersonTrackingService _service;

    public PersonTrackingServiceTests()
    {
        _detectionServiceMock = new Mock<IObjectDetectionService>();
        _featureExtractorMock = new Mock<IPromptFeatureExtractor>();
        _annotationServiceMock = new Mock<IImageAnnotationService>();
        var logger = Mock.Of<ILogger<PersonTrackingService>>();

        _service = new PersonTrackingService(
            _detectionServiceMock.Object,
            _featureExtractorMock.Object,
            _annotationServiceMock.Object,
            logger);
    }

    [Fact]
    public async Task ProcessFrameAsync_WithValidRequest_ReturnsResponse()
    {
        // Arrange
        var request = CreateTestRequest();
        var detections = CreateTestDetections();
        var searchCriteria = CreateTestSearchCriteria();
        var annotatedImage = "base64AnnotatedImage";

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.CameraId.Should().Be(request.CameraId);
        result.Timestamp.Should().Be(request.Timestamp);
        result.AnnotatedImageBase64.Should().Be(annotatedImage);
        result.Detections.Should().NotBeEmpty();
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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Detections.Should().HaveCount(3);
        result.Detections.Should().AllSatisfy(d =>
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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Detections.Should().AllSatisfy(d =>
        {
            d.TrackingId.Should().StartWith("track_");
            d.TrackingId.Should().Contain(request.CameraId);
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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new List<BoundingBox> { detection });

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act - First frame
        var result1 = await _service.ProcessFrameAsync(request1);
        var trackingId1 = result1.Detections.First().TrackingId;

        // Act - Second frame with same position (simulating same person)
        var request2 = CreateTestRequest();
        request2.Timestamp = request1.Timestamp.AddSeconds(1);
        var result2 = await _service.ProcessFrameAsync(request2);
        var trackingId2 = result2.Detections.First().TrackingId;

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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        await _service.ProcessFrameAsync(request);
        var tracks = await _service.GetActiveTracksAsync();

        // Assert
        tracks.Should().NotBeEmpty();
        tracks.Should().AllSatisfy(t =>
        {
            t.TrackingId.Should().NotBeNullOrEmpty();
            t.CameraId.Should().Be(request.CameraId);
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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(detections);

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        var result = await _service.ProcessFrameAsync(request);
        var trackingId = result.Detections.First().TrackingId;

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

        _detectionServiceMock
            .Setup(s => s.DetectPersonsAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new List<BoundingBox>());

        _featureExtractorMock
            .Setup(s => s.ExtractFeatures(It.IsAny<string>()))
            .Returns(searchCriteria);

        _annotationServiceMock
            .Setup(s => s.AnnotateImageAsync(It.IsAny<byte[]>(), It.IsAny<List<BoundingBox>>()))
            .ReturnsAsync(annotatedImage);

        // Act
        var result = await _service.ProcessFrameAsync(request);

        // Assert
        result.Detections.Should().BeEmpty();
        result.ProcessingMessage.Should().Contain("0 person detections");
    }

    private TrackingRequest CreateTestRequest()
    {
        var imageBytes = CreateTestImage(640, 480);
        return new TrackingRequest
        {
            CameraId = "test-camera-01",
            ImageBase64 = Convert.ToBase64String(imageBytes),
            Prompt = "Find a person wearing a green jacket",
            Timestamp = DateTime.UtcNow
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
}
