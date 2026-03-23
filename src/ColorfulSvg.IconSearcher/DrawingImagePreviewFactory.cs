using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorfulSvg.IconSearcher;

internal static class DrawingImagePreviewFactory
{
    public static ImageSource CreatePreviewImage(DrawingImage sourceImage)
    {
        ArgumentNullException.ThrowIfNull(sourceImage);

        if (sourceImage.Drawing is null)
        {
            return sourceImage;
        }

        var visibleBounds = GetVisibleBounds(sourceImage.Drawing);
        if (!IsValidBounds(visibleBounds))
        {
            return sourceImage;
        }

        const double canvasSize = 256d;
        const double padding = 18d;
        var availableSize = canvasSize - (padding * 2d);
        var scale = Math.Min(availableSize / visibleBounds.Width, availableSize / visibleBounds.Height);
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0)
        {
            return sourceImage;
        }

        var scaledWidth = visibleBounds.Width * scale;
        var scaledHeight = visibleBounds.Height * scale;
        var offsetX = (canvasSize - scaledWidth) / 2d;
        var offsetY = (canvasSize - scaledHeight) / 2d;

        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.PushTransform(new TranslateTransform(offsetX, offsetY));
            drawingContext.PushTransform(new ScaleTransform(scale, scale));
            drawingContext.PushTransform(new TranslateTransform(-visibleBounds.X, -visibleBounds.Y));
            drawingContext.DrawDrawing(sourceImage.Drawing);
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.Pop();
        }

        var bitmap = new RenderTargetBitmap(
            (int)canvasSize,
            (int)canvasSize,
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(drawingVisual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Rect GetVisibleBounds(Drawing drawing)
    {
        return drawing switch
        {
            DrawingGroup drawingGroup => GetVisibleBounds(drawingGroup),
            GeometryDrawing geometryDrawing => GetVisibleBounds(geometryDrawing),
            GlyphRunDrawing glyphRunDrawing => GetVisibleBounds(glyphRunDrawing),
            ImageDrawing imageDrawing => GetVisibleBounds(imageDrawing),
            VideoDrawing => Rect.Empty,
            _ => drawing.Bounds
        };
    }

    private static Rect GetVisibleBounds(DrawingGroup drawingGroup)
    {
        if (drawingGroup.Opacity <= 0 || drawingGroup.Children.Count == 0)
        {
            return Rect.Empty;
        }

        var hasVisibleChild = false;
        var bounds = Rect.Empty;

        foreach (var child in drawingGroup.Children)
        {
            var childBounds = GetVisibleBounds(child);
            if (!IsValidBounds(childBounds))
            {
                continue;
            }

            bounds = hasVisibleChild ? Rect.Union(bounds, childBounds) : childBounds;
            hasVisibleChild = true;
        }

        if (!hasVisibleChild)
        {
            return Rect.Empty;
        }

        if (drawingGroup.Transform is { } transform && !transform.Value.IsIdentity)
        {
            bounds = transform.TransformBounds(bounds);
        }

        if (drawingGroup.ClipGeometry is { } clipGeometry && !clipGeometry.Bounds.IsEmpty)
        {
            var clipBounds = clipGeometry.Bounds;
            if (drawingGroup.Transform is { } clipTransform && !clipTransform.Value.IsIdentity)
            {
                clipBounds = clipTransform.TransformBounds(clipBounds);
            }

            bounds.Intersect(clipBounds);
        }

        return bounds;
    }

    private static Rect GetVisibleBounds(GeometryDrawing geometryDrawing)
    {
        if (geometryDrawing.Geometry is null)
        {
            return Rect.Empty;
        }

        var hasBrush = IsVisibleBrush(geometryDrawing.Brush);
        var hasPen = IsVisiblePen(geometryDrawing.Pen);

        if (!hasBrush && !hasPen)
        {
            return Rect.Empty;
        }

        Rect bounds;
        if (hasBrush && hasPen)
        {
            bounds = Rect.Union(
                geometryDrawing.Geometry.Bounds,
                geometryDrawing.Geometry.GetRenderBounds(geometryDrawing.Pen));
        }
        else if (hasPen)
        {
            bounds = geometryDrawing.Geometry.GetRenderBounds(geometryDrawing.Pen);
        }
        else
        {
            bounds = geometryDrawing.Geometry.Bounds;
        }

        return bounds;
    }

    private static Rect GetVisibleBounds(GlyphRunDrawing glyphRunDrawing)
    {
        return glyphRunDrawing switch
        {
            { GlyphRun: not null } when IsVisibleBrush(glyphRunDrawing.ForegroundBrush) => glyphRunDrawing.Bounds,
            _ => Rect.Empty
        };
    }

    private static Rect GetVisibleBounds(ImageDrawing imageDrawing)
    {
        return imageDrawing.ImageSource is null || imageDrawing.Rect.IsEmpty
            ? Rect.Empty
            : imageDrawing.Rect;
    }

    private static bool IsVisibleBrush(Brush? brush)
    {
        return brush is not null && brush.Opacity > 0;
    }

    private static bool IsVisiblePen(Pen? pen)
    {
        return pen is not null &&
               pen.Thickness > 0 &&
               IsVisibleBrush(pen.Brush);
    }

    private static bool IsValidBounds(Rect bounds)
    {
        return !bounds.IsEmpty &&
               !double.IsNaN(bounds.X) &&
               !double.IsNaN(bounds.Y) &&
               !double.IsNaN(bounds.Width) &&
               !double.IsNaN(bounds.Height) &&
               !double.IsInfinity(bounds.X) &&
               !double.IsInfinity(bounds.Y) &&
               !double.IsInfinity(bounds.Width) &&
               !double.IsInfinity(bounds.Height) &&
               bounds.Width > 0 &&
               bounds.Height > 0;
    }
}
