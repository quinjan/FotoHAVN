namespace FotoHavn.Protocol;

/// <summary>
/// The protocol version the UI and the brain speak. The UI sends it in <c>hello</c>; any difference is a mismatch,
/// because the UI and the brain always ship together. Bump it with every incompatible change to the envelope,
/// commands, or presentations, and regenerate the schema and TypeScript types (<c>scripts/generate-protocol.ps1</c>).
/// </summary>
public static class ProtocolVersion
{
    public const int Current = 3;
}

/// <summary>Limits the brain enforces on every message it receives.</summary>
public static class ProtocolLimits
{
    /// <summary>The largest message the brain accepts, in UTF-8 bytes (256 KiB).</summary>
    public const int MaxMessageBytes = 256 * 1024;
}
