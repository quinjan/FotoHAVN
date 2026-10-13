using FotoHavn.Core.Storage;
using FotoHavn.Storage.Thumbnails;
using Microsoft.Data.Sqlite;

namespace FotoHavn.Storage;

public sealed partial class SqliteEventStore
{
    public Task<string> GetThumbnailPathAsync(string sourceId, int width, CancellationToken ct) =>
        RunAsync(ct, db =>
        {
            StorageValidation.ValidateThumbnailRequest(sourceId, width);
            var (sourcePath, sha256) = FindThumbnailSource(db, sourceId) ?? throw NotFound("The thumbnail's source doesn't exist.");
            if (!_fs.Exists(sourcePath))
            {
                throw NotFound("The thumbnail's source file is missing.");
            }

            var name = ThumbnailName.Format(sourceId, sha256, width);
            var finalPath = Path.Combine(ThumbnailDirectory, name);
            if (!_fs.Exists(finalPath))
            {
                WriteThumbnail(finalPath, ThumbnailGenerator.CreateJpeg(_fs.ReadAllBytes(sourcePath), width));
            }

            return ThumbnailName.RelativePath(sourceId, sha256, width);
        });

    /// <summary>The file and SHA-256 of a kept Capture, or of a Guest Cycle's Photo Strip; null when neither is live.</summary>
    private (string Path, string Sha256)? FindThumbnailSource(SqliteConnection db, string sourceId)
    {
        var captures = Db.Query(
            db,
            null,
            $"SELECT g.event_id, g.id, c.file_name, c.sha256 FROM captures c JOIN guest_cycles g ON g.id = c.guest_cycle_id JOIN events e ON e.id = g.event_id WHERE c.id = @id AND c.state = 'kept' AND {LiveCycle};",
            reader => (Path.Combine(CycleDirectory(reader.GetString(0), reader.GetString(1)), reader.GetString(2)), reader.GetString(3)),
            ("@id", sourceId));
        if (captures.Count > 0)
        {
            return captures[0];
        }

        var strips = Db.Query(
            db,
            null,
            $"SELECT g.event_id, g.id, p.file_name, p.sha256 FROM photo_strips p JOIN guest_cycles g ON g.id = p.guest_cycle_id JOIN events e ON e.id = g.event_id WHERE p.guest_cycle_id = @id AND {LiveCycle};",
            reader => (Path.Combine(CycleDirectory(reader.GetString(0), reader.GetString(1)), reader.GetString(2)), reader.GetString(3)),
            ("@id", sourceId));
        return strips.Count > 0 ? strips[0] : null;
    }

    /// <summary>Staging then rename, like every artifact; a thumbnail is never edited in place.</summary>
    private void WriteThumbnail(string finalPath, byte[] jpeg)
    {
        var stagingDirectory = Path.Combine(ThumbnailDirectory, ".staging");
        var stagingPath = Path.Combine(stagingDirectory, StorageIds.NewId() + ".tmp");
        try
        {
            _fs.CreateDirectory(stagingDirectory);
            _fs.WriteAllBytesAndFlush(stagingPath, jpeg);
            _fs.Move(stagingPath, finalPath);
        }
        catch
        {
            DeleteQuietly([stagingPath]);
            throw;
        }
    }
}
