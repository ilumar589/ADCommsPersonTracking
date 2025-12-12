using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace ADCommsPersonTracking.Api.Helpers;

/// <summary>
/// Provides SIMD-accelerated mathematical operations with scalar fallbacks.
/// </summary>
public static class SimdMath
{
    /// <summary>
    /// Calculate Euclidean distance between two 2D points using SIMD when available.
    /// </summary>
    public static float CalculateDistance(float x1, float y1, float x2, float y2)
    {
        if (Sse.IsSupported)
        {
            return CalculateDistanceSSE(x1, y1, x2, y2);
        }
        else
        {
            return CalculateDistanceScalar(x1, y1, x2, y2);
        }
    }

    /// <summary>
    /// Calculate color distance using SIMD when available.
    /// </summary>
    public static double CalculateColorDistance(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        if (Sse2.IsSupported)
        {
            return CalculateColorDistanceSSE2(r1, g1, b1, r2, g2, b2);
        }
        else
        {
            return CalculateColorDistanceScalar(r1, g1, b1, r2, g2, b2);
        }
    }

    /// <summary>
    /// Calculate IoU (Intersection over Union) for two bounding boxes using SIMD when available.
    /// </summary>
    public static float CalculateIoU(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        if (Sse.IsSupported)
        {
            return CalculateIoUSSE(x1, y1, w1, h1, x2, y2, w2, h2);
        }
        else
        {
            return CalculateIoUScalar(x1, y1, w1, h1, x2, y2, w2, h2);
        }
    }

    /// <summary>
    /// Batch distance calculation for multiple points using SIMD.
    /// </summary>
    public static void CalculateDistances(ReadOnlySpan<float> x1, ReadOnlySpan<float> y1, 
        ReadOnlySpan<float> x2, ReadOnlySpan<float> y2, Span<float> results)
    {
        if (x1.Length != y1.Length || x1.Length != x2.Length || x1.Length != y2.Length || x1.Length != results.Length)
        {
            throw new ArgumentException("All input spans must have the same length");
        }

        if (Avx.IsSupported && x1.Length >= 8)
        {
            CalculateDistancesAVX(x1, y1, x2, y2, results);
        }
        else if (Sse.IsSupported && x1.Length >= 4)
        {
            CalculateDistancesSSE(x1, y1, x2, y2, results);
        }
        else
        {
            CalculateDistancesScalar(x1, y1, x2, y2, results);
        }
    }

    #region SSE Implementations

    private static float CalculateDistanceSSE(float x1, float y1, float x2, float y2)
    {
        // Load values into SSE registers (4 floats at a time)
        var v1 = Vector128.Create(x1, y1, 0f, 0f);
        var v2 = Vector128.Create(x2, y2, 0f, 0f);
        
        // Calculate difference
        var diff = Sse.Subtract(v1, v2);
        
        // Square the differences
        var squared = Sse.Multiply(diff, diff);
        
        // Sum the first two elements (x and y differences)
        var temp = Sse.MoveHighToLow(squared, squared);
        var sum = Sse.Add(squared, temp);
        
        // Calculate square root
        var result = Sse.SqrtScalar(sum);
        
        return result.ToScalar();
    }

    private static double CalculateColorDistanceSSE2(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        // Use scalar calculation for color distance since SSE2 integer ops are complex
        // The performance gain from SIMD for 3 values is minimal anyway
        var rDiff = (int)r1 - (int)r2;
        var gDiff = (int)g1 - (int)g2;
        var bDiff = (int)b1 - (int)b2;
        return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
    }

    private static float CalculateIoUSSE(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        // Calculate box coordinates using SSE
        var box1Min = Vector128.Create(x1, y1, 0f, 0f);
        var box1Size = Vector128.Create(w1, h1, 0f, 0f);
        var box1Max = Sse.Add(box1Min, box1Size);

        var box2Min = Vector128.Create(x2, y2, 0f, 0f);
        var box2Size = Vector128.Create(w2, h2, 0f, 0f);
        var box2Max = Sse.Add(box2Min, box2Size);

        // Calculate intersection
        var interMin = Sse.Max(box1Min, box2Min);
        var interMax = Sse.Min(box1Max, box2Max);
        var interSize = Sse.Max(Sse.Subtract(interMax, interMin), Vector128<float>.Zero);

        var interWidth = interSize.GetElement(0);
        var interHeight = interSize.GetElement(1);
        var intersection = interWidth * interHeight;

        // Calculate union
        var area1 = w1 * h1;
        var area2 = w2 * h2;
        var union = area1 + area2 - intersection;

        return union > 0 ? intersection / union : 0f;
    }

    private static void CalculateDistancesSSE(ReadOnlySpan<float> x1, ReadOnlySpan<float> y1,
        ReadOnlySpan<float> x2, ReadOnlySpan<float> y2, Span<float> results)
    {
        int i = 0;
        int vectorSize = Vector128<float>.Count;
        
        // Process 4 elements at a time with SSE
        for (; i <= x1.Length - vectorSize; i += vectorSize)
        {
            var vx1 = Vector128.Create(x1[i], x1[i + 1], x1[i + 2], x1[i + 3]);
            var vy1 = Vector128.Create(y1[i], y1[i + 1], y1[i + 2], y1[i + 3]);
            var vx2 = Vector128.Create(x2[i], x2[i + 1], x2[i + 2], x2[i + 3]);
            var vy2 = Vector128.Create(y2[i], y2[i + 1], y2[i + 2], y2[i + 3]);

            var dx = Sse.Subtract(vx1, vx2);
            var dy = Sse.Subtract(vy1, vy2);

            var dx2 = Sse.Multiply(dx, dx);
            var dy2 = Sse.Multiply(dy, dy);

            var sum = Sse.Add(dx2, dy2);
            var dist = Sse.Sqrt(sum);

            for (int j = 0; j < vectorSize; j++)
            {
                results[i + j] = dist.GetElement(j);
            }
        }

        // Process remaining elements
        for (; i < x1.Length; i++)
        {
            results[i] = CalculateDistanceScalar(x1[i], y1[i], x2[i], y2[i]);
        }
    }

    #endregion

    #region AVX Implementations

    private static void CalculateDistancesAVX(ReadOnlySpan<float> x1, ReadOnlySpan<float> y1,
        ReadOnlySpan<float> x2, ReadOnlySpan<float> y2, Span<float> results)
    {
        int i = 0;
        int vectorSize = Vector256<float>.Count;

        // Process 8 elements at a time with AVX
        for (; i <= x1.Length - vectorSize; i += vectorSize)
        {
            var vx1 = Vector256.Create(x1.Slice(i, vectorSize));
            var vy1 = Vector256.Create(y1.Slice(i, vectorSize));
            var vx2 = Vector256.Create(x2.Slice(i, vectorSize));
            var vy2 = Vector256.Create(y2.Slice(i, vectorSize));

            var dx = Avx.Subtract(vx1, vx2);
            var dy = Avx.Subtract(vy1, vy2);

            var dx2 = Avx.Multiply(dx, dx);
            var dy2 = Avx.Multiply(dy, dy);

            var sum = Avx.Add(dx2, dy2);
            var dist = Avx.Sqrt(sum);

            for (int j = 0; j < vectorSize; j++)
            {
                results[i + j] = dist.GetElement(j);
            }
        }

        // Process remaining elements with scalar
        for (; i < x1.Length; i++)
        {
            results[i] = CalculateDistanceScalar(x1[i], y1[i], x2[i], y2[i]);
        }
    }

    #endregion

    #region Scalar Fallback Implementations

    private static float CalculateDistanceScalar(float x1, float y1, float x2, float y2)
    {
        var dx = x1 - x2;
        var dy = y1 - y2;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    private static double CalculateColorDistanceScalar(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
    {
        var rDiff = r1 - r2;
        var gDiff = g1 - g2;
        var bDiff = b1 - b2;
        return Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);
    }

    private static float CalculateIoUScalar(float x1, float y1, float w1, float h1, float x2, float y2, float w2, float h2)
    {
        // Calculate box coordinates
        var x1Min = x1;
        var y1Min = y1;
        var x1Max = x1 + w1;
        var y1Max = y1 + h1;

        var x2Min = x2;
        var y2Min = y2;
        var x2Max = x2 + w2;
        var y2Max = y2 + h2;

        // Calculate intersection
        var interXMin = Math.Max(x1Min, x2Min);
        var interYMin = Math.Max(y1Min, y2Min);
        var interXMax = Math.Min(x1Max, x2Max);
        var interYMax = Math.Min(y1Max, y2Max);

        var interWidth = Math.Max(0f, interXMax - interXMin);
        var interHeight = Math.Max(0f, interYMax - interYMin);
        var intersection = interWidth * interHeight;

        // Calculate union
        var area1 = w1 * h1;
        var area2 = w2 * h2;
        var union = area1 + area2 - intersection;

        return union > 0 ? intersection / union : 0f;
    }

    private static void CalculateDistancesScalar(ReadOnlySpan<float> x1, ReadOnlySpan<float> y1,
        ReadOnlySpan<float> x2, ReadOnlySpan<float> y2, Span<float> results)
    {
        for (int i = 0; i < x1.Length; i++)
        {
            results[i] = CalculateDistanceScalar(x1[i], y1[i], x2[i], y2[i]);
        }
    }

    #endregion
}
