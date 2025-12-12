namespace ADCommsPersonTracking.Api.Services;

public interface IVideoProcessingService
{
    Task<List<byte[]>> ExtractFramesAsync(Stream videoStream, string fileName);
}
