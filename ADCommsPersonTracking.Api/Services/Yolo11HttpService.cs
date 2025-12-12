using ADCommsPersonTracking.Api.Models;
using System.Text.Json;

namespace ADCommsPersonTracking.Api.Services;

/// <summary>
/// HTTP-based YOLO11 detection service that communicates with the YOLO11 container
/// </summary>
public class Yolo11HttpService : IObjectDetectionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<Yolo11HttpService> _logger;
    private readonly string? _yoloEndpoint;
    private bool _isAvailable = true;

    public Yolo11HttpService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<Yolo11HttpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("Yolo11");
        _logger = logger;
        
        // Try to get endpoint from Aspire service discovery first
        _yoloEndpoint = configuration.GetConnectionString("yolo11") 
            ?? configuration["Yolo11:Endpoint"];
        
        if (string.IsNullOrEmpty(_yoloEndpoint))
        {
            _logger.LogWarning("YOLO11 HTTP endpoint not configured. Service will be unavailable.");
            _isAvailable = false;
        }
        else
        {
            _httpClient.BaseAddress = new Uri(_yoloEndpoint);
            _logger.LogInformation("YOLO11 HTTP service configured at {Endpoint}", _yoloEndpoint);
        }
    }

    public async Task<List<BoundingBox>> DetectPersonsAsync(byte[] imageBytes)
    {
        if (!_isAvailable || string.IsNullOrEmpty(_yoloEndpoint))
        {
            _logger.LogWarning("YOLO11 HTTP service is not available");
            throw new InvalidOperationException("YOLO11 HTTP service is not configured or available");
        }

        try
        {
            _logger.LogDebug("Sending image ({Size} bytes) to YOLO11 HTTP service at {Endpoint}", 
                imageBytes.Length, _yoloEndpoint);

            // Create multipart form data with the image
            using var content = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "image", "frame.jpg");

            // Send request to YOLO11 inference endpoint
            var response = await _httpClient.PostAsync("/inference", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("YOLO11 HTTP service returned error: {StatusCode}", response.StatusCode);
                throw new HttpRequestException($"YOLO11 service returned {response.StatusCode}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var detections = ParseYoloHttpResponse(responseBody);
            
            _logger.LogInformation("YOLO11 HTTP service detected {Count} persons", detections.Count);
            return detections;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to YOLO11 service failed");
            _isAvailable = false;
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling YOLO11 HTTP service");
            throw;
        }
    }

    private List<BoundingBox> ParseYoloHttpResponse(string responseBody)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Parse YOLO11 serve API response format
            // The ultralytics YOLO serve API returns predictions in a specific format
            var response = JsonDocument.Parse(responseBody);
            var detections = new List<BoundingBox>();

            // YOLO serve returns results in format:
            // { "images": [...], "predictions": [[...]] }
            if (response.RootElement.TryGetProperty("predictions", out var predictions) && 
                predictions.GetArrayLength() > 0)
            {
                var firstImagePredictions = predictions[0];
                
                foreach (var detection in firstImagePredictions.EnumerateArray())
                {
                    // YOLO format: [x, y, width, height, confidence, class_id]
                    if (detection.GetArrayLength() >= 6)
                    {
                        var classId = detection[5].GetInt32();
                        
                        // Filter for persons only (class 0 in COCO dataset)
                        if (classId == 0)
                        {
                            var x = detection[0].GetSingle();
                            var y = detection[1].GetSingle();
                            var width = detection[2].GetSingle();
                            var height = detection[3].GetSingle();
                            var confidence = detection[4].GetSingle();

                            detections.Add(new BoundingBox
                            {
                                X = x,
                                Y = y,
                                Width = width,
                                Height = height,
                                Confidence = confidence,
                                Label = "person"
                            });
                        }
                    }
                }
            }

            return detections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse YOLO11 HTTP response: {Response}", responseBody);
            throw new InvalidOperationException("Failed to parse YOLO11 response", ex);
        }
    }
}
