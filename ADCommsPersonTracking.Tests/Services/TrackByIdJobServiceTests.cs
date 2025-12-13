using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADCommsPersonTracking.Tests.Services;

public class TrackByIdJobServiceTests
{
    private readonly TrackByIdJobService _service;

    public TrackByIdJobServiceTests()
    {
        var logger = Mock.Of<ILogger<TrackByIdJobService>>();
        _service = new TrackByIdJobService(logger);
    }

    [Fact]
    public void CreateJob_ShouldCreateJobWithCorrectInitialState()
    {
        // Arrange
        var totalFrames = 100;

        // Act
        var job = _service.CreateJob(totalFrames);

        // Assert
        job.Should().NotBeNull();
        job.JobId.Should().NotBeNullOrEmpty();
        job.Status.Should().Be("Pending");
        job.ProgressPercentage.Should().Be(0);
        job.CurrentStep.Should().Be("Initializing");
        job.TotalFrames.Should().Be(totalFrames);
        job.ProcessedFrames.Should().Be(0);
        job.TrackingResponse.Should().BeNull();
        job.ErrorMessage.Should().BeNull();
        job.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        job.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void GetJob_WithValidJobId_ShouldReturnJob()
    {
        // Arrange
        var job = _service.CreateJob(50);

        // Act
        var retrievedJob = _service.GetJob(job.JobId);

        // Assert
        retrievedJob.Should().NotBeNull();
        retrievedJob!.JobId.Should().Be(job.JobId);
    }

    [Fact]
    public void GetJob_WithInvalidJobId_ShouldReturnNull()
    {
        // Act
        var retrievedJob = _service.GetJob("non-existent-job-id");

        // Assert
        retrievedJob.Should().BeNull();
    }

    [Fact]
    public void UpdateProgress_ShouldUpdateJobProgress()
    {
        // Arrange
        var job = _service.CreateJob(100);
        var processedFrames = 25;
        var step = "Processing frames";

        // Act
        _service.UpdateProgress(job.JobId, processedFrames, step);
        var updatedJob = _service.GetJob(job.JobId);

        // Assert
        updatedJob.Should().NotBeNull();
        updatedJob!.ProcessedFrames.Should().Be(processedFrames);
        updatedJob.CurrentStep.Should().Be(step);
        updatedJob.Status.Should().Be("Processing");
        updatedJob.ProgressPercentage.Should().Be(25); // 25/100 = 25%
    }

    [Fact]
    public void UpdateProgress_WithHalfFrames_ShouldCalculateCorrectPercentage()
    {
        // Arrange
        var job = _service.CreateJob(200);
        var processedFrames = 100;

        // Act
        _service.UpdateProgress(job.JobId, processedFrames, "Halfway through");
        var updatedJob = _service.GetJob(job.JobId);

        // Assert
        updatedJob!.ProgressPercentage.Should().Be(50);
    }

    [Fact]
    public void CompleteJob_ShouldSetStatusToCompletedWithResults()
    {
        // Arrange
        var job = _service.CreateJob(50);
        var trackingResponse = new TrackingResponse
        {
            Timestamp = DateTime.UtcNow,
            Results = new List<ImageDetectionResult>
            {
                new ImageDetectionResult
                {
                    ImageIndex = 0,
                    Detections = new List<Detection>
                    {
                        new Detection
                        {
                            TrackingId = "person-1",
                            MatchScore = 0.95f
                        }
                    }
                }
            }
        };

        // Act
        _service.CompleteJob(job.JobId, trackingResponse);
        var completedJob = _service.GetJob(job.JobId);

        // Assert
        completedJob.Should().NotBeNull();
        completedJob!.Status.Should().Be("Completed");
        completedJob.ProgressPercentage.Should().Be(100);
        completedJob.CurrentStep.Should().Be("Completed");
        completedJob.TrackingResponse.Should().NotBeNull();
        completedJob.TrackingResponse.Should().BeSameAs(trackingResponse);
        completedJob.CompletedAt.Should().NotBeNull();
        completedJob.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void FailJob_ShouldSetStatusToFailedWithErrorMessage()
    {
        // Arrange
        var job = _service.CreateJob(50);
        var errorMessage = "Frame processing failed";

        // Act
        _service.FailJob(job.JobId, errorMessage);
        var failedJob = _service.GetJob(job.JobId);

        // Assert
        failedJob.Should().NotBeNull();
        failedJob!.Status.Should().Be("Failed");
        failedJob.ErrorMessage.Should().Be(errorMessage);
        failedJob.CompletedAt.Should().NotBeNull();
        failedJob.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void UpdateProgress_WithNonExistentJobId_ShouldNotThrowException()
    {
        // Act & Assert
        var act = () => _service.UpdateProgress("non-existent-id", 10, "test");
        act.Should().NotThrow();
    }

    [Fact]
    public void CompleteJob_WithNonExistentJobId_ShouldNotThrowException()
    {
        // Arrange
        var trackingResponse = new TrackingResponse();

        // Act & Assert
        var act = () => _service.CompleteJob("non-existent-id", trackingResponse);
        act.Should().NotThrow();
    }

    [Fact]
    public void FailJob_WithNonExistentJobId_ShouldNotThrowException()
    {
        // Act & Assert
        var act = () => _service.FailJob("non-existent-id", "error");
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateJob_MultipleJobs_ShouldCreateUniqueJobIds()
    {
        // Act
        var job1 = _service.CreateJob(10);
        var job2 = _service.CreateJob(20);
        var job3 = _service.CreateJob(30);

        // Assert
        job1.JobId.Should().NotBe(job2.JobId);
        job2.JobId.Should().NotBe(job3.JobId);
        job1.JobId.Should().NotBe(job3.JobId);
    }

    [Fact]
    public void UpdateProgress_WithZeroTotalFrames_ShouldHandleGracefully()
    {
        // Arrange
        var job = _service.CreateJob(0);

        // Act
        _service.UpdateProgress(job.JobId, 0, "Processing");
        var updatedJob = _service.GetJob(job.JobId);

        // Assert
        updatedJob.Should().NotBeNull();
        updatedJob!.Status.Should().Be("Processing");
        // Percentage should be 0 to avoid division by zero
        updatedJob.ProgressPercentage.Should().Be(0);
    }
}
