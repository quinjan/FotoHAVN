namespace FotoHavn.Storage.FileSystem;

/// <summary>
/// A delete was refused because the path, or a folder above it, is a junction or symbolic link: following it could remove files
/// outside the Data folder. Nothing was deleted. The message names no path.
/// </summary>
public sealed class ReparsePointException() : IOException("The path goes through a junction or symbolic link, so it was left alone.");
