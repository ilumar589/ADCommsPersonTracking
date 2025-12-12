using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADCommsPersonTracking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonTrackingController : ControllerBase
{
    private readonly IPersonTrackingService _trackingService;
    private readonly ILogger<PersonTrackingController> _logger;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly IFrameStorageService _frameStorageService;
    private readonly IVideoCacheService _videoCacheService;

    public PersonTrackingController(
        IPersonTrackingService trackingService,
        ILogger<PersonTrackingController> logger,
        IVideoProcessingService videoProcessingService,
        IFrameStorageService frameStorageService,
        IVideoCacheService videoCacheService)
    {
        _trackingService = trackingService;
        _logger = logger;
        _videoProcessingService = videoProcessingService;
        _frameStorageService = frameStorageService;
        _videoCacheService = videoCacheService;
    }

    /// <summary>
    /// Process a video frame and track persons based on the provided prompt
    /// </summary>
    /// <param name="request">Tracking request containing frame data and search prompt</param>
    /// <returns>Tracking response with bounding boxes for matched persons</returns>
    [HttpPost("track")]
    [ProducesResponseType(typeof(TrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TrackingResponse>> TrackPersons([FromBody] TrackingRequest request)
    {
        if (request.ImagesBase64 == null || request.ImagesBase64.Count == 0)
        {
            return BadRequest("At least one image is required");
        }

        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest("Search prompt is required");
        }

        try
        {
            var response = await _trackingService.ProcessFrameAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing tracking request");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all active person tracks
    /// </summary>
    /// <returns>List of active person tracks</returns>
    [HttpGet("tracks")]
    [ProducesResponseType(typeof(List<PersonTrack>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PersonTrack>>> GetActiveTracks()
    {
        var tracks = await _trackingService.GetActiveTracksAsync();
        return Ok(tracks);
    }

    /// <summary>
    /// Get a specific person track by ID
    /// </summary>
    /// <param name="trackingId">The tracking ID</param>
    /// <returns>Person track details</returns>
    [HttpGet("tracks/{trackingId}")]
    [ProducesResponseType(typeof(PersonTrack), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonTrack>> GetTrack(string trackingId)
    {
        var track = await _trackingService.GetTrackByIdAsync(trackingId);
        
        if (track == null)
        {
            return NotFound($"Track with ID '{trackingId}' not found");
        }

        return Ok(track);
    }

    /// <summary>
    /// Upload a video file, extract frames, and store them in Azure Blob Storage
    /// </summary>
    /// <param name="video">Video file to process</param>
    /// <returns>Video upload response with tracking ID and frame count</returns>
    [HttpPost("video/upload")]
    [ProducesResponseType(typeof(VideoUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VideoUploadResponse>> UploadVideo(IFormFile video)
    {
        if (video == null || video.Length == 0)
        {
            return BadRequest("Video file is required");
        }

        try
        {
            var videoName = video.FileName;
            
            // Check cache for existing video
            var cachedTrackingId = await _videoCacheService.GetTrackingIdByVideoNameAsync(videoName);
            if (cachedTrackingId != null)
            {
                // Video already processed, return cached tracking ID
                _logger.LogInformation("Returning cached tracking ID for video: {VideoName}", videoName);
                
                return Ok(new VideoUploadResponse
                {
                    TrackingId = cachedTrackingId,
                    FrameCount = 0, // Not recounting frames from cache
                    WasCached = true,
                    Message = "Video already processed. Returning existing tracking ID."
                });
            }

            // Extract frames from video
            _logger.LogInformation("Processing new video: {VideoName}", videoName);
            
            using var stream = video.OpenReadStream();
            var frames = await _videoProcessingService.ExtractFramesAsync(stream, videoName);

            // Generate tracking ID
            var trackingId = $"video_{Guid.NewGuid():N}";

            // Upload frames to blob storage
            await _frameStorageService.UploadFramesAsync(trackingId, frames);

            // Cache the video name -> tracking ID mapping
            await _videoCacheService.SetTrackingIdForVideoAsync(videoName, trackingId);

            _logger.LogInformation("Successfully processed video {VideoName} with tracking ID {TrackingId}", 
                videoName, trackingId);

            return Ok(new VideoUploadResponse
            {
                TrackingId = trackingId,
                FrameCount = frames.Count,
                WasCached = false,
                Message = $"Video processed successfully. Extracted {frames.Count} frames."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video upload");
            return StatusCode(500, "Internal server error while processing video");
        }
    }

    /// <summary>
    /// Track persons using a previously uploaded video's tracking ID
    /// </summary>
    /// <param name="request">Track by ID request with tracking ID and prompt</param>
    /// <returns>Tracking response with bounding boxes for matched persons</returns>
    [HttpPost("track-by-id")]
    [ProducesResponseType(typeof(TrackingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TrackingResponse>> TrackById([FromBody] TrackByIdRequest request)
    {
        if (string.IsNullOrEmpty(request.TrackingId))
        {
            return BadRequest("Tracking ID is required");
        }

        if (string.IsNullOrEmpty(request.Prompt))
        {
            return BadRequest("Search prompt is required");
        }

        try
        {
            // Check if frames exist for the tracking ID
            var framesExist = await _frameStorageService.FramesExistAsync(request.TrackingId);
            if (!framesExist)
            {
                return NotFound($"No frames found for tracking ID: {request.TrackingId}");
            }

            // Retrieve frames from storage
            var frames = await _frameStorageService.GetFramesAsync(request.TrackingId);
            
            if (frames.Count == 0)
            {
                return NotFound($"No frames found for tracking ID: {request.TrackingId}");
            }

            _logger.LogInformation("Retrieved {FrameCount} frames for tracking ID {TrackingId}", 
                frames.Count, request.TrackingId);

            // Convert frames to base64
            var imagesBase64 = frames.Select(f => Convert.ToBase64String(f)).ToList();

            // Create tracking request
            var trackingRequest = new TrackingRequest
            {
                ImagesBase64 = imagesBase64,
                Prompt = request.Prompt,
                Timestamp = request.Timestamp
            };

            // Process frames using existing tracking service
            var response = await _trackingService.ProcessFrameAsync(trackingRequest);
            
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing track-by-id request for tracking ID: {TrackingId}", 
                request.TrackingId);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Get all tracking IDs that have frames stored in blob storage
    /// </summary>
    /// <returns>List of tracking IDs</returns>
    [HttpGet("tracking-ids")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetTrackingIds()
    {
        try
        {
            var trackingIds = await _frameStorageService.GetAllTrackingIdsAsync();
            return Ok(trackingIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracking IDs");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
