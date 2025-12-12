using ADCommsPersonTracking.Api.Helpers;
using FluentAssertions;

namespace ADCommsPersonTracking.Tests.Helpers;

public class SimdMathTests
{
    [Fact]
    public void CalculateDistance_ShouldReturnCorrectDistance()
    {
        // Arrange
        float x1 = 100f, y1 = 100f;
        float x2 = 103f, y2 = 104f;
        var expectedDistance = Math.Sqrt(3 * 3 + 4 * 4); // 5.0

        // Act
        var result = SimdMath.CalculateDistance(x1, y1, x2, y2);

        // Assert
        result.Should().BeApproximately((float)expectedDistance, 0.001f);
    }

    [Fact]
    public void CalculateDistance_WithSamePoint_ShouldReturnZero()
    {
        // Arrange
        float x = 50f, y = 75f;

        // Act
        var result = SimdMath.CalculateDistance(x, y, x, y);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CalculateColorDistance_ShouldReturnCorrectDistance()
    {
        // Arrange
        byte r1 = 255, g1 = 0, b1 = 0;  // Red
        byte r2 = 0, g2 = 255, b2 = 0;  // Green
        var expectedDistance = Math.Sqrt(255 * 255 + 255 * 255); // ~360.62

        // Act
        var result = SimdMath.CalculateColorDistance(r1, g1, b1, r2, g2, b2);

        // Assert
        result.Should().BeApproximately(expectedDistance, 0.01);
    }

    [Fact]
    public void CalculateColorDistance_WithSameColor_ShouldReturnZero()
    {
        // Arrange
        byte r = 128, g = 64, b = 192;

        // Act
        var result = SimdMath.CalculateColorDistance(r, g, b, r, g, b);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void CalculateIoU_WithIdenticalBoxes_ShouldReturnOne()
    {
        // Arrange
        float x = 100f, y = 100f, w = 50f, h = 50f;

        // Act
        var result = SimdMath.CalculateIoU(x, y, w, h, x, y, w, h);

        // Assert
        result.Should().BeApproximately(1.0f, 0.001f);
    }

    [Fact]
    public void CalculateIoU_WithNoOverlap_ShouldReturnZero()
    {
        // Arrange
        float x1 = 0f, y1 = 0f, w1 = 50f, h1 = 50f;
        float x2 = 100f, y2 = 100f, w2 = 50f, h2 = 50f;

        // Act
        var result = SimdMath.CalculateIoU(x1, y1, w1, h1, x2, y2, w2, h2);

        // Assert
        result.Should().Be(0f);
    }

    [Fact]
    public void CalculateIoU_WithPartialOverlap_ShouldReturnCorrectValue()
    {
        // Arrange
        float x1 = 0f, y1 = 0f, w1 = 100f, h1 = 100f;     // Area = 10000
        float x2 = 50f, y2 = 50f, w2 = 100f, h2 = 100f;   // Area = 10000
        // Intersection = 50x50 = 2500
        // Union = 10000 + 10000 - 2500 = 17500
        // IoU = 2500/17500 = 0.142857

        // Act
        var result = SimdMath.CalculateIoU(x1, y1, w1, h1, x2, y2, w2, h2);

        // Assert
        result.Should().BeApproximately(0.142857f, 0.001f);
    }

    [Fact]
    public void CalculateDistances_ShouldCalculateAllDistances()
    {
        // Arrange
        var x1 = new float[] { 0f, 10f, 20f, 30f };
        var y1 = new float[] { 0f, 10f, 20f, 30f };
        var x2 = new float[] { 3f, 13f, 23f, 33f };
        var y2 = new float[] { 4f, 14f, 24f, 34f };
        var results = new float[4];
        var expectedDistance = 5f; // 3^2 + 4^2 = 25, sqrt(25) = 5

        // Act
        SimdMath.CalculateDistances(x1, y1, x2, y2, results);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeApproximately(expectedDistance, 0.001f));
    }

    [Fact]
    public void CalculateDistances_WithEmptyArrays_ShouldNotThrow()
    {
        // Arrange
        var x1 = Array.Empty<float>();
        var y1 = Array.Empty<float>();
        var x2 = Array.Empty<float>();
        var y2 = Array.Empty<float>();
        var results = Array.Empty<float>();

        // Act & Assert
        var act = () => SimdMath.CalculateDistances(x1, y1, x2, y2, results);
        act.Should().NotThrow();
    }

    [Fact]
    public void CalculateDistances_WithMismatchedLengths_ShouldThrow()
    {
        // Arrange
        var x1 = new float[] { 0f, 10f };
        var y1 = new float[] { 0f, 10f };
        var x2 = new float[] { 3f };  // Different length
        var y2 = new float[] { 4f };
        var results = new float[2];

        // Act & Assert
        var act = () => SimdMath.CalculateDistances(x1, y1, x2, y2, results);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    public void CalculateDistances_WithVariousSizes_ShouldWork(int size)
    {
        // Arrange
        var x1 = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var y1 = Enumerable.Range(0, size).Select(i => (float)i).ToArray();
        var x2 = Enumerable.Range(0, size).Select(i => (float)(i + 3)).ToArray();
        var y2 = Enumerable.Range(0, size).Select(i => (float)(i + 4)).ToArray();
        var results = new float[size];
        var expectedDistance = 5f; // 3^2 + 4^2 = 25, sqrt(25) = 5

        // Act
        SimdMath.CalculateDistances(x1, y1, x2, y2, results);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeApproximately(expectedDistance, 0.001f));
    }
}
