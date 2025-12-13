using ADCommsPersonTracking.Web.Models;

namespace ADCommsPersonTracking.Web.Services;

public class PersonTrackingApiService : IPersonTrackingApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PersonTrackingApiService> _logger;

    public PersonTrackingApiService(HttpClient httpClient, ILogger<PersonTrackingApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<HealthResponse?> GetHealthAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<HealthResponse>("api/persontracking/health");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health status");
            return null;
        }
    }

    public async Task<TrackingResponse?> SubmitTrackingRequestAsync(TrackingRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/persontracking/track", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TrackingResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting tracking request");
            return null;
        }
    }

    public async Task<List<PersonTrack>?> GetActiveTracksAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<PersonTrack>>("api/persontracking/tracks");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active tracks");
            return null;
        }
    }

    public async Task<PersonTrack?> GetTrackByIdAsync(string trackingId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<PersonTrack>($"api/persontracking/tracks/{trackingId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track by ID: {TrackingId}", trackingId);
            return null;
        }
    }

    public async Task<VideoUploadJobResponse?> UploadVideoAsync(Stream videoStream, string fileName)
    {
        try
        {
            // Ensure we have a seekable stream for retry support
            Stream seekableStream;
            bool shouldDisposeStream = false;

            if (videoStream.CanSeek)
            {
                seekableStream = videoStream;
            }
            else
            {
                // Buffer non-seekable streams
                var memoryStream = new MemoryStream();
                await videoStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                seekableStream = memoryStream;
                shouldDisposeStream = true;
            }

            try
            {
                seekableStream.Position = 0; // Reset position in case of retry
                
                using var content = new MultipartFormDataContent();
                var streamContent = new StreamContent(seekableStream);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("video/mp4");
                content.Add(streamContent, "video", fileName);

                var response = await _httpClient.PostAsync("api/persontracking/video/upload", content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<VideoUploadJobResponse>();
            }
            finally
            {
                if (shouldDisposeStream)
                {
                    await seekableStream.DisposeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading video: {FileName}", fileName);
            return null;
        }
    }

    public async Task<VideoUploadJobStatus?> GetVideoUploadStatusAsync(string jobId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<VideoUploadJobStatus>($"api/persontracking/video/upload/status/{jobId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video upload status: {JobId}", jobId);
            return null;
        }
    }

    public async Task<TrackByIdJobResponse?> TrackByIdAsync(TrackByIdRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/persontracking/track-by-id", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TrackByIdJobResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting track-by-id request: {TrackingId}", request.TrackingId);
            return null;
        }
    }

    public async Task<TrackByIdJobStatus?> GetTrackByIdStatusAsync(string jobId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<TrackByIdJobStatus>($"api/persontracking/track-by-id/status/{jobId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting track-by-id status: {JobId}", jobId);
            return null;
        }
    }

    public async Task<List<string>?> GetTrackingIdsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>("api/persontracking/tracking-ids");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracking IDs");
            return null;
        }
    }

    public async Task<TrackByIdWithDiagnosticsResponse?> TrackByIdWithDiagnosticsAsync(TrackByIdRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/persontracking/track-by-id-with-diagnostics", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<TrackByIdWithDiagnosticsResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting track-by-id with diagnostics request: {TrackingId}", request.TrackingId);
            return null;
        }
    }

    public async Task<InferenceDiagnostics?> GetDiagnosticsAsync(string sessionId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<InferenceDiagnostics>($"api/persontracking/diagnostics/{sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting diagnostics: {SessionId}", sessionId);
            return null;
        }
    }

    public async Task<InferenceDiagnostics?> GetLatestDiagnosticsAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<InferenceDiagnostics>("api/persontracking/diagnostics/latest");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest diagnostics");
            return null;
        }
    }
}
