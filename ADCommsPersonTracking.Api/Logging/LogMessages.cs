using Microsoft.Extensions.Logging;

namespace ADCommsPersonTracking.Api.Logging;

public static partial class LogMessages
{
    // PromptFeatureExtractor
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Extracted features from prompt - Colors: {Colors}, Clothing: {Clothing}, Accessories: {Accessories}, Height: {Height}")]
    public static partial void LogExtractedFeatures(
        this ILogger logger,
        string colors,
        string clothing,
        string accessories,
        string height);

    // ObjectDetectionService
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "YOLO11 ONNX model loaded successfully from {ModelPath}")]
    public static partial void LogModelLoaded(
        this ILogger logger,
        string modelPath);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "Could not load YOLO11 ONNX model from {ModelPath}. Detection will use mock data.")]
    public static partial void LogModelLoadWarning(
        this ILogger logger,
        string modelPath,
        Exception ex);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "YOLO11 ONNX model not found at {ModelPath}. Detection will use mock data.")]
    public static partial void LogModelNotFound(
        this ILogger logger,
        string modelPath);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Detected {Count} persons in frame")]
    public static partial void LogDetectedPersons(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Error during object detection")]
    public static partial void LogObjectDetectionError(
        this ILogger logger,
        Exception ex);

    // PersonTrackingService
    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Information,
        Message = "Processing {Count} images with prompt: {Prompt}")]
    public static partial void LogProcessingImages(
        this ILogger logger,
        int count,
        string prompt);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Extracted {Count} search features from prompt (colors: {Colors})")]
    public static partial void LogExtractedSearchFeatures(
        this ILogger logger,
        int count,
        string colors);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Information,
        Message = "Detected {Count} persons in image {Index}")]
    public static partial void LogDetectedPersonsInImage(
        this ILogger logger,
        int count,
        int index);

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Information,
        Message = "Returning {Count} matched detections out of {Total} total detections")]
    public static partial void LogMatchedDetections(
        this ILogger logger,
        int count,
        int total);

    [LoggerMessage(
        EventId = 11,
        Level = LogLevel.Debug,
        Message = "Removed old track {TrackId}")]
    public static partial void LogRemovedTrack(
        this ILogger logger,
        string trackId);

    [LoggerMessage(
        EventId = 12,
        Level = LogLevel.Error,
        Message = "Error processing frame")]
    public static partial void LogProcessingError(
        this ILogger logger,
        Exception ex);

    // ColorAnalysisService
    [LoggerMessage(
        EventId = 13,
        Level = LogLevel.Warning,
        Message = "Invalid bounding box dimensions for person")]
    public static partial void LogInvalidBoundingBox(
        this ILogger logger);

    [LoggerMessage(
        EventId = 14,
        Level = LogLevel.Error,
        Message = "Error analyzing person colors")]
    public static partial void LogColorAnalysisError(
        this ILogger logger,
        Exception ex);

    // ImageAnnotationService
    [LoggerMessage(
        EventId = 15,
        Level = LogLevel.Debug,
        Message = "Annotated image with {Count} bounding boxes")]
    public static partial void LogAnnotatedImage(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 16,
        Level = LogLevel.Error,
        Message = "Error annotating image")]
    public static partial void LogAnnotationError(
        this ILogger logger,
        Exception ex);

    [LoggerMessage(
        EventId = 17,
        Level = LogLevel.Warning,
        Message = "Could not draw label, fonts may not be available")]
    public static partial void LogLabelDrawWarning(
        this ILogger logger,
        Exception ex);

    [LoggerMessage(
        EventId = 18,
        Level = LogLevel.Warning,
        Message = "Could not parse color {Hex}, using default green")]
    public static partial void LogColorParseWarning(
        this ILogger logger,
        string hex,
        Exception ex);

    // VideoProcessingService
    [LoggerMessage(
        EventId = 19,
        Level = LogLevel.Information,
        Message = "Checking FFmpeg installation...")]
    public static partial void LogCheckingFFmpeg(
        this ILogger logger);

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Debug,
        Message = "FFmpeg directory: {Path}")]
    public static partial void LogFFmpegDirectory(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Information,
        Message = "FFmpeg binaries not found. Downloading version 6.1 from official sources...")]
    public static partial void LogFFmpegDownloading(
        this ILogger logger);

    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Information,
        Message = "FFmpeg binaries (v6.1) downloaded successfully to: {Path}")]
    public static partial void LogFFmpegDownloaded(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventId = 23,
        Level = LogLevel.Information,
        Message = "FFmpeg binaries already present at: {Path}")]
    public static partial void LogFFmpegPresent(
        this ILogger logger,
        string path);

    [LoggerMessage(
        EventId = 24,
        Level = LogLevel.Information,
        Message = "FFmpeg initialized successfully")]
    public static partial void LogFFmpegInitialized(
        this ILogger logger);

    [LoggerMessage(
        EventId = 25,
        Level = LogLevel.Error,
        Message = "Failed to initialize FFmpeg")]
    public static partial void LogFFmpegInitializationError(
        this ILogger logger,
        Exception ex);

    [LoggerMessage(
        EventId = 26,
        Level = LogLevel.Information,
        Message = "Processing video: {FileName}, Max frames: {MaxFrames}, Interval: {Interval}")]
    public static partial void LogProcessingVideo(
        this ILogger logger,
        string fileName,
        int maxFrames,
        int interval);

    [LoggerMessage(
        EventId = 27,
        Level = LogLevel.Information,
        Message = "Video duration: {Duration}, Frame rate: {FrameRate}")]
    public static partial void LogVideoInfo(
        this ILogger logger,
        TimeSpan duration,
        double frameRate);

    [LoggerMessage(
        EventId = 28,
        Level = LogLevel.Information,
        Message = "Total frames in video: {TotalFrames}, Extracting: {FramesToExtract}")]
    public static partial void LogFrameExtraction(
        this ILogger logger,
        int totalFrames,
        int framesToExtract);

    [LoggerMessage(
        EventId = 29,
        Level = LogLevel.Debug,
        Message = "Extracted frame {FrameNumber} at {TimeOffset}")]
    public static partial void LogExtractedFrame(
        this ILogger logger,
        int frameNumber,
        TimeSpan timeOffset);

    [LoggerMessage(
        EventId = 30,
        Level = LogLevel.Information,
        Message = "Successfully extracted {FrameCount} frames from video {FileName}")]
    public static partial void LogFramesExtracted(
        this ILogger logger,
        int frameCount,
        string fileName);

    [LoggerMessage(
        EventId = 31,
        Level = LogLevel.Error,
        Message = "Error extracting frames from video {FileName}")]
    public static partial void LogFrameExtractionError(
        this ILogger logger,
        string fileName,
        Exception ex);

    // FrameStorageService
    [LoggerMessage(
        EventId = 32,
        Level = LogLevel.Information,
        Message = "Uploading {FrameCount} frames for tracking ID: {TrackingId}")]
    public static partial void LogUploadingFrames(
        this ILogger logger,
        int frameCount,
        string trackingId);

    [LoggerMessage(
        EventId = 33,
        Level = LogLevel.Debug,
        Message = "Uploaded frame {FrameIndex} for tracking ID: {TrackingId}")]
    public static partial void LogUploadedFrame(
        this ILogger logger,
        int frameIndex,
        string trackingId);

    [LoggerMessage(
        EventId = 34,
        Level = LogLevel.Information,
        Message = "Successfully uploaded {FrameCount} frames for tracking ID: {TrackingId}")]
    public static partial void LogFramesUploaded(
        this ILogger logger,
        int frameCount,
        string trackingId);

    [LoggerMessage(
        EventId = 35,
        Level = LogLevel.Error,
        Message = "Error uploading frames for tracking ID: {TrackingId}")]
    public static partial void LogFrameUploadError(
        this ILogger logger,
        string trackingId,
        Exception ex);

    [LoggerMessage(
        EventId = 36,
        Level = LogLevel.Warning,
        Message = "Container {ContainerName} does not exist")]
    public static partial void LogContainerNotExist(
        this ILogger logger,
        string containerName);

    [LoggerMessage(
        EventId = 37,
        Level = LogLevel.Information,
        Message = "Retrieving frames for tracking ID: {TrackingId}")]
    public static partial void LogRetrievingFrames(
        this ILogger logger,
        string trackingId);

    [LoggerMessage(
        EventId = 38,
        Level = LogLevel.Debug,
        Message = "Retrieved frame: {BlobName}")]
    public static partial void LogRetrievedFrame(
        this ILogger logger,
        string blobName);

    [LoggerMessage(
        EventId = 39,
        Level = LogLevel.Information,
        Message = "Successfully retrieved {FrameCount} frames for tracking ID: {TrackingId}")]
    public static partial void LogFramesRetrieved(
        this ILogger logger,
        int frameCount,
        string trackingId);

    [LoggerMessage(
        EventId = 40,
        Level = LogLevel.Error,
        Message = "Error retrieving frames for tracking ID: {TrackingId}")]
    public static partial void LogFrameRetrievalError(
        this ILogger logger,
        string trackingId,
        Exception ex);

    [LoggerMessage(
        EventId = 41,
        Level = LogLevel.Error,
        Message = "Error checking if frames exist for tracking ID: {TrackingId}")]
    public static partial void LogFrameExistenceCheckError(
        this ILogger logger,
        string trackingId,
        Exception ex);

    [LoggerMessage(
        EventId = 42,
        Level = LogLevel.Information,
        Message = "Retrieving all tracking IDs from blob storage")]
    public static partial void LogRetrievingTrackingIds(
        this ILogger logger);

    [LoggerMessage(
        EventId = 43,
        Level = LogLevel.Information,
        Message = "Found {Count} unique tracking IDs")]
    public static partial void LogFoundTrackingIds(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 44,
        Level = LogLevel.Error,
        Message = "Error retrieving tracking IDs from blob storage")]
    public static partial void LogTrackingIdsRetrievalError(
        this ILogger logger,
        Exception ex);
}
