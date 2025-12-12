using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using FFMpegCore.Extensions.Downloader.Enums;

namespace ADCommsPersonTracking.Api.Services;

public class VideoProcessingService : IVideoProcessingService
{
    private readonly ILogger<VideoProcessingService> _logger;
    private readonly IConfiguration _configuration;
    private static bool _ffmpegInitialized = false;
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

            _logger.LogInformation("Checking FFmpeg installation...");
            
            // Get FFmpeg binaries path - use application data folder for storage
            var ffmpegPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ADCommsPersonTracking",
                "ffmpeg"
            );

            // Ensure directory exists
            Directory.CreateDirectory(ffmpegPath);

            // Check if FFmpeg binaries already exist
            var ffmpegExe = Path.Combine(ffmpegPath, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            var ffprobeExe = Path.Combine(ffmpegPath, OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

            if (!File.Exists(ffmpegExe) || !File.Exists(ffprobeExe))
            {
                _logger.LogInformation("FFmpeg binaries not found. Downloading...");
                
                // Download FFmpeg binaries (both FFMpeg and FFProbe)
                var options = new FFOptions { BinaryFolder = ffmpegPath };
                await FFMpegDownloader.DownloadBinaries(
                    FFMpegVersions.LatestAvailable,
                    FFMpegBinaries.FFMpeg | FFMpegBinaries.FFProbe,
                    options
                );
                
                _logger.LogInformation("FFmpeg binaries downloaded successfully to: {Path}", ffmpegPath);
            }
            else
            {
                _logger.LogInformation("FFmpeg binaries already present at: {Path}", ffmpegPath);
            }

            // Configure FFMpegCore to use the downloaded binaries
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegPath);
            
            _ffmpegInitialized = true;
            _logger.LogInformation("FFmpeg initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize FFmpeg");
            throw new InvalidOperationException("Failed to initialize FFmpeg. Video processing is unavailable.", ex);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<byte[]>> ExtractFramesAsync(Stream videoStream, string fileName)
    {
        // Ensure FFmpeg is installed before processing
        await EnsureFFmpegInstalledAsync();
        
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
