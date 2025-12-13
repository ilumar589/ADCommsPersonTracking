using ADCommsPersonTracking.Api.Logging;
using ADCommsPersonTracking.Api.Models;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace ADCommsPersonTracking.Api.Services;

public partial class PromptFeatureExtractor : IPromptFeatureExtractor
{
    private readonly ILogger<PromptFeatureExtractor> _logger;

    // Known color keywords - using FrozenSet for optimized lookups
    private static readonly FrozenSet<string> ColorKeywords = new[]
    {
        "red", "blue", "green", "yellow", "black", "white", "orange", "purple", 
        "pink", "brown", "gray", "grey", "navy", "maroon", "cyan", "magenta",
        "beige", "tan", "cream", "silver", "gold", "khaki", "olive",
        // Additional shades
        "crimson", "scarlet", "burgundy", "coral", "salmon", "peach", "amber", "mustard",
        "lime", "teal", "turquoise", "aqua", "indigo", "violet", "lavender", "plum",
        "rose", "blush", "charcoal", "slate", "ivory", "pearl", "bronze", "copper",
        "rust", "wine", "forest", "emerald", "sapphire", "ruby", "jade", "mint",
        "sky", "midnight", "cobalt", "periwinkle", "fuchsia", "mauve", "taupe", "mocha",
        "espresso", "caramel", "honey", "lemon", "tangerine", "apricot", "champagne",
        "chocolate", "coffee", "denim", "eggplant", "flamingo", "grape", "hunter",
        "mahogany", "moss", "mulberry", "nude", "oatmeal", "paprika", "pewter",
        "pistachio", "pumpkin", "raspberry", "sage", "sand", "seafoam", "sepia",
        "sienna", "smoky", "steel", "stone", "strawberry", "sunflower", "terracotta",
        "thistle", "tomato", "vanilla", "walnut", "watermelon", "wheat", "wisteria"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Known clothing item keywords - using FrozenSet for optimized lookups
    private static readonly FrozenSet<string> ClothingKeywords = new[]
    {
        "jacket", "coat", "shirt", "t-shirt", "tshirt", "pants", "jeans", 
        "trousers", "dress", "skirt", "hat", "cap", "shoes", "boots", "sneakers",
        "sweater", "hoodie", "blazer", "suit", "tie", "scarf", "gloves",
        "shorts", "sandals", "vest", "cardigan", "sweatshirt",
        // Tops
        "blouse", "tank top", "polo", "tunic", "camisole", "crop top", "bodysuit",
        "turtleneck", "henley", "flannel", "jersey", "pullover",
        // Bottoms
        "leggings", "capris", "culottes", "joggers", "chinos", "slacks", "overalls",
        "dungarees", "cargo pants",
        // Outerwear
        "parka", "windbreaker", "raincoat", "trench", "poncho", "cape", "anorak",
        "fleece", "bomber", "duster", "peacoat", "puffer",
        // Footwear
        "loafers", "heels", "flats", "mules", "clogs", "oxfords", "brogues",
        "espadrilles", "wedges", "platforms", "slippers", "flip-flops", "trainers",
        // Formal
        "tuxedo", "gown", "cocktail dress", "evening dress", "formal wear",
        // Casual
        "jumpsuit", "romper", "playsuit", "kaftan", "kimono", "robe",
        // Athletic
        "uniform", "tracksuit", "leotard", "wetsuit", "swimsuit", "bikini", "trunks"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Known accessory keywords - using FrozenSet for optimized lookups
    private static readonly FrozenSet<string> AccessoryKeywords = new[]
    {
        "bag", "backpack", "suitcase", "luggage", "umbrella", "glasses", 
        "sunglasses", "watch", "briefcase", "handbag", "purse", "wallet",
        "phone", "laptop", "headphones", "earbuds", "necklace", "bracelet",
        // Bags
        "tote", "clutch", "messenger bag", "duffel", "fanny pack", "crossbody",
        "shoulder bag", "hobo bag", "bucket bag", "weekender",
        // Eyewear
        "spectacles", "reading glasses", "goggles", "monocle", "contact lenses",
        // Jewelry
        "ring", "earrings", "pendant", "chain", "anklet", "brooch", "cufflinks",
        "tie clip", "hair clip", "barrette",
        // Headwear
        "beanie", "beret", "fedora", "visor", "bandana", "headband", "turban",
        "hijab", "headscarf", "earmuffs",
        // Other
        "belt", "suspenders", "cane", "walking stick", "wheelchair", "crutches",
        "stroller", "shopping bag", "grocery bag", "plastic bag", "paper bag",
        "box", "package", "parcel", "envelope", "folder", "clipboard", "tablet",
        "camera", "binoculars", "tripod", "microphone", "musical instrument",
        "guitar", "violin", "skateboard", "scooter", "bicycle", "helmet",
        "knee pads", "elbow pads"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // Known physical attribute keywords - using FrozenSet for optimized lookups
    private static readonly FrozenSet<string> PhysicalAttributeKeywords = new[]
    {
        "tall", "short", "slim", "thin", "heavy", "large", "small", 
        "muscular", "athletic", "stocky", "petite", "lanky",
        // Build
        "slender", "lean", "fit", "buff", "bulky", "husky", "plump", "chubby",
        "overweight", "underweight", "medium build", "average build", "well-built",
        "broad-shouldered", "narrow-shouldered",
        // Height descriptors
        "very tall", "extremely tall", "above average height", "below average height",
        "very short", "medium height",
        // Age-related
        "young", "old", "elderly", "middle-aged", "teenage", "adolescent", "child",
        "adult", "senior", "youth",
        // Hair
        "bald", "balding", "receding hairline", "long hair", "short hair", "curly hair",
        "straight hair", "wavy hair", "braided", "ponytail", "bun", "dreadlocks",
        "mohawk", "afro", "buzz cut", "crew cut", "bob", "pixie cut", "mullet",
        "blonde", "brunette", "redhead", "gray-haired", "white-haired", "black-haired",
        // Facial
        "bearded", "clean-shaven", "mustache", "goatee", "stubble", "sideburns",
        "glasses-wearing", "freckled",
        // Skin tone
        "fair", "pale", "light-skinned", "dark-skinned", "tanned", "olive"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

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

        _logger.LogPromptFeatureExtractionStart(prompt);

        var criteria = new SearchCriteria();
        var lowerPrompt = prompt.ToLowerInvariant();

        // Extract colors
        criteria.Colors = ExtractKeywords(lowerPrompt, ColorKeywords);
        _logger.LogColorExtractionResult(criteria.Colors.Count);
        foreach (var color in criteria.Colors)
        {
            _logger.LogColorKeywordFound(color);
        }

        // Extract clothing items
        criteria.ClothingItems = ExtractKeywords(lowerPrompt, ClothingKeywords);
        _logger.LogClothingExtractionResult(criteria.ClothingItems.Count);
        foreach (var clothing in criteria.ClothingItems)
        {
            _logger.LogClothingKeywordFound(clothing);
        }

        // Extract accessories
        criteria.Accessories = ExtractKeywords(lowerPrompt, AccessoryKeywords);
        _logger.LogAccessoryExtractionResult(criteria.Accessories.Count);
        foreach (var accessory in criteria.Accessories)
        {
            _logger.LogAccessoryKeywordFound(accessory);
        }

        // Extract physical attributes
        criteria.PhysicalAttributes = ExtractKeywords(lowerPrompt, PhysicalAttributeKeywords);
        _logger.LogPhysicalAttributeExtractionResult(criteria.PhysicalAttributes.Count);
        foreach (var attribute in criteria.PhysicalAttributes)
        {
            _logger.LogPhysicalAttributeKeywordFound(attribute);
        }

        // Extract height
        criteria.Height = ExtractHeight(prompt);

        // Check if any criteria were found
        var hasCriteria = criteria.Colors.Count > 0 || 
                         criteria.ClothingItems.Count > 0 || 
                         criteria.Accessories.Count > 0 || 
                         criteria.PhysicalAttributes.Count > 0 ||
                         criteria.Height != null;

        if (!hasCriteria)
        {
            _logger.LogWarning("No specific search criteria found in prompt: '{Prompt}'. The prompt did not contain recognized colors, clothing, accessories, physical attributes, or height information.", prompt);
        }

        // Log final search criteria
        _logger.LogFinalSearchCriteria(
            string.Join(", ", criteria.Colors),
            string.Join(", ", criteria.ClothingItems),
            string.Join(", ", criteria.Accessories),
            string.Join(", ", criteria.PhysicalAttributes),
            criteria.Height?.OriginalText ?? "none");

        // Legacy logging for backward compatibility
        _logger.LogExtractedFeatures(
            string.Join(", ", criteria.Colors),
            string.Join(", ", criteria.ClothingItems),
            string.Join(", ", criteria.Accessories),
            criteria.Height?.Meters.ToString() ?? "none");

        return criteria;
    }

    private List<string> ExtractKeywords(string prompt, FrozenSet<string> keywords)
    {
        var found = new List<string>();
        var promptSpan = prompt.AsSpan();
        
        foreach (var keyword in keywords)
        {
            // Use span-based search with word boundary checking
            if (ContainsWordSpan(promptSpan, keyword))
            {
                found.Add(keyword.ToLowerInvariant());
            }
        }

        return found.Distinct().ToList();
    }

    private static bool ContainsWordSpan(ReadOnlySpan<char> text, string word)
    {
        var wordSpan = word.AsSpan();
        var startIndex = 0;
        
        while (startIndex <= text.Length - wordSpan.Length)
        {
            var index = text[startIndex..].IndexOf(wordSpan, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }
            
            index += startIndex;
            
            // Check if it's a word boundary before
            var isStartBoundary = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            
            // Check if it's a word boundary after
            var endIndex = index + wordSpan.Length;
            var isEndBoundary = endIndex >= text.Length || !char.IsLetterOrDigit(text[endIndex]);
            
            if (isStartBoundary && isEndBoundary)
            {
                return true;
            }
            
            startIndex = index + 1;
        }
        
        return false;
    }

    // Source-generated regex patterns for height extraction
    [GeneratedRegex(@"(\d+\.?\d*)\s*(?:m\b|meters?\b)", RegexOptions.IgnoreCase)]
    private static partial Regex MetersPattern();

    [GeneratedRegex(@"(\d+)\s*(?:cm\b|centimeters?\b)", RegexOptions.IgnoreCase)]
    private static partial Regex CentimetersPattern();

    [GeneratedRegex(@"(\d+)\s*(?:feet|ft|')\s*(\d+)\s*(?:inches|in|"")?", RegexOptions.IgnoreCase)]
    private static partial Regex FeetInchesPattern();

    [GeneratedRegex(@"(\d+)\s*(?:feet|ft|')", RegexOptions.IgnoreCase)]
    private static partial Regex FeetOnlyPattern();

    private HeightInfo? ExtractHeight(string prompt)
    {
        // Pattern for meters: "1.73 m", "1.73m", "1.73 meters"
        var metersMatch = MetersPattern().Match(prompt);
        if (metersMatch.Success && float.TryParse(metersMatch.Groups[1].Value, out float meters))
        {
            return new HeightInfo(meters, metersMatch.Value);
        }

        // Pattern for centimeters: "173 cm", "173cm", "173 centimeters"
        var cmMatch = CentimetersPattern().Match(prompt);
        if (cmMatch.Success && int.TryParse(cmMatch.Groups[1].Value, out int cm))
        {
            return new HeightInfo(cm / 100f, cmMatch.Value);
        }

        // Pattern for feet and inches: "5'8\"", "5' 8\"", "5 feet 8 inches"
        var feetInchesMatch = FeetInchesPattern().Match(prompt);
        if (feetInchesMatch.Success && 
            int.TryParse(feetInchesMatch.Groups[1].Value, out int feet) &&
            int.TryParse(feetInchesMatch.Groups[2].Value, out int inches))
        {
            var totalInches = feet * 12 + inches;
            var metersFromFeet = totalInches * 0.0254f;
            return new HeightInfo(metersFromFeet, feetInchesMatch.Value);
        }

        // Pattern for feet only: "5'", "5 feet", "5ft"
        var feetOnlyMatch = FeetOnlyPattern().Match(prompt);
        if (feetOnlyMatch.Success && int.TryParse(feetOnlyMatch.Groups[1].Value, out int feetOnly))
        {
            var metersFromFeetOnly = feetOnly * 0.3048f;
            return new HeightInfo(metersFromFeetOnly, feetOnlyMatch.Value);
        }

        return null;
    }
}
