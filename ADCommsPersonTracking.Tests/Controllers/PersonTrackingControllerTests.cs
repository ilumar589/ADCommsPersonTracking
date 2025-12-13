using ADCommsPersonTracking.Api.Controllers;
using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ADCommsPersonTracking.Tests.Controllers;

public class PersonTrackingControllerTests
{
    private readonly Mock<IPersonTrackingService> _trackingServiceMock;
    private readonly Mock<IVideoProcessingService> _videoProcessingServiceMock;
    private readonly Mock<IFrameStorageService> _frameStorageServiceMock;
    private readonly Mock<IVideoCacheService> _videoCacheServiceMock;
    private readonly Mock<IVideoUploadJobService> _videoUploadJobServiceMock;
    private readonly Mock<ITrackByIdJobService> _trackByIdJobServiceMock;
    private readonly PersonTrackingController _controller;

    public PersonTrackingControllerTests()
    {
        _trackingServiceMock = new Mock<IPersonTrackingService>();
        _videoProcessingServiceMock = new Mock<IVideoProcessingService>();
        _frameStorageServiceMock = new Mock<IFrameStorageService>();
        _videoCacheServiceMock = new Mock<IVideoCacheService>();
        _videoUploadJobServiceMock = new Mock<IVideoUploadJobService>();
        _trackByIdJobServiceMock = new Mock<ITrackByIdJobService>();
        
        // Setup default behavior for video upload job service
        _videoUploadJobServiceMock
            .Setup(s => s.CreateJob())
            .Returns(new VideoUploadJob
            {
                JobId = "test-job-id",
                Status = "Pending",
                ProgressPercentage = 0,
                CurrentStep = "Initializing",
                CreatedAt = DateTime.UtcNow
            });
        
        // Setup default behavior for track-by-id job service
        _trackByIdJobServiceMock
            .Setup(s => s.CreateJob(It.IsAny<int>()))
            .Returns((int totalFrames) => new TrackByIdJob
            {
                JobId = "test-track-job-id",
                Status = "Pending",
                ProgressPercentage = 0,
                CurrentStep = "Initializing",
                CreatedAt = DateTime.UtcNow,
                TotalFrames = totalFrames,
                ProcessedFrames = 0
            });
        
        var logger = Mock.Of<ILogger<PersonTrackingController>>();
        _controller = new PersonTrackingController(
            _trackingServiceMock.Object, 
            logger,
            _videoProcessingServiceMock.Object,
            _frameStorageServiceMock.Object,
            _videoCacheServiceMock.Object,
            _videoUploadJobServiceMock.Object,
            _trackByIdJobServiceMock.Object);
    }

    [Fact]
    public async Task TrackPersons_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = CreateTestRequest();
        var response = CreateTestResponse();

        _trackingServiceMock
            .Setup(s => s.ProcessFrameAsync(It.IsAny<TrackingRequest>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.TrackPersons(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(response);
    }

    [Fact]
    public async Task TrackPersons_WithMissingImage_ReturnsBadRequest()
    {
        // Arrange
        var request = new TrackingRequest
        {
            ImagesBase64 = new List<string>(),
            Prompt = "Find a person",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackPersons(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("At least one image is required");
    }

    [Fact]
    public async Task TrackPersons_WithMissingPrompt_ReturnsBadRequest()
    {
        // Arrange
        var imageBytes = CreateTestImage(640, 480);
        var request = new TrackingRequest
        {
            ImagesBase64 = new List<string> { Convert.ToBase64String(imageBytes) },
            Prompt = "",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackPersons(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Search prompt is required");
    }

    [Fact]
    public async Task TrackPersons_WithException_ReturnsInternalServerError()
    {
        // Arrange
        var request = CreateTestRequest();

        _trackingServiceMock
            .Setup(s => s.ProcessFrameAsync(It.IsAny<TrackingRequest>()))
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.TrackPersons(request);

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Internal server error");
    }

    [Fact]
    public async Task GetActiveTracks_ReturnsOkWithTracks()
    {
        // Arrange
        var tracks = new List<PersonTrack>
        {
            new PersonTrack
            {
                TrackingId = "track_001",
                FirstSeen = DateTime.UtcNow.AddMinutes(-5),
                LastSeen = DateTime.UtcNow,
                LastKnownPosition = new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" }
            }
        };

        _trackingServiceMock
            .Setup(s => s.GetActiveTracksAsync())
            .ReturnsAsync(tracks);

        // Act
        var result = await _controller.GetActiveTracks();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(tracks);
    }

    [Fact]
    public async Task GetActiveTracks_WithNoTracks_ReturnsEmptyList()
    {
        // Arrange
        _trackingServiceMock
            .Setup(s => s.GetActiveTracksAsync())
            .ReturnsAsync(new List<PersonTrack>());

        // Act
        var result = await _controller.GetActiveTracks();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var tracks = okResult!.Value as List<PersonTrack>;
        tracks.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrack_WithValidId_ReturnsOkWithTrack()
    {
        // Arrange
        var trackingId = "track_001";
        var track = new PersonTrack
        {
            TrackingId = trackingId,
            FirstSeen = DateTime.UtcNow.AddMinutes(-5),
            LastSeen = DateTime.UtcNow,
            LastKnownPosition = new BoundingBox { X = 100, Y = 150, Width = 120, Height = 280, Confidence = 0.85f, Label = "person" }
        };

        _trackingServiceMock
            .Setup(s => s.GetTrackByIdAsync(trackingId))
            .ReturnsAsync(track);

        // Act
        var result = await _controller.GetTrack(trackingId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(track);
    }

    [Fact]
    public async Task GetTrack_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        var trackingId = "nonexistent_id";

        _trackingServiceMock
            .Setup(s => s.GetTrackByIdAsync(trackingId))
            .ReturnsAsync((PersonTrack?)null);

        // Act
        var result = await _controller.GetTrack(trackingId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result.Result as NotFoundObjectResult;
        notFound!.Value.Should().Be($"Track with ID '{trackingId}' not found");
    }

    [Fact]
    public async Task GetTrackingIds_ReturnsOkWithTrackingIds()
    {
        // Arrange
        var trackingIds = new List<string> { "video_123", "video_456", "video_789" };

        _frameStorageServiceMock
            .Setup(s => s.GetAllTrackingIdsAsync())
            .ReturnsAsync(trackingIds);

        // Act
        var result = await _controller.GetTrackingIds();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(trackingIds);
    }

    [Fact]
    public async Task GetTrackingIds_WithNoTrackingIds_ReturnsEmptyList()
    {
        // Arrange
        _frameStorageServiceMock
            .Setup(s => s.GetAllTrackingIdsAsync())
            .ReturnsAsync(new List<string>());

        // Act
        var result = await _controller.GetTrackingIds();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var trackingIds = okResult!.Value as List<string>;
        trackingIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTrackingIds_WithException_ReturnsInternalServerError()
    {
        // Arrange
        _frameStorageServiceMock
            .Setup(s => s.GetAllTrackingIdsAsync())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetTrackingIds();

        // Assert
        result.Result.Should().BeOfType<ObjectResult>();
        var objectResult = result.Result as ObjectResult;
        objectResult!.StatusCode.Should().Be(500);
        objectResult.Value.Should().Be("Internal server error");
    }

    [Fact]
    public void HealthCheck_ReturnsOk()
    {
        // Act
        var result = _controller.HealthCheck();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task UploadVideo_WithValidVideo_ReturnsJobIdImmediately()
    {
        // Arrange
        var videoName = "test_video.mp4";

        var video = CreateMockFormFile(videoName, "video/mp4", new byte[] { 1, 2, 3 });

        // Act
        var result = await _controller.UploadVideo(video);

        // Assert - Should return job response immediately
        result.Result.Should().BeOfType<OkObjectResult>();

        var response = (result.Result as OkObjectResult)!.Value as VideoUploadJobResponse;

        response.Should().NotBeNull();
        response!.JobId.Should().Be("test-job-id");
        response.Message.Should().Contain("job ID");
        
        // Verify that CreateJob was called
        _videoUploadJobServiceMock.Verify(s => s.CreateJob(), Times.Once);
    }

    [Fact]
    public async Task UploadVideo_UpdatesJobProgress()
    {
        // Arrange
        var videoName = "test_video.mp4";
        var video = CreateMockFormFile(videoName, "video/mp4", new byte[] { 1, 2, 3 });

        // Act
        var result = await _controller.UploadVideo(video);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        
        // Verify that progress updates were called
        _videoUploadJobServiceMock.Verify(
            s => s.UpdateProgress(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UploadVideo_StartsBackgroundProcessing()
    {
        // Arrange
        var videoName = "test_video.mp4";
        var video = CreateMockFormFile(videoName, "video/mp4", new byte[] { 1, 2, 3 });

        // Act
        var result = await _controller.UploadVideo(video);

        // Assert - Should return job response immediately without waiting for processing
        result.Result.Should().BeOfType<OkObjectResult>();
        var response = (result.Result as OkObjectResult)!.Value as VideoUploadJobResponse;
        
        response.Should().NotBeNull();
        response!.JobId.Should().NotBeNullOrEmpty();
        response.Message.Should().Contain("job ID");
        
        // Verify that CreateJob was called to start the background processing
        _videoUploadJobServiceMock.Verify(s => s.CreateJob(), Times.Once);
    }

    [Fact]
    public async Task TrackById_WithValidRequest_ReturnsJobIdImmediately()
    {
        // Arrange
        var trackingId = "video_123";
        var frames = new List<byte[]> 
        { 
            CreateTestImage(640, 480),
            CreateTestImage(640, 480),
            CreateTestImage(640, 480)
        };
        
        _frameStorageServiceMock
            .Setup(s => s.FramesExistAsync(trackingId))
            .ReturnsAsync(true);
        
        _frameStorageServiceMock
            .Setup(s => s.GetFramesAsync(trackingId))
            .ReturnsAsync(frames);

        var request = new TrackByIdRequest
        {
            TrackingId = trackingId,
            Prompt = "Find a person in a yellow jacket",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackById(request);

        // Assert - Should return job response immediately
        result.Result.Should().BeOfType<OkObjectResult>();
        var response = (result.Result as OkObjectResult)!.Value as TrackByIdJobResponse;
        
        response.Should().NotBeNull();
        response!.JobId.Should().Be("test-track-job-id");
        response.Message.Should().Contain("job ID");
        response.TotalFrames.Should().Be(3);
        
        // Verify that CreateJob was called
        _trackByIdJobServiceMock.Verify(s => s.CreateJob(3), Times.Once);
    }

    [Fact]
    public async Task TrackById_WithMissingTrackingId_ReturnsBadRequest()
    {
        // Arrange
        var request = new TrackByIdRequest
        {
            TrackingId = "",
            Prompt = "Find a person",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackById(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Tracking ID is required");
    }

    [Fact]
    public async Task TrackById_WithMissingPrompt_ReturnsBadRequest()
    {
        // Arrange
        var request = new TrackByIdRequest
        {
            TrackingId = "video_123",
            Prompt = "",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackById(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = result.Result as BadRequestObjectResult;
        badRequest!.Value.Should().Be("Search prompt is required");
    }

    [Fact]
    public async Task TrackById_WithNonExistentTrackingId_ReturnsNotFound()
    {
        // Arrange
        var trackingId = "non_existent_video";
        
        _frameStorageServiceMock
            .Setup(s => s.FramesExistAsync(trackingId))
            .ReturnsAsync(false);

        var request = new TrackByIdRequest
        {
            TrackingId = trackingId,
            Prompt = "Find a person",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackById(request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result.Result as NotFoundObjectResult;
        notFound!.Value.Should().Be($"No frames found for tracking ID: {trackingId}");
    }

    [Fact]
    public async Task TrackById_UpdatesJobProgress()
    {
        // Arrange
        var trackingId = "video_123";
        var frames = new List<byte[]> { CreateTestImage(640, 480) };
        
        _frameStorageServiceMock
            .Setup(s => s.FramesExistAsync(trackingId))
            .ReturnsAsync(true);
        
        _frameStorageServiceMock
            .Setup(s => s.GetFramesAsync(trackingId))
            .ReturnsAsync(frames);

        var request = new TrackByIdRequest
        {
            TrackingId = trackingId,
            Prompt = "Find a person",
            Timestamp = DateTime.UtcNow
        };

        // Act
        var result = await _controller.TrackById(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        
        // Verify that progress updates were called
        _trackByIdJobServiceMock.Verify(
            s => s.UpdateProgress(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>()), 
            Times.AtLeastOnce);
    }

    [Fact]
    public void GetTrackByIdStatus_WithValidJobId_ReturnsJob()
    {
        // Arrange
        var jobId = "test-job-id";
        var job = new TrackByIdJob
        {
            JobId = jobId,
            Status = "Processing",
            ProgressPercentage = 50,
            CurrentStep = "Processing frames",
            TotalFrames = 100,
            ProcessedFrames = 50,
            CreatedAt = DateTime.UtcNow
        };

        _trackByIdJobServiceMock
            .Setup(s => s.GetJob(jobId))
            .Returns(job);

        // Act
        var result = _controller.GetTrackByIdStatus(jobId);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        okResult!.Value.Should().BeEquivalentTo(job);
    }

    [Fact]
    public void GetTrackByIdStatus_WithInvalidJobId_ReturnsNotFound()
    {
        // Arrange
        var jobId = "non-existent-job-id";

        _trackByIdJobServiceMock
            .Setup(s => s.GetJob(jobId))
            .Returns((TrackByIdJob?)null);

        // Act
        var result = _controller.GetTrackByIdStatus(jobId);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
        var notFound = result.Result as NotFoundObjectResult;
        notFound!.Value.Should().Be($"Job with ID '{jobId}' not found");
    }

    private IFormFile CreateMockFormFile(string fileName, string contentType, byte[] content)
    {
        var formFile = new Mock<IFormFile>();
        formFile.Setup(f => f.FileName).Returns(fileName);
        formFile.Setup(f => f.ContentType).Returns(contentType);
        formFile.Setup(f => f.Length).Returns(content.Length);
        formFile.Setup(f => f.OpenReadStream()).Returns(() => new MemoryStream(content));
        return formFile.Object;
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

    private TrackingResponse CreateTestResponse()
    {
        return new TrackingResponse
        {
            Timestamp = DateTime.UtcNow,
            Results = new List<ImageDetectionResult>
            {
                new ImageDetectionResult
                {
                    ImageIndex = 0,
                    AnnotatedImageBase64 = "base64AnnotatedImage",
                    Detections = new List<Detection>
                    {
                        new Detection
                        {
                            TrackingId = "track_001",
                            BoundingBox = new BoundingBox
                            {
                                X = 100,
                                Y = 150,
                                Width = 120,
                                Height = 280,
                                Confidence = 0.85f,
                                Label = "person"
                            },
                            Description = "green, jacket",
                            MatchScore = 0.85f,
                            MatchedCriteria = new List<string> { "green" }
                        }
                    }
                }
            },
            ProcessingMessage = "Processed 1 images with 1 person detections"
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
