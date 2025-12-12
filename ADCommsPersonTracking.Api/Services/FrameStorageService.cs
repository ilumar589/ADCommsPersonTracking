using ADCommsPersonTracking.Api.Logging;
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

            _logger.LogUploadingFrames(frames.Count, trackingId);

            for (int i = 0; i < frames.Count; i++)
            {
                var blobName = $"{trackingId}/frame_{i:D4}.png";
                var blobClient = containerClient.GetBlobClient(blobName);

                using var stream = new MemoryStream(frames[i]);
                await blobClient.UploadAsync(stream, overwrite: true);
                
                _logger.LogUploadedFrame(i, trackingId);
            }

            _logger.LogFramesUploaded(frames.Count, trackingId);

            return trackingId;
        }
        catch (Exception ex)
        {
            _logger.LogFrameUploadError(trackingId, ex);
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
                _logger.LogContainerNotExist(_containerName);
                return frames;
            }

            _logger.LogRetrievingFrames(trackingId);

            // List all blobs with the tracking ID prefix
            var prefix = $"{trackingId}/";
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                
                using var memoryStream = new MemoryStream();
                await blobClient.DownloadToAsync(memoryStream);
                frames.Add(memoryStream.ToArray());
                
                _logger.LogRetrievedFrame(blobItem.Name);
            }

            _logger.LogFramesRetrieved(frames.Count, trackingId);
        }
        catch (Exception ex)
        {
            _logger.LogFrameRetrievalError(trackingId, ex);
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
            _logger.LogFrameExistenceCheckError(trackingId, ex);
            return false;
        }
    }

    public async Task<List<string>> GetAllTrackingIdsAsync()
    {
        var trackingIds = new HashSet<string>();

        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
            
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogContainerNotExist(_containerName);
                return new List<string>();
            }

            _logger.LogRetrievingTrackingIds();

            // List all blobs in the container
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                // Extract tracking ID from blob name (format: trackingId/frame_xxxx.png)
                var parts = blobItem.Name.Split('/');
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    trackingIds.Add(parts[0]);
                }
            }

            _logger.LogFoundTrackingIds(trackingIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogTrackingIdsRetrievalError(ex);
            throw;
        }

        return trackingIds.OrderByDescending(id => id).ToList();
    }
}
