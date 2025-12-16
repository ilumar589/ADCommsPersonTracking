using ADCommsPersonTracking.Api.Models;

namespace ADCommsPersonTracking.Api.Services;

public interface IColorAnalysisService
{
    Task<PersonColorProfile> AnalyzePersonColorsAsync(byte[] imageBytes, BoundingBox personBox);
    bool MatchesColorCriteria(PersonColorProfile profile, List<string> searchColors);
    
    /// <summary>
    /// Analyze colors specifically on a clothing region's bounding box.
    /// </summary>
    /// <param name="imageBytes">The full image bytes</param>
    /// <param name="clothingBox">The bounding box of the clothing item</param>
    /// <returns>List of detected colors on the clothing region</returns>
    Task<List<DetectedColor>> AnalyzeClothingRegionColorsAsync(byte[] imageBytes, BoundingBox clothingBox);
    
    /// <summary>
    /// Check if a clothing item matches a color+clothing query (e.g., "blue jacket").
    /// </summary>
    /// <param name="detectedClothing">List of detected clothing items with their colors</param>
    /// <param name="searchColor">The color to search for (e.g., "blue")</param>
    /// <param name="searchClothingType">The clothing type to search for (e.g., "jacket")</param>
    /// <returns>True if a match is found, false otherwise</returns>
    bool MatchesColoredClothingCriteria(List<ClothingWithColors> detectedClothing, string searchColor, string searchClothingType);
}
