using ADCommsPersonTracking.Api.Models;
using ADCommsPersonTracking.Api.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace ADCommsPersonTracking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PersonTrackingController : ControllerBase
{
    private const int TrackingIdHashLength = 32;
    
    private readonly IPersonTrackingService _trackingService;
    private readonly ILogger<PersonTrackingController> _logger;
    private readonly IVideoProcessingService _videoProcessingService;
    private readonly IFrameStorageService _frameStorageService;
    private readonly IVideoCacheService _videoCacheService;
    private readonly IVideoUploadJobService _videoUploadJobService;

    public PersonTrackingController(
        IPersonTrackingService trackingService,
        ILogger<PersonTrackingController> logger,
        IVideoProcessingService videoProcessingService,
        IFrameStorageService frameStorageService,
        IVideoCacheService videoCacheService,
        IVideoUploadJobService videoUploadJobService)
    {
        _trackingService = trackingService;
        _logger = logger;
        _videoProcessingService = videoProcessingService;
        _frameStorageService = frameStorageService;
        _videoCacheService = videoCacheService;
        _videoUploadJobService = videoUploadJobService;
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
    /// <returns>Video upload job response with job ID for tracking progress</returns>
    [HttpPost("video/upload")]
    [ProducesResponseType(typeof(VideoUploadJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VideoUploadJobResponse>> UploadVideo(IFormFile video)
    {
        if (video == null || video.Length == 0)
        {
            return BadRequest("Video file is required");
        }

        var videoName = video.FileName;
        
        // Create a job entry
        var job = _videoUploadJobService.CreateJob();
        _videoUploadJobService.UpdateProgress(job.JobId, 0, "Receiving video file");

        // Save video to temp file for background processing
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{job.JobId}_{videoName}");
        
        try
        {
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write))
            {
                await video.CopyToAsync(fileStream);
            }
            _videoUploadJobService.UpdateProgress(job.JobId, 5, "Video file received");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving video file for job {JobId}", job.JobId);
            _videoUploadJobService.FailJob(job.JobId, "Failed to save video file");
            
            // Clean up temp file if it exists
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
            
            return StatusCode(500, "Failed to save video file");
        }

        // Start background processing
        _ = Task.Run(async () => await ProcessVideoInBackground(job.JobId, videoName, tempFilePath));

        return Ok(new VideoUploadJobResponse
        {
            JobId = job.JobId,
            Message = "Video upload started. Use the job ID to check progress."
        });
    }

    /// <summary>
    /// Get the status of a video upload job
    /// </summary>
    /// <param name="jobId">The job ID</param>
    /// <returns>Video upload job status</returns>
    [HttpGet("video/upload/status/{jobId}")]
    [ProducesResponseType(typeof(VideoUploadJob), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<VideoUploadJob> GetUploadStatus(string jobId)
    {
        var job = _videoUploadJobService.GetJob(jobId);
        
        if (job == null)
        {
            return NotFound($"Job with ID '{jobId}' not found");
        }

        return Ok(job);
    }

    private async Task ProcessVideoInBackground(string jobId, string videoName, string tempFilePath)
    {
        try
        {
            _videoUploadJobService.UpdateProgress(jobId, 5, "Checking cache");
            
            // Check cache for existing video
            var cachedTrackingId = await _videoCacheService.GetTrackingIdByVideoNameAsync(videoName);
            if (cachedTrackingId != null)
            {
                // Video already processed, return cached tracking ID
                _logger.LogInformation("Returning cached tracking ID for video: {VideoName}", videoName);
                _videoUploadJobService.CompleteJob(jobId, cachedTrackingId, 0);
                
                // Clean up temp file
                if (System.IO.File.Exists(tempFilePath))
                {
                    System.IO.File.Delete(tempFilePath);
                }
                
                return;
            }

            _videoUploadJobService.UpdateProgress(jobId, 10, "Extracting frames");
            _logger.LogInformation("Processing new video: {VideoName}", videoName);
            
            // Extract frames from video
            using var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read);
            var frames = await _videoProcessingService.ExtractFramesAsync(fileStream, videoName);

            _videoUploadJobService.UpdateProgress(jobId, 50, "Uploading frames to storage");

            // Generate deterministic tracking ID based on video filename
            var trackingId = GenerateDeterministicTrackingId(videoName);

            // Upload frames to blob storage with progress updates
            await UploadFramesWithProgress(jobId, trackingId, frames);

            _videoUploadJobService.UpdateProgress(jobId, 90, "Caching tracking ID");
            
            // Cache the video name -> tracking ID mapping
            await _videoCacheService.SetTrackingIdForVideoAsync(videoName, trackingId);

            _videoUploadJobService.UpdateProgress(jobId, 95, "Finalizing");
            _logger.LogInformation("Successfully processed video {VideoName} with tracking ID {TrackingId}", 
                videoName, trackingId);

            _videoUploadJobService.CompleteJob(jobId, trackingId, frames.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing video upload for job {JobId}", jobId);
            _videoUploadJobService.FailJob(jobId, ex.Message);
        }
        finally
        {
            // Clean up temp file
            if (System.IO.File.Exists(tempFilePath))
            {
                try
                {
                    System.IO.File.Delete(tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp file {TempFilePath}", tempFilePath);
                }
            }
        }
    }

    private async Task UploadFramesWithProgress(string jobId, string trackingId, List<byte[]> frames)
    {
        // Upload frames and update progress incrementally
        var totalFrames = frames.Count;
        
        // Use the existing method but wrap it to report progress
        await _frameStorageService.UploadFramesAsync(trackingId, frames);
        
        // Since UploadFramesAsync doesn't provide incremental progress, we just complete at 90%
        _videoUploadJobService.UpdateProgress(jobId, 90, $"Uploaded {totalFrames} frames to storage");
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

    /// <summary>
    /// Generate a deterministic tracking ID based on the video filename.
    /// Uses SHA256 hash to ensure the same video always gets the same tracking ID.
    /// </summary>
    /// <param name="videoName">The name of the video file</param>
    /// <returns>A deterministic tracking ID in the format "video_{hash}"</returns>
    private string GenerateDeterministicTrackingId(string videoName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(videoName));
        var hashString = Convert.ToHexString(hash).ToLowerInvariant()[..TrackingIdHashLength];
        return $"video_{hashString}";
    }
}
