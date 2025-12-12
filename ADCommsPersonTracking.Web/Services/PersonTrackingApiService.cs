using System.Net.Http.Json;
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
}
