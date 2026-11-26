using SkiaSharp;

namespace NotesFunctions;

public static class ThumbnailGenerator
{
    /// <summary>Zmniejsza obraz tak, by dłuższy bok miał co najwyżej <paramref name="maxSize"/> px.</summary>
    /// <returns>Zakodowana miniatura albo null, jeśli plik nie jest obsługiwanym obrazem.</returns>
    public static byte[]? Create(byte[] imageBytes, SKEncodedImageFormat format, int maxSize)
    {
        using var codec = SKCodec.Create(new MemoryStream(imageBytes));
        if (codec is null)
        {
            return null;
        }

        using var decoded = SKBitmap.Decode(codec);
        if (decoded is null)
        {
            return null;
        }

        // Zdjęcia z telefonu często są zapisane "bokiem" z informacją o obrocie w EXIF – obracamy je od razu.
        using var oriented = ApplyOrientation(decoded, codec.EncodedOrigin);

        var scale = Math.Min(1.0, (double)maxSize / Math.Max(oriented.Width, oriented.Height));
        var width = Math.Max(1, (int)Math.Round(oriented.Width * scale));
        var height = Math.Max(1, (int)Math.Round(oriented.Height * scale));

        using var resized = oriented.Resize(new SKImageInfo(width, height), new SKSamplingOptions(SKCubicResampler.Mitchell));
        using var image = SKImage.FromBitmap(resized);
        using var data = image.Encode(format, quality: 80);
        return data?.ToArray();
    }

    // Obsługujemy obroty o 90/180/270°; rzadkie warianty z odbiciem lustrzanym zostawiamy bez zmian.
    private static SKBitmap ApplyOrientation(SKBitmap source, SKEncodedOrigin origin)
    {
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.RightTop => 90,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0,
        };

        if (degrees == 0)
        {
            return source.Copy();
        }

        var swap = degrees != 180;
        var rotated = new SKBitmap(swap ? source.Height : source.Width, swap ? source.Width : source.Height);
        using var canvas = new SKCanvas(rotated);
        canvas.Translate(rotated.Width / 2f, rotated.Height / 2f);
        canvas.RotateDegrees(degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, SKSamplingOptions.Default);
        return rotated;
    }
}
