using Microsoft.Extensions.Caching.Distributed;
using System.Text;

namespace ADCommsPersonTracking.Api.Services;

public class VideoCacheService : IVideoCacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<VideoCacheService> _logger;
    private const int CacheExpirationMinutes = 5;

    public VideoCacheService(IDistributedCache cache, ILogger<VideoCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<string?> GetTrackingIdByVideoNameAsync(string videoName)
    {
        try
        {
            var cacheKey = GetCacheKey(videoName);
            var cachedValue = await _cache.GetAsync(cacheKey);

            if (cachedValue != null)
            {
                var trackingId = Encoding.UTF8.GetString(cachedValue);
                _logger.LogInformation("Cache hit for video: {VideoName}, Tracking ID: {TrackingId}", 
                    videoName, trackingId);
                return trackingId;
            }

            _logger.LogInformation("Cache miss for video: {VideoName}", videoName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving tracking ID from cache for video: {VideoName}", videoName);
            return null;
        }
    }

    public async Task SetTrackingIdForVideoAsync(string videoName, string trackingId)
    {
        try
        {
            var cacheKey = GetCacheKey(videoName);
            var cacheValue = Encoding.UTF8.GetBytes(trackingId);
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheExpirationMinutes)
            };

            await _cache.SetAsync(cacheKey, cacheValue, options);
            
            _logger.LogInformation("Cached tracking ID: {TrackingId} for video: {VideoName} with {Minutes} minute expiration", 
                trackingId, videoName, CacheExpirationMinutes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching tracking ID for video: {VideoName}", videoName);
            // Don't throw - caching is not critical
        }
    }

    private static string GetCacheKey(string videoName) => $"video:{videoName}";
}
