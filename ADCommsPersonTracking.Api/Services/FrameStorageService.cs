using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace ADCommsPersonTracking.Api.Services;

public class FrameStorageService : IFrameStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<FrameStorageService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _containerName;

    public FrameStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<FrameStorageService> logger,
        IConfiguration configuration)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
        _configuration = configuration;
        _containerName = _configuration.GetValue<string>("BlobStorage:ContainerName", "video-frames");
    }

    public async Task<string> UploadFramesAsync(string trackingId, List<byte[]> frames)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

            _logger.LogInformation("Uploading {FrameCount} frames for tracking ID: {TrackingId}", 
                frames.Count, trackingId);

            for (int i = 0; i < frames.Count; i++)
            {
                var blobName = $"{trackingId}/frame_{i:D4}.png";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(frames[i]);
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogDebug("Uploaded frame {FrameIndex} for tracking ID: {TrackingId}", i, trackingId);
            }

            _logger.LogInformation("Successfully uploaded {FrameCount} frames for tracking ID: {TrackingId}", 
                frames.Count, trackingId);

            return trackingId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading frames for tracking ID: {TrackingId}", trackingId);
            throw;
        }
    }

    public async Task<List<byte[]>> GetFramesAsync(string trackingId)
    {
        var frames = new List<byte[]>();

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogWarning("Container {ContainerName} does not exist", _containerName);
                return frames;
            }

            _logger.LogInformation("Retrieving frames for tracking ID: {TrackingId}", trackingId);

            // List all blobs with the tracking ID prefix
            var prefix = $"{trackingId}/";
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                
                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                frames.Add(memoryStream.ToArray());
                
                _logger.LogDebug("Retrieved frame: {BlobName}", blobItem.Name);
            }

            _logger.LogInformation("Successfully retrieved {FrameCount} frames for tracking ID: {TrackingId}", 
                frames.Count, trackingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving frames for tracking ID: {TrackingId}", trackingId);
            throw;
        }

        return frames;
    }

    public async Task<bool> FramesExistAsync(string trackingId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            if (!await containerClient.ExistsAsync())
            {
                return false;
            }

            var prefix = $"{trackingId}/";
            await foreach (var _ in containerClient.GetBlobsAsync(prefix: prefix))
            {
                // If we find at least one blob, frames exist
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if frames exist for tracking ID: {TrackingId}", trackingId);
            return false;
        }
    }
}
