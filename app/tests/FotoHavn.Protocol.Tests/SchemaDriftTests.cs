namespace FotoHavn.Protocol.Tests;

public sealed class SchemaDriftTests
{
    [Fact]
    public void Committed_schema_matches_the_protocol_records()
    {
        var exported = ProtocolSchema.ExportJson();

        if (SourcePaths.Updating)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SourcePaths.SchemaFile)!);
            File.WriteAllText(SourcePaths.SchemaFile, exported);
        }

        Assert.True(
            File.Exists(SourcePaths.SchemaFile),
            $"{SourcePaths.SchemaFile} is missing. Run scripts/generate-protocol.ps1 from app/.");
        Assert.True(
            exported == SourcePaths.ReadNormalized(SourcePaths.SchemaFile),
            "The committed protocol schema is stale. Run scripts/generate-protocol.ps1 from app/ and commit the schema and ui/src/protocol/generated.ts.");
    }

    [Fact]
    public void Export_is_deterministic()
    {
        Assert.Equal(ProtocolSchema.ExportJson(), ProtocolSchema.ExportJson());
    }
}
