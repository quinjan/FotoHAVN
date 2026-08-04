namespace CaptureQualityContract;

public sealed record CameraSample(
    string Name,
    int EncodedWidth,
    int EncodedHeight,
    int ClockwiseOrientation,
    bool EncodesJpeg = true);

public sealed record OutputSlot(int Width, int Height)
{
    public double AspectRatio => (double)Width / Height;
}

public sealed record CropResult(
    int NormalizedWidth,
    int NormalizedHeight,
    int CropX,
    int CropY,
    int CropWidth,
    int CropHeight,
    bool Eligible,
    IReadOnlyList<string> RejectionReasons);

public static class CaptureQuality
{
    public static CameraSample? SelectBest(
        IEnumerable<CameraSample> candidates,
        OutputSlot slot) =>
        candidates
            .Select(camera => (Camera: camera, Result: Evaluate(camera, slot)))
            .Where(candidate => candidate.Result.Eligible)
            .OrderByDescending(candidate =>
                (long)candidate.Result.NormalizedWidth * candidate.Result.NormalizedHeight)
            .ThenByDescending(candidate =>
                (long)candidate.Result.CropWidth * candidate.Result.CropHeight)
            .Select(candidate => candidate.Camera)
            .FirstOrDefault();

    public static CropResult Evaluate(CameraSample camera, OutputSlot slot)
    {
        var rotated = camera.ClockwiseOrientation is 90 or 270;
        var width = rotated ? camera.EncodedHeight : camera.EncodedWidth;
        var height = rotated ? camera.EncodedWidth : camera.EncodedHeight;

        var sourceRatio = (double)width / height;
        int cropWidth;
        int cropHeight;

        if (sourceRatio > slot.AspectRatio)
        {
            cropHeight = height;
            cropWidth = (int)Math.Floor(height * slot.AspectRatio);
        }
        else
        {
            cropWidth = width;
            cropHeight = (int)Math.Floor(width / slot.AspectRatio);
        }

        var reasons = new List<string>();
        if (!camera.EncodesJpeg)
            reasons.Add("photo stream does not encode to JPEG");
        if (width < 1280 || height < 720)
            reasons.Add($"normalized photo is {width}x{height}; needs at least 1280x720");
        if (width < height)
            reasons.Add("normalized photo remains portrait; orientation is absent or unusable");
        if (cropWidth < slot.Width || cropHeight < slot.Height)
            reasons.Add($"center crop is {cropWidth}x{cropHeight}; output slot needs {slot.Width}x{slot.Height}");

        return new CropResult(
            width,
            height,
            (width - cropWidth) / 2,
            (height - cropHeight) / 2,
            cropWidth,
            cropHeight,
            reasons.Count == 0,
            reasons);
    }
}
