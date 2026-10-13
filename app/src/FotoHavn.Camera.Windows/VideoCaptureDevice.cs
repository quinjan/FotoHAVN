namespace FotoHavn.Camera.Windows;

/// <summary>A video capture device Windows reports, identified by its device id.</summary>
public sealed record VideoCaptureDevice(string Id, string Name);
