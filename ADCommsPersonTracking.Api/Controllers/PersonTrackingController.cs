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

    public PersonTrackingController(
        IPersonTrackingService trackingService,
        ILogger<PersonTrackingController> logger)
    {
        _trackingService = trackingService;
        _logger = logger;
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
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult HealthCheck()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
}
