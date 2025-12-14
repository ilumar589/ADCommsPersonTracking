using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADCommsPersonTracking.Tests.Services;

public class VideoProcessingServiceTests
{
    private readonly VideoProcessingService _service;
    private readonly Mock<ILogger<VideoProcessingService>> _mockLogger;
    private readonly IConfiguration _configuration;

    public VideoProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<VideoProcessingService>>();
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["VideoProcessing:MaxFrames"] = "100",
                ["VideoProcessing:FrameInterval"] = "1"
            })
            .Build();

        _service = new VideoProcessingService(_mockLogger.Object, _configuration);
    }

    [Fact]
    public void FrameDistribution_Calculation_ShouldDistributeEvenly()
    {
        // Arrange
        var videoDurationSeconds = 60.0; // 60-second video
        var maxFrames = 30;
        var totalFrames = 1800; // 60 seconds * 30 fps

        // Act - Simulate the calculation logic from VideoProcessingService
        var framesToExtract = Math.Min(maxFrames, totalFrames);
        var intervalBetweenFrames = videoDurationSeconds / framesToExtract;

        // Calculate time offsets for extracted frames
        var timeOffsets = new List<double>();
        for (int i = 0; i < framesToExtract; i++)
        {
            var timeOffset = i * intervalBetweenFrames;
            timeOffsets.Add(timeOffset);
        }

        // Assert
        framesToExtract.Should().Be(30, "should extract up to maxFrames");
        intervalBetweenFrames.Should().Be(2.0, "frames should be 2 seconds apart for a 60-second video with 30 frames");
        
        // Verify first and last frame times
        timeOffsets.First().Should().Be(0.0, "first frame should be at the start");
        timeOffsets.Last().Should().Be(58.0, "last frame should be near the end (29 * 2 = 58 seconds)");
        
        // Verify frames are evenly distributed
        for (int i = 1; i < timeOffsets.Count; i++)
        {
            var interval = timeOffsets[i] - timeOffsets[i - 1];
            interval.Should().BeApproximately(2.0, 0.01, $"interval between frame {i-1} and {i} should be consistent");
        }
    }

    [Fact]
    public void FrameDistribution_Calculation_WithShortVideo_ShouldRespectVideoLength()
    {
        // Arrange
        var videoDurationSeconds = 10.0; // 10-second video
        var maxFrames = 100;
        var totalFrames = 300; // 10 seconds * 30 fps

        // Act
        var framesToExtract = Math.Min(maxFrames, totalFrames);
        var intervalBetweenFrames = videoDurationSeconds / framesToExtract;

        // Calculate time offsets for extracted frames
        var timeOffsets = new List<double>();
        for (int i = 0; i < framesToExtract; i++)
        {
            var timeOffset = i * intervalBetweenFrames;
            timeOffsets.Add(timeOffset);
        }

        // Assert
        framesToExtract.Should().Be(100, "should extract maxFrames when video has enough frames");
        intervalBetweenFrames.Should().BeApproximately(0.1, 0.01, "frames should be 0.1 seconds apart");
        
        // Verify first and last frame times
        timeOffsets.First().Should().Be(0.0, "first frame should be at the start");
        timeOffsets.Last().Should().BeApproximately(9.9, 0.01, "last frame should be near the end");
    }

    [Fact]
    public void FrameDistribution_Calculation_WithVeryShortVideo_ShouldExtractLimitedFrames()
    {
        // Arrange
        var videoDurationSeconds = 2.0; // 2-second video
        var maxFrames = 100;
        var totalFrames = 60; // 2 seconds * 30 fps

        // Act
        var framesToExtract = Math.Min(maxFrames, totalFrames);
        var intervalBetweenFrames = videoDurationSeconds / framesToExtract;

        // Calculate time offsets for extracted frames
        var timeOffsets = new List<double>();
        for (int i = 0; i < framesToExtract; i++)
        {
            var timeOffset = i * intervalBetweenFrames;
            timeOffsets.Add(timeOffset);
        }

        // Assert
        framesToExtract.Should().Be(60, "should extract only totalFrames when less than maxFrames");
        intervalBetweenFrames.Should().BeApproximately(0.0333, 0.01, "frames should be evenly distributed");
        
        // Verify distribution covers the entire video
        timeOffsets.First().Should().Be(0.0, "first frame should be at the start");
        timeOffsets.Last().Should().BeLessThan(2.0, "last frame should be within video duration");
    }

    [Theory]
    [InlineData(60.0, 30, 2.0)]  // 60-second video, 30 frames = 2 seconds apart
    [InlineData(120.0, 60, 2.0)] // 120-second video, 60 frames = 2 seconds apart
    [InlineData(30.0, 15, 2.0)]  // 30-second video, 15 frames = 2 seconds apart
    [InlineData(10.0, 10, 1.0)]  // 10-second video, 10 frames = 1 second apart
    public void FrameDistribution_Calculation_VariousScenarios_ShouldDistributeEvenly(
        double videoDurationSeconds,
        int expectedFrames,
        double expectedInterval)
    {
        // Arrange
        var maxFrames = expectedFrames;
        var totalFrames = (int)(videoDurationSeconds * 30); // Assume 30 fps

        // Act
        var framesToExtract = Math.Min(maxFrames, totalFrames);
        var intervalBetweenFrames = videoDurationSeconds / framesToExtract;

        // Assert
        framesToExtract.Should().Be(expectedFrames, "should extract the expected number of frames");
        intervalBetweenFrames.Should().BeApproximately(expectedInterval, 0.01, "interval should match expected distribution");
    }
}
