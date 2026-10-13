using FotoHavn.Core.Storage;
using FotoHavn.Storage.FileSystem;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

/// <summary>
/// The startup reconciler (plan section 10, steps 1 to 7). <c>OpenAsync</c> calls it after migrations and the integrity
/// check, before the Event list can appear. Wave B2 implements it; the store (wave B1) calls it through this interface.
/// </summary>
public interface IStartupReconciler
{
    /// <summary>
    /// Repairs the Data folder against the database and reports what it did. Runs each step in order on
    /// <paramref name="connection"/> (open, migrated, foreign keys on) and <paramref name="fs"/>. Never touches
    /// <c>fotohavn.db</c>, <c>logs/</c>, or <c>webview2-user-data/</c>. A step that can't finish because of an I/O failure
    /// throws a <see cref="StorageException"/>; a damaged row or file is a problem in the report, not an exception.
    /// </summary>
    /// <param name="connection">The open database connection.</param>
    /// <param name="fs">The file system to use for every file operation.</param>
    /// <param name="dataRoot">The absolute Data folder path.</param>
    /// <param name="ct">Cancels the run between steps and between files.</param>
    Task<ReconcileReport> RunAsync(SqliteConnection connection, IStorageFileSystem fs, string dataRoot, CancellationToken ct);
}

/// <summary>
/// Observes where the Data folder lives. Wave B2 implements it (drive type, UNC, known cloud-sync roots, cloud-file
/// attributes, a write probe); <see cref="DataPathClassifier"/> turns the facts into a verdict.
/// </summary>
public interface IDataPathProbe
{
    /// <summary>Collects the facts about <paramref name="dataRoot"/>. Creates nothing that remains after it returns; never throws for an unreadable path (it reports <c>CanWrite = false</c>).</summary>
    DataPathFacts Probe(string dataRoot);
}
