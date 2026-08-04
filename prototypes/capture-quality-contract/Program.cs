using CaptureQualityContract;

var samples = new[]
{
    new CameraSample("Proven Integrated Webcam", 1920, 1080, 0),
    new CameraSample("Minimum 16:9 camera", 1280, 720, 0),
    new CameraSample("Minimum 4:3 camera", 1280, 960, 0),
    new CameraSample("Portrait pixels with valid 90-degree orientation", 720, 1280, 90),
    new CameraSample("Portrait pixels without orientation", 720, 1280, 0),
    new CameraSample("Legacy webcam", 640, 480, 0),
    new CameraSample("Large square camera", 1440, 1440, 0),
    new CameraSample("JPEG unavailable", 1920, 1080, 0, false),
};

var slots = new[]
{
    new OutputSlot(600, 420),
    new OutputSlot(600, 450),
    new OutputSlot(600, 400),
};

var provenCameraModes = new[]
{
    new CameraSample("Integrated Webcam — 640x480", 640, 480, 0),
    new CameraSample("Integrated Webcam — 1280x720", 1280, 720, 0),
    new CameraSample("Integrated Webcam — 1920x1080", 1920, 1080, 0),
};

var cameraIndex = 0;
var slotIndex = 0;

while (true)
{
    Console.Clear();
    var camera = samples[cameraIndex];
    var slot = slots[slotIndex];
    var result = CaptureQuality.Evaluate(camera, slot);
    var selected = CaptureQuality.SelectBest(provenCameraModes, slot);

    Console.WriteLine("\e[1mPROTOTYPE — Capture-quality contract\e[0m");
    Console.WriteLine("Tests whether normalized camera output can satisfy a final landscape slot by one deterministic center crop.\n");
    Console.WriteLine($"\e[1mCamera\e[0m          {camera.Name}");
    Console.WriteLine($"\e[1mEncoded photo\e[0m   {camera.EncodedWidth}x{camera.EncodedHeight} JPEG={camera.EncodesJpeg}");
    Console.WriteLine($"\e[1mOrientation\e[0m     {camera.ClockwiseOrientation} degrees clockwise");
    Console.WriteLine($"\e[1mNormalized photo\e[0m {result.NormalizedWidth}x{result.NormalizedHeight}");
    Console.WriteLine($"\e[1mOutput slot\e[0m     {slot.Width}x{slot.Height} ({slot.AspectRatio:F3}:1)");
    Console.WriteLine($"\e[1mCenter crop\e[0m     x={result.CropX}, y={result.CropY}, {result.CropWidth}x{result.CropHeight}");
    Console.WriteLine($"\e[1mVerdict\e[0m         {(result.Eligible ? "ELIGIBLE" : "REJECT")}");
    Console.WriteLine($"\e[1mSelected maximum\e[0m{(selected is null ? " none" : $" {selected.EncodedWidth}x{selected.EncodedHeight}")}");

    if (!result.Eligible)
        foreach (var reason in result.RejectionReasons)
            Console.WriteLine($"                - {reason}");

    Console.WriteLine("\n\e[2m[n] next camera  [p] previous camera  [s] next slot  [q] quit\e[0m");
    var key = Console.ReadKey(intercept: true).KeyChar;
    if (key == 'q') break;
    if (key == 'n') cameraIndex = (cameraIndex + 1) % samples.Length;
    if (key == 'p') cameraIndex = (cameraIndex - 1 + samples.Length) % samples.Length;
    if (key == 's') slotIndex = (slotIndex + 1) % slots.Length;
}
