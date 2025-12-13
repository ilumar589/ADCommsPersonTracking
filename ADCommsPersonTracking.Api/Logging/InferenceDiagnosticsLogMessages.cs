namespace ADCommsPersonTracking.Api.Logging;

public static partial class InferenceDiagnosticsLogMessages
{
    // PromptFeatureExtractor - Detailed keyword extraction
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Debug,
        Message = "Starting feature extraction from prompt: '{Prompt}'")]
    public static partial void LogPromptFeatureExtractionStart(
        this ILogger logger,
        string prompt);

    [LoggerMessage(
        EventId = 101,
        Level = LogLevel.Debug,
        Message = "Searching for colors in prompt, found: {FoundCount} colors")]
    public static partial void LogColorExtractionResult(
        this ILogger logger,
        int foundCount);

    [LoggerMessage(
        EventId = 102,
        Level = LogLevel.Debug,
        Message = "Color keyword '{Keyword}' found in prompt")]
    public static partial void LogColorKeywordFound(
        this ILogger logger,
        string keyword);

    [LoggerMessage(
        EventId = 103,
        Level = LogLevel.Debug,
        Message = "Searching for clothing items in prompt, found: {FoundCount} items")]
    public static partial void LogClothingExtractionResult(
        this ILogger logger,
        int foundCount);

    [LoggerMessage(
        EventId = 104,
        Level = LogLevel.Debug,
        Message = "Clothing keyword '{Keyword}' found in prompt")]
    public static partial void LogClothingKeywordFound(
        this ILogger logger,
        string keyword);

    [LoggerMessage(
        EventId = 105,
        Level = LogLevel.Debug,
        Message = "Searching for accessories in prompt, found: {FoundCount} accessories")]
    public static partial void LogAccessoryExtractionResult(
        this ILogger logger,
        int foundCount);

    [LoggerMessage(
        EventId = 106,
        Level = LogLevel.Debug,
        Message = "Accessory keyword '{Keyword}' found in prompt (IMPORTANT for backpack detection)")]
    public static partial void LogAccessoryKeywordFound(
        this ILogger logger,
        string keyword);

    [LoggerMessage(
        EventId = 107,
        Level = LogLevel.Debug,
        Message = "Searching for physical attributes in prompt, found: {FoundCount} attributes")]
    public static partial void LogPhysicalAttributeExtractionResult(
        this ILogger logger,
        int foundCount);

    [LoggerMessage(
        EventId = 108,
        Level = LogLevel.Debug,
        Message = "Physical attribute keyword '{Keyword}' found in prompt")]
    public static partial void LogPhysicalAttributeKeywordFound(
        this ILogger logger,
        string keyword);

    [LoggerMessage(
        EventId = 109,
        Level = LogLevel.Information,
        Message = "Final search criteria - Colors: [{Colors}], Clothing: [{Clothing}], Accessories: [{Accessories}], Physical: [{Physical}], Height: {Height}")]
    public static partial void LogFinalSearchCriteria(
        this ILogger logger,
        string colors,
        string clothing,
        string accessories,
        string physical,
        string height);

    // PersonTrackingService - Processing flow
    [LoggerMessage(
        EventId = 110,
        Level = LogLevel.Information,
        Message = "ProcessFrameAsync started - Images: {ImageCount}, Prompt: '{Prompt}'")]
    public static partial void LogProcessFrameStart(
        this ILogger logger,
        int imageCount,
        string prompt);

    [LoggerMessage(
        EventId = 111,
        Level = LogLevel.Information,
        Message = "HasCriteria: {HasCriteria} - Colors: {ColorCount}, Clothing: {ClothingCount}, Accessories: {AccessoryCount}, Physical: {PhysicalCount}, Height: {HasHeight}")]
    public static partial void LogCriteriaBreakdown(
        this ILogger logger,
        bool hasCriteria,
        int colorCount,
        int clothingCount,
        int accessoryCount,
        int physicalCount,
        bool hasHeight);

    [LoggerMessage(
        EventId = 112,
        Level = LogLevel.Debug,
        Message = "Processing image {ImageIndex} - Size: {Width}x{Height}, Base64 length: {Base64Length}")]
    public static partial void LogImageProcessingStart(
        this ILogger logger,
        int imageIndex,
        int width,
        int height,
        int base64Length);

    [LoggerMessage(
        EventId = 113,
        Level = LogLevel.Information,
        Message = "Image {ImageIndex} - Taking accessory detection path: {TakingAccessoryPath} (Accessory criteria count: {AccessoryCount}, Clothing criteria count: {ClothingCount})")]
    public static partial void LogAccessoryDetectionPath(
        this ILogger logger,
        int imageIndex,
        bool takingAccessoryPath,
        int accessoryCount,
        int clothingCount);

    [LoggerMessage(
        EventId = 114,
        Level = LogLevel.Debug,
        Message = "Image {ImageIndex} - YOLO detected {TotalObjects} objects: {PersonCount} persons, {AccessoryCount} accessories/items")]
    public static partial void LogYoloDetectionSummary(
        this ILogger logger,
        int imageIndex,
        int totalObjects,
        int personCount,
        int accessoryCount);

    [LoggerMessage(
        EventId = 115,
        Level = LogLevel.Debug,
        Message = "YOLO detection - ClassId: {ClassId}, ClassName: '{ClassName}', Confidence: {Confidence:F3}, BBox: ({X:F1},{Y:F1},{W:F1},{H:F1})")]
    public static partial void LogYoloDetection(
        this ILogger logger,
        int classId,
        string className,
        float confidence,
        float x,
        float y,
        float w,
        float h);

    [LoggerMessage(
        EventId = 116,
        Level = LogLevel.Debug,
        Message = "Person {PersonIndex} - Detected colors: Upper[{UpperColors}], Lower[{LowerColors}], Overall[{OverallColors}]")]
    public static partial void LogPersonColorAnalysis(
        this ILogger logger,
        int personIndex,
        string upperColors,
        string lowerColors,
        string overallColors);

    [LoggerMessage(
        EventId = 117,
        Level = LogLevel.Debug,
        Message = "Person {PersonIndex} - Physical attributes: {Attributes}, Height: {Height:F2}m")]
    public static partial void LogPersonPhysicalAttributes(
        this ILogger logger,
        int personIndex,
        string attributes,
        float height);

    [LoggerMessage(
        EventId = 118,
        Level = LogLevel.Information,
        Message = "Person {PersonIndex} - Matching results: Colors={MatchColors}, Accessories={MatchAccessories}, Physical={MatchPhysical}, Overall={OverallMatch}")]
    public static partial void LogPersonMatchingResult(
        this ILogger logger,
        int personIndex,
        bool matchColors,
        bool matchAccessories,
        bool matchPhysical,
        bool overallMatch);

    [LoggerMessage(
        EventId = 119,
        Level = LogLevel.Debug,
        Message = "Person {PersonIndex} - Excluded: {Reason}")]
    public static partial void LogPersonExcluded(
        this ILogger logger,
        int personIndex,
        string reason);

    [LoggerMessage(
        EventId = 120,
        Level = LogLevel.Information,
        Message = "Processing complete - Total: {TotalDetections}, Matched: {MatchedDetections}, Criteria: {CriteriaDescription}")]
    public static partial void LogProcessingComplete(
        this ILogger logger,
        int totalDetections,
        int matchedDetections,
        string criteriaDescription);

    // AccessoryDetectionService - Association logic
    [LoggerMessage(
        EventId = 130,
        Level = LogLevel.Debug,
        Message = "DetectAccessoriesFromYolo - Person box: ({X:F1},{Y:F1},{W:F1},{H:F1}), Total accessories to evaluate: {AccessoryCount}")]
    public static partial void LogAccessoryDetectionStart(
        this ILogger logger,
        float x,
        float y,
        float w,
        float h,
        int accessoryCount);

    [LoggerMessage(
        EventId = 131,
        Level = LogLevel.Debug,
        Message = "Evaluating accessory '{Type}' - Confidence: {Confidence:F3}, Box: ({X:F1},{Y:F1},{W:F1},{H:F1})")]
    public static partial void LogAccessoryEvaluation(
        this ILogger logger,
        string type,
        float confidence,
        float x,
        float y,
        float w,
        float h);

    [LoggerMessage(
        EventId = 132,
        Level = LogLevel.Debug,
        Message = "IoU calculation - Intersection: {Intersection:F2}, Union: {Union:F2}, IoU: {IoU:F4}")]
    public static partial void LogIoUCalculation(
        this ILogger logger,
        float intersection,
        float union,
        float iou);

    [LoggerMessage(
        EventId = 133,
        Level = LogLevel.Debug,
        Message = "Extended bounds check - Accessory center: ({CenterX:F1},{CenterY:F1}), Extended box: ({X:F1},{Y:F1},{W:F1},{H:F1}), Within: {Within}")]
    public static partial void LogExtendedBoundsCheck(
        this ILogger logger,
        float centerX,
        float centerY,
        float x,
        float y,
        float w,
        float h,
        bool within);

    [LoggerMessage(
        EventId = 134,
        Level = LogLevel.Information,
        Message = "Accessory '{Type}' association result: {Associated} - Reason: {Reason}")]
    public static partial void LogAccessoryAssociationResult(
        this ILogger logger,
        string type,
        bool associated,
        string reason);

    [LoggerMessage(
        EventId = 135,
        Level = LogLevel.Information,
        Message = "DetectAccessoriesFromYolo complete - Associated: {AccessoryCount} accessories, {ClothingCount} clothing items")]
    public static partial void LogAccessoryDetectionComplete(
        this ILogger logger,
        int accessoryCount,
        int clothingCount);

    [LoggerMessage(
        EventId = 136,
        Level = LogLevel.Debug,
        Message = "MatchesCriteria - Input: SearchClothing[{SearchClothingCount}], SearchAccessories[{SearchAccessoriesCount}], DetectedClothing[{DetectedClothingCount}], DetectedAccessories[{DetectedAccessoriesCount}]")]
    public static partial void LogMatchesCriteriaInput(
        this ILogger logger,
        int searchClothingCount,
        int searchAccessoriesCount,
        int detectedClothingCount,
        int detectedAccessoriesCount);

    [LoggerMessage(
        EventId = 137,
        Level = LogLevel.Debug,
        Message = "Comparing search item '{SearchItem}' with detected items: {DetectedItems}")]
    public static partial void LogItemComparison(
        this ILogger logger,
        string searchItem,
        string detectedItems);

    [LoggerMessage(
        EventId = 138,
        Level = LogLevel.Information,
        Message = "MatchesCriteria result: {Matches} - Found match for: '{MatchedItem}'")]
    public static partial void LogMatchesCriteriaResult(
        this ILogger logger,
        bool matches,
        string matchedItem);
}
