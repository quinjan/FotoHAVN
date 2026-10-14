using FotoHavn.Core.Camera;
using FotoHavn.Core.Camera.Policies;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Camera.Windows;

/// <summary>
/// The Core camera policies the adapter calls. An interface so pure tests can run the adapter's logic on stand-ins while
/// the real policies are implemented elsewhere; production always uses <see cref="CoreCameraPolicies"/>.
/// </summary>
internal interface ICameraPolicySet
{
    CameraFormat? ChooseFormat(IReadOnlyList<CameraFormat> formats);

    bool IsEligible(TimeSpan frameTimestamp, TimeSpan notBefore);

    CameraFailureKind Classify(int hresult, CameraOperation operation);

    CameraFailureKind Classify(Exception exception, CameraOperation operation);
}

internal sealed class CoreCameraPolicies : ICameraPolicySet
{
    public static CoreCameraPolicies Instance { get; } = new();

    private CoreCameraPolicies()
    {
    }

    public CameraFormat? ChooseFormat(IReadOnlyList<CameraFormat> formats) => CameraFormatPolicy.Choose(formats);

    public bool IsEligible(TimeSpan frameTimestamp, TimeSpan notBefore) => CaptureFrameSelector.IsEligible(frameTimestamp, notBefore);

    public CameraFailureKind Classify(int hresult, CameraOperation operation) => CameraFailureClassifier.Classify(hresult, operation);

    public CameraFailureKind Classify(Exception exception, CameraOperation operation) => CameraFailureClassifier.Classify(exception, operation);
}
