using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;

namespace FotoHavn.App.Controls;

internal static class MotionPolicy
{
    public static Duration Standard() => Resolve("MotionStandardDuration");

    public static Duration Slow() => Resolve("MotionSlowDuration");

    public static Duration Resolve(string durationResourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durationResourceKey);
        var resources = Application.Current.Resources;
        return new UISettings().AnimationsEnabled
            ? (Duration)resources[durationResourceKey]
            : (Duration)resources["MotionInstantDuration"];
    }
}
