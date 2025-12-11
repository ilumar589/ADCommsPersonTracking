using ADCommsPersonTracking.Api.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace ADCommsPersonTracking.Api.Services;

public class ImageAnnotationService : IImageAnnotationService
{
    private readonly ILogger<ImageAnnotationService> _logger;
    private readonly Color _boxColor;
    private readonly int _boxThickness;
    private readonly bool _showLabels;
    private readonly float _fontSize;

    public ImageAnnotationService(IConfiguration configuration, ILogger<ImageAnnotationService> logger)
    {
        _logger = logger;
        
        // Read configuration with defaults
        var boxColorHex = configuration["ImageAnnotation:BoxColor"] ?? "#00FF00";
        _boxThickness = int.TryParse(configuration["ImageAnnotation:BoxThickness"], out var thickness) ? thickness : 2;
        _showLabels = bool.TryParse(configuration["ImageAnnotation:ShowLabels"], out var showLabels) ? showLabels : true;
        _fontSize = float.TryParse(configuration["ImageAnnotation:FontSize"], out var fontSize) ? fontSize : 12f;
        
        // Parse hex color
        _boxColor = ParseHexColor(boxColorHex);
    }

    public async Task<string> AnnotateImageAsync(byte[] imageBytes, List<BoundingBox> boundingBoxes)
    {
        try
        {
            using var image = Image.Load<Rgba32>(imageBytes);
            
            // Draw bounding boxes
            foreach (var box in boundingBoxes)
            {
                DrawBoundingBox(image, box);
            }

            // Convert to base64
            using var memoryStream = new MemoryStream();
            await image.SaveAsJpegAsync(memoryStream);
            var annotatedBytes = memoryStream.ToArray();
            var base64 = Convert.ToBase64String(annotatedBytes);

            _logger.LogDebug("Annotated image with {Count} bounding boxes", boundingBoxes.Count);
            return base64;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error annotating image");
            throw;
        }
    }

    private void DrawBoundingBox(Image<Rgba32> image, BoundingBox box)
    {
        // Calculate rectangle coordinates
        var rect = new RectangleF(box.X, box.Y, box.Width, box.Height);

        // Draw the rectangle
        image.Mutate(ctx =>
        {
            ctx.Draw(_boxColor, _boxThickness, rect);

            // Draw label if enabled
            if (_showLabels)
            {
                var label = $"{box.Label} {box.Confidence:F2}";
                DrawLabel(ctx, label, box.X, box.Y);
            }
        });
    }

    private void DrawLabel(IImageProcessingContext ctx, string text, float x, float y)
    {
        try
        {
            // Try to use a system font, fallback to default if not available
            Font font;
            try
            {
                var fontCollection = new FontCollection();
                var fontFamily = SystemFonts.CreateFont("Arial", _fontSize, FontStyle.Bold);
                font = fontFamily;
            }
            catch
            {
                // Fallback: use default font
                font = SystemFonts.CreateFont(SystemFonts.Families.First().Name, _fontSize, FontStyle.Bold);
            }

            // Position label above the bounding box
            var labelY = Math.Max(0, y - _fontSize - 4);
            var textPosition = new PointF(x, labelY);

            // Draw text background
            var textOptions = new RichTextOptions(font)
            {
                Origin = textPosition
            };

            var textBounds = TextMeasurer.MeasureBounds(text, textOptions);
            var backgroundRect = new RectangleF(
                textBounds.X - 2, 
                textBounds.Y - 2, 
                textBounds.Width + 4, 
                textBounds.Height + 4);

            ctx.Fill(Color.Black.WithAlpha(0.7f), backgroundRect);

            // Draw text
            ctx.DrawText(text, font, _boxColor, textPosition);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not draw label, fonts may not be available");
            // Continue without labels if font rendering fails
        }
    }

    private Color ParseHexColor(string hex)
    {
        try
        {
            // Remove # if present
            hex = hex.TrimStart('#');

            // Parse RGB
            if (hex.Length == 6)
            {
                var r = Convert.ToByte(hex.Substring(0, 2), 16);
                var g = Convert.ToByte(hex.Substring(2, 2), 16);
                var b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not parse color {Hex}, using default green", hex);
        }

        // Default to green
        return Color.Green;
    }
}
