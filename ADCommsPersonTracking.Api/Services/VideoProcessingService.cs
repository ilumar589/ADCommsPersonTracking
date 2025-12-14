using ADCommsPersonTracking.Api.Logging;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;

namespace ADCommsPersonTracking.Api.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IConfiguration _configuration;
    private static bool _ffmpegInitialized = false;
    // Static semaphore for thread-safe initialization across all service instances
    // This is intentionally not disposed as it's shared across the application lifetime
    private static readonly SemaphoreSlim _initLock = new(1, 1);

    public VideoProcessingService(ILogger<VideoProcessingService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    private async Task EnsureFFmpegInstalledAsync()
    {
        if (_ffmpegInitialized)
            return;

        await _initLock.WaitAsync();
        try
        {
            if (_ffmpegInitialized)
                return;

            _logger.LogCheckingFFmpeg();
            
            // Get FFmpeg binaries path - use application data folder for storage
            var ffmpegPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ADCommsPersonTracking",
                "ffmpeg"
            );

            // Ensure directory exists (will throw if no permissions, caught by outer try-catch)
            Directory.CreateDirectory(ffmpegPath);
            _logger.LogFFmpegDirectory(ffmpegPath);

            // Check if FFmpeg binaries already exist
            var ffmpegExe = Path.Combine(ffmpegPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            var ffprobeExe = Path.Combine(ffmpegPath, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

            if (!File.Exists(ffmpegExe) || !File.Exists(ffprobeExe))
            {
                _logger.LogFFmpegDownloading();
                
                // Download FFmpeg binaries (both FFMpeg and FFProbe)
                // Note: Pinned to V6_1 for security and consistency
                var options = new FFOptions { BinaryFolder = ffmpegPath };
                await FFMpegDownloader.DownloadBinaries(
                    FFMpegVersions.V6_1,
                    FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe,
                    options
                );
                
                _logger.LogFFmpegDownloaded(ffmpegPath);
            }
            else
            {
                _logger.LogFFmpegPresent(ffmpegPath);
            }

            // Configure FFMpegCore to use the downloaded binaries
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
            
            _ffmpegInitialized = true;
            _logger.LogFFmpegInitialized();
        }
        catch (Exception ex)
        {
            _logger.LogFFmpegInitializationError(ex);
            throw new InvalidOperationException("Failed to initialize FFmpeg. Video processing is unavailable.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<byte[]>> ExtractFramesAsync(Stream videoStream, string fileName, int? maxFrames = null)
    {
        // Ensure FFmpeg is installed before processing
        await EnsureFFmpegInstalledAsync();
        
        var frames = new List<byte[]>();
        
        try
        {
            var effectiveMaxFrames = maxFrames ?? _configuration.GetValue<int>("VideoProcessing:MaxFrames", 100);
            var frameInterval = _configuration.GetValue<int>("VideoProcessing:FrameInterval", 1);

            // Save the video stream to a temporary file
            var tempVideoPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{fileName}");
            
            try
            {
                await using (var fileStream = File.Create(tempVideoPath))
                {
                    await videoStream.CopyToAsync(fileStream);
                }

                _logger.LogProcessingVideo(fileName, effectiveMaxFrames, frameInterval);

                // Get video info
                var videoInfo = await FFMpegCore.FFProbe.AnalyseAsync(tempVideoPath);
                var duration = videoInfo.Duration;
                var frameRate = videoInfo.PrimaryVideoStream?.FrameRate ?? 30;
                
                _logger.LogVideoInfo(duration, frameRate);

                // Calculate how many frames to extract
                var totalFrames = (int)(duration.TotalSeconds * frameRate);
                var framesToExtract = Math.Min(effectiveMaxFrames, totalFrames);
                
                _logger.LogFrameExtraction(totalFrames, framesToExtract);

                // Calculate the interval between frames based on video duration to distribute frames evenly
                var intervalBetweenFrames = duration.TotalSeconds / framesToExtract;

                // Extract frames distributed evenly across the video
                for (int i = 0; i < framesToExtract; i++)
                {
                    var timeOffset = TimeSpan.FromSeconds(i * intervalBetweenFrames);
                    
                    // Create temporary file for frame output
                    var tempFramePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
                    
                    try
                    {
                        // Extract frame at specific time offset
                        await FFMpegCore.FFMpeg.SnapshotAsync(tempVideoPath, tempFramePath, null, timeOffset);
                        
                        // Read frame into byte array
                        var frameBytes = await File.ReadAllBytesAsync(tempFramePath);
                        frames.Add(frameBytes);
                        
                        _logger.LogExtractedFrame(i + 1, timeOffset);
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

                _logger.LogFramesExtracted(frames.Count, fileName);
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
            _logger.LogFrameExtractionError(fileName, ex);
            throw;
        }

        return frames;
    }
}
