using Microsoft.UI.Xaml;

namespace FotoHavn.App.Controls;

internal static class MotionPolicy
{
    public static Duration Fast() => Resolve("FotoHavnFastMotionDuration");

    public static Duration Standard() => Resolve("FotoHavnStandardMotionDuration");

    public static Duration Slow() => Resolve("FotoHavnSlowMotionDuration");

    private static Duration Resolve(string durationResourceKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(durationResourceKey);
        return (Duration)Application.Current.Resources[durationResourceKey];
    }
}
