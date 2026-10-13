namespace FotoHavn.Core;

/// <summary>
/// Placeholder until M2 brings the application state machine. The brain never calls Windows APIs.
/// </summary>
public static class CoreAssembly
{
    public static string Name { get; } = typeof(CoreAssembly).Assembly.GetName().Name!;
}
