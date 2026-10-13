namespace FotoHavn.Core;

/// <summary>
/// Names the brain assembly. The brain never calls Windows APIs.
/// </summary>
public static class CoreAssembly
{
    public static string Name { get; } = typeof(CoreAssembly).Assembly.GetName().Name!;
}
