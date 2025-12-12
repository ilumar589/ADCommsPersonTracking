using FFMpegCore;

namespace ADCommsPersonTracking.Api.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IConfiguration _configuration;

    public VideoProcessingService(ILogger<VideoProcessingService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<byte[]>> ExtractFramesAsync(Stream videoStream, string fileName)
    {
        var frames = new List<byte[]>();
        
        try
        {
            var maxFrames = _configuration.GetValue<int>("VideoProcessing:MaxFrames", 100);
            var frameInterval = _configuration.GetValue<int>("VideoProcessing:FrameInterval", 1);

            // Save the video stream to a temporary file
            var tempVideoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");
            
            try
            {
                await using (var fileStream = File.Create(tempVideoPath))
                {
                    await videoStream.CopyToAsync(fileStream);
                }

                _logger.LogInformation("Processing video: {FileName}, Max frames: {MaxFrames}, Interval: {Interval}", 
                    fileName, maxFrames, frameInterval);

                // Get video info
                var videoInfo = await FFMpegCore.FFProbe.AnalyseAsync(tempVideoPath);
                var duration = videoInfo.Duration;
                var frameRate = videoInfo.PrimaryVideoStream?.FrameRate ?? 30;
                
                _logger.LogInformation("Video duration: {Duration}, Frame rate: {FrameRate}", duration, frameRate);

                // Calculate how many frames to extract
                var totalFrames = (int)(duration.TotalSeconds * frameRate);
                var framesToExtract = Math.Min(maxFrames, totalFrames / frameInterval);
                
                _logger.LogInformation("Total frames in video: {TotalFrames}, Extracting: {FramesToExtract}", 
                    totalFrames, framesToExtract);

                // Extract frames at intervals
                for (int i = 0; i < framesToExtract; i++)
                {
                    var timeOffset = TimeSpan.FromSeconds(i * frameInterval / frameRate);
                    
                    // Create temporary file for frame output
                    var tempFramePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                    
                    try
                    {
                        // Extract frame at specific time offset
                        await FFMpegCore.FFMpeg.SnapshotAsync(tempVideoPath, tempFramePath, null, timeOffset);
                        
                        // Read frame into byte array
                        var frameBytes = await File.ReadAllBytesAsync(tempFramePath);
                        frames.Add(frameBytes);
                        
                        _logger.LogDebug("Extracted frame {FrameNumber} at {TimeOffset}", i + 1, timeOffset);
                    }
                    finally
                    {
                        // Clean up temp frame file
                        if (File.Exists(tempFramePath))
                        {
                            File.Delete(tempFramePath);
                        }
                    }
                }

                _logger.LogInformation("Successfully extracted {FrameCount} frames from video {FileName}", 
                    frames.Count, fileName);
            }
            finally
            {
                // Clean up temp file
                if (File.Exists(tempVideoPath))
                {
                    File.Delete(tempVideoPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting frames from video {FileName}", fileName);
            throw;
        }

        return frames;
    }
}
