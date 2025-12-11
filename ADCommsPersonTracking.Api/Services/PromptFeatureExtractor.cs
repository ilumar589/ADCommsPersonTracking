using ADCommsPersonTracking.Api.Models;
using System.Text.RegularExpressions;

namespace ADCommsPersonTracking.Api.Services;

public class PromptFeatureExtractor : IPromptFeatureExtractor
{
    private readonly ILogger<PromptFeatureExtractor> _logger;

    // Known color keywords
    private static readonly HashSet<string> ColorKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "red", "blue", "green", "yellow", "black", "white", "orange", "purple", 
        "pink", "brown", "gray", "grey", "navy", "maroon", "cyan", "magenta",
        "beige", "tan", "cream", "silver", "gold", "khaki", "olive"
    };

    // Known clothing item keywords
    private static readonly HashSet<string> ClothingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "jacket", "coat", "shirt", "t-shirt", "tshirt", "pants", "jeans", 
        "trousers", "dress", "skirt", "hat", "cap", "shoes", "boots", "sneakers",
        "sweater", "hoodie", "blazer", "suit", "tie", "scarf", "gloves",
        "shorts", "sandals", "vest", "cardigan", "sweatshirt"
    };

    // Known accessory keywords
    private static readonly HashSet<string> AccessoryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "bag", "backpack", "suitcase", "luggage", "umbrella", "glasses", 
        "sunglasses", "watch", "briefcase", "handbag", "purse", "wallet",
        "phone", "laptop", "headphones", "earbuds", "necklace", "bracelet"
    };

    // Known physical attribute keywords
    private static readonly HashSet<string> PhysicalAttributeKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "tall", "short", "slim", "thin", "heavy", "large", "small", 
        "muscular", "athletic", "stocky", "petite", "lanky"
    };

    public PromptFeatureExtractor(ILogger<PromptFeatureExtractor> logger)
    {
        _logger = logger;
    }

    public SearchCriteria ExtractFeatures(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return new SearchCriteria();
        }

        var criteria = new SearchCriteria();
        var lowerPrompt = prompt.ToLowerInvariant();

        // Extract colors
        criteria.Colors = ExtractKeywords(lowerPrompt, ColorKeywords);

        // Extract clothing items
        criteria.ClothingItems = ExtractKeywords(lowerPrompt, ClothingKeywords);

        // Extract accessories
        criteria.Accessories = ExtractKeywords(lowerPrompt, AccessoryKeywords);

        // Extract physical attributes
        criteria.PhysicalAttributes = ExtractKeywords(lowerPrompt, PhysicalAttributeKeywords);

        // Extract height
        criteria.Height = ExtractHeight(prompt);

        _logger.LogDebug("Extracted features from prompt - Colors: {Colors}, Clothing: {Clothing}, Accessories: {Accessories}, Height: {Height}",
            string.Join(", ", criteria.Colors),
            string.Join(", ", criteria.ClothingItems),
            string.Join(", ", criteria.Accessories),
            criteria.Height?.Meters.ToString() ?? "none");

        return criteria;
    }

    private List<string> ExtractKeywords(string prompt, HashSet<string> keywords)
    {
        var found = new List<string>();
        
        foreach (var keyword in keywords)
        {
            // Use word boundaries to avoid partial matches
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            if (Regex.IsMatch(prompt, pattern, RegexOptions.IgnoreCase))
            {
                found.Add(keyword.ToLowerInvariant());
            }
        }

        return found.Distinct().ToList();
    }

    private HeightInfo? ExtractHeight(string prompt)
    {
        // Pattern for meters: "1.73 m", "1.73m", "1.73 meters"
        var metersPattern = @"(\d+\.?\d*)\s*(?:m\b|meters?\b)";
        var metersMatch = Regex.Match(prompt, metersPattern, RegexOptions.IgnoreCase);
        if (metersMatch.Success && float.TryParse(metersMatch.Groups[1].Value, out float meters))
        {
            return new HeightInfo
            {
                Meters = meters,
                OriginalText = metersMatch.Value
            };
        }

        // Pattern for centimeters: "173 cm", "173cm", "173 centimeters"
        var cmPattern = @"(\d+)\s*(?:cm\b|centimeters?\b)";
        var cmMatch = Regex.Match(prompt, cmPattern, RegexOptions.IgnoreCase);
        if (cmMatch.Success && int.TryParse(cmMatch.Groups[1].Value, out int cm))
        {
            return new HeightInfo
            {
                Meters = cm / 100f,
                OriginalText = cmMatch.Value
            };
        }

        // Pattern for feet and inches: "5'8\"", "5' 8\"", "5 feet 8 inches"
        var feetInchesPattern = @"(\d+)\s*(?:feet|ft|')\s*(\d+)\s*(?:inches|in|"")?";
        var feetInchesMatch = Regex.Match(prompt, feetInchesPattern, RegexOptions.IgnoreCase);
        if (feetInchesMatch.Success && 
            int.TryParse(feetInchesMatch.Groups[1].Value, out int feet) &&
            int.TryParse(feetInchesMatch.Groups[2].Value, out int inches))
        {
            var totalInches = feet * 12 + inches;
            var metersFromFeet = totalInches * 0.0254f;
            return new HeightInfo
            {
                Meters = metersFromFeet,
                OriginalText = feetInchesMatch.Value
            };
        }

        // Pattern for feet only: "5'", "5 feet", "5ft"
        var feetOnlyPattern = @"(\d+)\s*(?:feet|ft|')";
        var feetOnlyMatch = Regex.Match(prompt, feetOnlyPattern, RegexOptions.IgnoreCase);
        if (feetOnlyMatch.Success && int.TryParse(feetOnlyMatch.Groups[1].Value, out int feetOnly))
        {
            var metersFromFeetOnly = feetOnly * 0.3048f;
            return new HeightInfo
            {
                Meters = metersFromFeetOnly,
                OriginalText = feetOnlyMatch.Value
            };
        }

        return null;
    }
}
