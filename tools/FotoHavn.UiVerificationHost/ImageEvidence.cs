using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace FotoHavn.UiVerificationHost;

public static class ImageEvidence
{
    public static ImageComparison Compare(string targetPath, string actualPath, string diffPath)
    {
        using var targetSource = new Bitmap(targetPath);
        using var actualSource = new Bitmap(actualPath);
        using var target = Normalize(targetSource);
        using var actual = Normalize(actualSource);
        var width = Math.Max(target.Width, actual.Width);
        var height = Math.Max(target.Height, actual.Height);
        using var diff = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        var targetPixels = ReadPixels(target);
        var actualPixels = ReadPixels(actual);
        var diffPixels = new byte[width * height * 4];
        var changedPixels = 0L;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var targetOffset = ((y * target.Width) + x) * 4;
                var actualOffset = ((y * actual.Width) + x) * 4;
                var insideTarget = x < target.Width && y < target.Height;
                var insideActual = x < actual.Width && y < actual.Height;
                var equal = insideTarget && insideActual &&
                    targetPixels.AsSpan(targetOffset, 4).SequenceEqual(actualPixels.AsSpan(actualOffset, 4));
                if (equal)
                {
                    continue;
                }

                changedPixels++;
                var diffOffset = ((y * width) + x) * 4;
                diffPixels[diffOffset] = 255;
                diffPixels[diffOffset + 1] = 0;
                diffPixels[diffOffset + 2] = 255;
                diffPixels[diffOffset + 3] = 255;
            }
        }

        WritePixels(diff, diffPixels);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(diffPath))!);
        diff.Save(diffPath, ImageFormat.Png);
        return new(
            changedPixels,
            (long)width * height,
            Hash(targetPath),
            Hash(actualPath),
            Hash(diffPath));
    }

    private static Bitmap Normalize(Bitmap source) =>
        source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);

    private static byte[] ReadPixels(Bitmap bitmap)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[Math.Abs(data.Stride) * bitmap.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            return pixels;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static void WritePixels(Bitmap bitmap, byte[] pixels)
    {
        var rectangle = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rectangle, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

public sealed record ImageComparison(
    long ChangedPixels,
    long TotalPixels,
    string TargetSha256,
    string ActualSha256,
    string DiffSha256);
