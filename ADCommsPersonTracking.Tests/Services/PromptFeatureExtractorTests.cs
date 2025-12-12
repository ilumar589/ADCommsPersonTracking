using ADCommsPersonTracking.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace ADCommsPersonTracking.Tests.Services;

public class PromptFeatureExtractorTests
{
    private readonly PromptFeatureExtractor _extractor;

    public PromptFeatureExtractorTests()
    {
        var logger = Mock.Of<ILogger<PromptFeatureExtractor>>();
        _extractor = new PromptFeatureExtractor(logger);
    }

    [Fact]
    public void ExtractFeatures_WithEmptyPrompt_ReturnsEmptyCriteria()
    {
        // Arrange
        var prompt = "";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Colors.Should().BeEmpty();
        result.ClothingItems.Should().BeEmpty();
        result.Accessories.Should().BeEmpty();
        result.PhysicalAttributes.Should().BeEmpty();
        result.Height.Should().BeNull();
    }

    [Fact]
    public void ExtractFeatures_WithColorKeywords_ExtractsColors()
    {
        // Arrange
        var prompt = "Find a person wearing a green jacket and blue pants";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Colors.Should().Contain("green");
        result.Colors.Should().Contain("blue");
    }

    [Fact]
    public void ExtractFeatures_WithClothingKeywords_ExtractsClothing()
    {
        // Arrange
        var prompt = "Person in a yellow jacket and black hat with a red shirt";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.ClothingItems.Should().Contain("jacket");
        result.ClothingItems.Should().Contain("hat");
        result.ClothingItems.Should().Contain("shirt");
    }

    [Fact]
    public void ExtractFeatures_WithAccessoryKeywords_ExtractsAccessories()
    {
        // Arrange
        var prompt = "Find person carrying a suitcase, backpack, and wearing sunglasses";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Accessories.Should().Contain("suitcase");
        result.Accessories.Should().Contain("backpack");
        result.Accessories.Should().Contain("sunglasses");
    }

    [Fact]
    public void ExtractFeatures_WithHeightInMeters_ParsesHeight()
    {
        // Arrange
        var prompt = "Find a person with height 1.73 m wearing blue jeans";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Height.Should().NotBeNull();
        result.Height.Value.Meters.Should().BeApproximately(1.73f, 0.01f);
    }

    [Fact]
    public void ExtractFeatures_WithHeightInCentimeters_ParsesHeight()
    {
        // Arrange
        var prompt = "Find person 173 cm tall";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Height.Should().NotBeNull();
        result.Height.Value.Meters.Should().BeApproximately(1.73f, 0.01f);
    }

    [Fact]
    public void ExtractFeatures_WithHeightInFeetAndInches_ParsesHeight()
    {
        // Arrange
        var prompt = "Find someone 5 feet 8 inches tall";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Height.Should().NotBeNull();
        result.Height.Value.Meters.Should().BeApproximately(1.727f, 0.01f);
    }

    [Fact]
    public void ExtractFeatures_WithPhysicalAttributes_ExtractsAttributes()
    {
        // Arrange
        var prompt = "Find a tall slim person";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.PhysicalAttributes.Should().Contain("tall");
        result.PhysicalAttributes.Should().Contain("slim");
    }

    [Fact]
    public void ExtractFeatures_WithCombinedFeatures_ExtractsAllFeatures()
    {
        // Arrange
        var prompt = "I am searching for a person with height 1.73 m, wearing a green jacket, black pants, carrying a backpack, tall and slim";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Colors.Should().Contain("green");
        result.Colors.Should().Contain("black");
        result.ClothingItems.Should().Contain("jacket");
        result.ClothingItems.Should().Contain("pants");
        result.Accessories.Should().Contain("backpack");
        result.PhysicalAttributes.Should().Contain("tall");
        result.PhysicalAttributes.Should().Contain("slim");
        result.Height.Should().NotBeNull();
        result.Height.Value.Meters.Should().BeApproximately(1.73f, 0.01f);
    }

    [Fact]
    public void ExtractFeatures_WithDifferentPhrasings_ExtractsFeatures()
    {
        // Test "wearing"
        var result1 = _extractor.ExtractFeatures("wearing a green jacket");
        result1.Colors.Should().Contain("green");
        result1.ClothingItems.Should().Contain("jacket");

        // Test "with"
        var result2 = _extractor.ExtractFeatures("with green jacket");
        result2.Colors.Should().Contain("green");
        result2.ClothingItems.Should().Contain("jacket");

        // Test "in"
        var result3 = _extractor.ExtractFeatures("in green jacket");
        result3.Colors.Should().Contain("green");
        result3.ClothingItems.Should().Contain("jacket");
    }

    [Theory]
    [InlineData("5'8\"", 1.727f)]
    [InlineData("6 feet", 1.829f)]
    [InlineData("180 cm", 1.80f)]
    [InlineData("1.8 m", 1.80f)]
    [InlineData("5 feet 10 inches", 1.778f)]
    public void ExtractFeatures_WithVariousHeightFormats_ParsesCorrectly(string heightString, float expectedMeters)
    {
        // Arrange
        var prompt = $"Find person {heightString} tall";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Height.Should().NotBeNull();
        result.Height.Value.Meters.Should().BeApproximately(expectedMeters, 0.01f);
    }

    [Fact]
    public void ExtractFeatures_IsCaseInsensitive()
    {
        // Arrange
        var prompt1 = "GREEN jacket";
        var prompt2 = "green JACKET";
        var prompt3 = "Green Jacket";

        // Act
        var result1 = _extractor.ExtractFeatures(prompt1);
        var result2 = _extractor.ExtractFeatures(prompt2);
        var result3 = _extractor.ExtractFeatures(prompt3);

        // Assert
        result1.Colors.Should().Contain("green");
        result1.ClothingItems.Should().Contain("jacket");
        result2.Colors.Should().Contain("green");
        result2.ClothingItems.Should().Contain("jacket");
        result3.Colors.Should().Contain("green");
        result3.ClothingItems.Should().Contain("jacket");
    }

    [Fact]
    public void ExtractFeatures_WithNoFeatures_ReturnsEmptyLists()
    {
        // Arrange
        var prompt = "Find a person in the frame";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Colors.Should().BeEmpty();
        result.ClothingItems.Should().BeEmpty();
        result.Accessories.Should().BeEmpty();
        result.PhysicalAttributes.Should().BeEmpty();
        result.Height.Should().BeNull();
    }

    [Fact]
    public void ExtractFeatures_WithNewColorKeywords_ExtractsNewColors()
    {
        // Arrange
        var prompt = "Find person wearing crimson jacket, turquoise pants, and lavender scarf";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Colors.Should().Contain("crimson");
        result.Colors.Should().Contain("turquoise");
        result.Colors.Should().Contain("lavender");
    }

    [Fact]
    public void ExtractFeatures_WithNewClothingKeywords_ExtractsNewClothing()
    {
        // Arrange
        var prompt = "Person in parka, joggers, and loafers with turtleneck";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.ClothingItems.Should().Contain("parka");
        result.ClothingItems.Should().Contain("joggers");
        result.ClothingItems.Should().Contain("loafers");
        result.ClothingItems.Should().Contain("turtleneck");
    }

    [Fact]
    public void ExtractFeatures_WithNewAccessoryKeywords_ExtractsNewAccessories()
    {
        // Arrange
        var prompt = "Person carrying tote bag and duffel, wearing beanie and goggles";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.Accessories.Should().Contain("tote");
        result.Accessories.Should().Contain("duffel");
        result.Accessories.Should().Contain("beanie");
        result.Accessories.Should().Contain("goggles");
    }

    [Fact]
    public void ExtractFeatures_WithNewPhysicalAttributeKeywords_ExtractsNewAttributes()
    {
        // Arrange
        var prompt = "Find elderly person with gray-haired, bearded, and medium build";

        // Act
        var result = _extractor.ExtractFeatures(prompt);

        // Assert
        result.PhysicalAttributes.Should().Contain("elderly");
        result.PhysicalAttributes.Should().Contain("gray-haired");
        result.PhysicalAttributes.Should().Contain("bearded");
        result.PhysicalAttributes.Should().Contain("medium build");
    }
}
