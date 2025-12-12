using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// Composite object detection service that tries HTTP-based YOLO11 first,
/// then falls back to local ONNX inference if HTTP is unavailable
/// </summary>
public class CompositeObjectDetectionService : IObjectDetectionService
{
    private const int DefaultRetryIntervalMinutes = 5;
    
    private readonly Yolo11HttpService _httpService;
    private readonly ObjectDetectionService _onnxService;
    private readonly ILogger<CompositeObjectDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _retryInterval;
    private bool _httpServiceFailed = false;
    private DateTime _lastHttpAttempt = DateTime.MinValue;

    public CompositeObjectDetectionService(
        Yolo11HttpService httpService,
        ObjectDetectionService onnxService,
        IConfiguration configuration,
        ILogger<CompositeObjectDetectionService> logger)
    {
        _httpService = httpService;
        _onnxService = onnxService;
        _configuration = configuration;
        _logger = logger;

        // Read retry interval from configuration
        var retryMinutes = configuration.GetValue<int?>("ObjectDetection:HttpRetryIntervalMinutes") 
            ?? DefaultRetryIntervalMinutes;
        _retryInterval = TimeSpan.FromMinutes(retryMinutes);

        var detectionMode = configuration["ObjectDetection:Mode"];
        _logger.LogInformation("Object detection mode: {Mode}", detectionMode ?? "Auto (HTTP with ONNX fallback)");
        _logger.LogInformation("HTTP retry interval: {Minutes} minutes", retryMinutes);
    }

    public async Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes)
    {
        var mode = _configuration["ObjectDetection:Mode"]?.ToLowerInvariant() ?? "auto";

        switch (mode)
        {
            case "http":
                return await DetectWithHttpOnlyAsync(imageBytes);
            
            case "onnx":
                return await DetectWithOnnxOnlyAsync(imageBytes);
            
            case "auto":
            default:
                return await DetectWithAutoFallbackAsync(imageBytes);
        }
    }

    private async Task<List<BoundingBox>> DetectWithHttpOnlyAsync(byte[] imageBytes)
    {
        _logger.LogDebug("Using HTTP-only detection mode");
        return await _httpService.DetectPersonsAsync(imageBytes);
    }

    private async Task<List<BoundingBox>> DetectWithOnnxOnlyAsync(byte[] imageBytes)
    {
        _logger.LogDebug("Using ONNX-only detection mode");
        return await _onnxService.DetectPersonsAsync(imageBytes);
    }

    private async Task<List<BoundingBox>> DetectWithAutoFallbackAsync(byte[] imageBytes)
    {
        // If HTTP service failed recently, don't retry yet
        if (_httpServiceFailed && DateTime.UtcNow - _lastHttpAttempt < _retryInterval)
        {
            _logger.LogDebug("HTTP service recently failed, using ONNX fallback");
            return await _onnxService.DetectPersonsAsync(imageBytes);
        }

        try
        {
            _logger.LogDebug("Attempting HTTP-based detection");
            _lastHttpAttempt = DateTime.UtcNow;
            var result = await _httpService.DetectPersonsAsync(imageBytes);
            
            // If we succeed and it was previously failed, log recovery
            if (_httpServiceFailed)
            {
                _logger.LogInformation("HTTP service recovered and is now available");
                _httpServiceFailed = false;
            }
            
            return result;
        }
        catch (Exception ex)
        {
            if (!_httpServiceFailed)
            {
                _logger.LogWarning(ex, "HTTP-based detection failed, falling back to local ONNX inference");
                _httpServiceFailed = true;
            }
            else
            {
                _logger.LogDebug(ex, "HTTP service still unavailable, using ONNX fallback");
            }

            try
            {
                return await _onnxService.DetectPersonsAsync(imageBytes);
            }
            catch (Exception onnxEx)
            {
                _logger.LogError(onnxEx, "Both HTTP and ONNX detection methods failed");
                throw new InvalidOperationException("All detection methods failed", onnxEx);
            }
        }
    }
}
