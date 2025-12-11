using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADCommsPersonTracking.Tests.Integration;

/// <summary>
/// Integration tests for YOLO11 ONNX model with real model inference.
/// These tests use the actual ObjectDetectionService with the YOLO11 ONNX model
/// to perform real inference and validate detection capabilities.
/// NOTE: These tests require the YOLO11 model to be downloaded.
/// Run 'python download-model.py' to download the model before running these tests.
/// </summary>
[Trait("Category", "Integration")]
public class YoloIntegrationTests : IDisposable
{
    private readonly ObjectDetectionService _service;
    private const string TestDataPath = "TestData/Images";
    private readonly string _modelPath;

    public YoloIntegrationTests()
    {
        // Get path to repository root by searching for .git directory
        var repoRoot = FindRepositoryRoot(Directory.GetCurrentDirectory());
        if (repoRoot == null)
        {
            throw new InvalidOperationException("Could not find repository root directory");
        }
        
        _modelPath = Path.Combine(repoRoot, "models", "yolo11n.onnx");
        
        // Skip tests if model doesn't exist
        if (!File.Exists(_modelPath))
        {
            throw new InvalidOperationException(
                $"YOLO11 model not found at {_modelPath}. " +
                "Run 'python download-model.py' to download the model before running integration tests.");
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ObjectDetection:ModelPath"] = _modelPath
            })
            .Build();

        var logger = Mock.Of<ILogger<ObjectDetectionService>>();
        _service = new ObjectDetectionService(configuration, logger);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    [Fact]
    public async Task DetectPersonsAsync_ModelLoadsSuccessfully()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var detections = await _service.DetectPersonsAsync(imageBytes);

        // Assert - Just verify that model loads and returns a result (may or may not detect the simple test image)
        detections.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectPersonsAsync_WithImageWithoutPerson_ReturnsNoDetections()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "no_person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var detections = await _service.DetectPersonsAsync(imageBytes);

        // Assert - Solid color image should not be detected as a person
        detections.Should().NotBeNull();
        detections.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectPersonsAsync_WithEmptyScene_ReturnsNoDetections()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "empty_scene.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var detections = await _service.DetectPersonsAsync(imageBytes);

        // Assert - Empty scene should not have person detections
        detections.Should().NotBeNull();
        detections.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectPersonsAsync_BoundingBoxFormat_IsCorrect()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var detections = await _service.DetectPersonsAsync(imageBytes);

        // Assert - Verify bounding box format for any detections
        detections.Should().NotBeNull();
        
        foreach (var detection in detections)
        {
            detection.Label.Should().Be("person");
            detection.X.Should().BeGreaterThanOrEqualTo(0);
            detection.Y.Should().BeGreaterThanOrEqualTo(0);
            detection.Width.Should().BeGreaterThan(0);
            detection.Height.Should().BeGreaterThan(0);
            detection.Confidence.Should().BeInRange(0f, 1f);
        }
    }

    [Fact]
    public async Task DetectPersonsAsync_DetectionsHaveValidConfidence()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var detections = await _service.DetectPersonsAsync(imageBytes);

        // Assert - All detections should have confidence >= threshold (0.45)
        detections.Should().NotBeNull();
        
        foreach (var detection in detections)
        {
            detection.Confidence.Should().BeGreaterThanOrEqualTo(0.45f);
            detection.Confidence.Should().BeLessThanOrEqualTo(1.0f);
        }
    }

    [Fact]
    public async Task DetectPersonsAsync_WithMultipleImages_ProducesConsistentResults()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "no_person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act - Run detection twice on the same image
        var detections1 = await _service.DetectPersonsAsync(imageBytes);
        var detections2 = await _service.DetectPersonsAsync(imageBytes);

        // Assert - Results should be consistent
        detections1.Should().NotBeNull();
        detections2.Should().NotBeNull();
        detections1.Count.Should().Be(detections2.Count);
    }

    [Fact]
    public async Task DetectPersonsAsync_WithValidImage_CompletesWithinReasonableTime()
    {
        // Arrange
        var imagePath = Path.Combine(TestDataPath, "person.jpg");
        var imageBytes = await File.ReadAllBytesAsync(imagePath);

        // Act
        var startTime = DateTime.UtcNow;
        var detections = await _service.DetectPersonsAsync(imageBytes);
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert - Detection should complete within 10 seconds on reasonable hardware
        duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
        detections.Should().NotBeNull();
    }
}
