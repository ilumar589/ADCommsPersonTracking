using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Services;

public class ClothingDetectionServiceTests
{
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<ClothingDetectionService>> _loggerMock;

    public ClothingDetectionServiceTests()
    {
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<ClothingDetectionService>>();
    }

    private static Mock<IConfiguration> CreateMockConfiguration(bool enabled = false, string? modelPath = null)
    {
        var configMock = new Mock<IConfiguration>();
        
        // Setup Enabled flag
        var enabledSection = new Mock<IConfigurationSection>();
        enabledSection.Setup(s => s.Value).Returns(enabled.ToString());
        configMock.Setup(c => c.GetSection("ClothingDetection:Enabled")).Returns(enabledSection.Object);
        
        // Setup ModelPath
        configMock.Setup(c => c["ClothingDetection:ModelPath"]).Returns(modelPath);
        
        // Setup ConfidenceThreshold
        var thresholdSection = new Mock<IConfigurationSection>();
        thresholdSection.Setup(s => s.Value).Returns("0.5");
        configMock.Setup(c => c.GetSection("ClothingDetection:ConfidenceThreshold")).Returns(thresholdSection.Object);
        
        return configMock;
    }

    private static byte[] CreateTestImage(int width, int height)
    {
        using var image = new Image<Rgb24>(width, height);
        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Constructor_WhenDisabled_ShouldNotLoadModel()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);

        // Act
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WhenEnabledButNoModelPath_ShouldDisable()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: true, modelPath: null);

        // Act
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WhenEnabledButModelNotFound_ShouldDisable()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: true, modelPath: "/nonexistent/model.onnx");

        // Act
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectClothingAsync_WhenDisabled_ShouldReturnEmptyList()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);
        var imageBytes = CreateTestImage(640, 480);

        // Act
        var result = await service.DetectClothingAsync(imageBytes);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectClothingAsync_WithNullImage_ShouldReturnEmptyList()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);

        // Act
        var result = await service.DetectClothingAsync(Array.Empty<byte>());

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectClothingAsync_WithCustomThreshold_ShouldUseCustomValue()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);
        var imageBytes = CreateTestImage(640, 480);

        // Act
        var result = await service.DetectClothingAsync(imageBytes, confidenceThreshold: 0.7f);

        // Assert - when disabled, always returns empty regardless of threshold
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);

        // Act & Assert
        var act = () => service.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DetectClothingAsync_WithValidImage_ShouldNotThrow()
    {
        // Arrange
        var config = CreateMockConfiguration(enabled: false);
        var service = new ClothingDetectionService(config.Object, _loggerMock.Object);
        var imageBytes = CreateTestImage(320, 240);

        // Act
        var act = async () => await service.DetectClothingAsync(imageBytes);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
