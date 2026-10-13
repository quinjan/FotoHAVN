using System.Reflection;
using FotoHavn.Storage.Migrations;

namespace FotoHavn.Storage.Tests.Migrations;

public sealed class MigrationCatalogTests
{
    [Fact]
    public void The_embedded_migrations_start_at_one_and_run_without_gaps()
    {
        var scripts = MigrationCatalog.Embedded;

        Assert.Equal(Enumerable.Range(1, scripts.Count), scripts.Select(script => script.Version));
        Assert.Equal(scripts[^1].Version, MigrationCatalog.LatestVersion);
        Assert.Equal("initial", scripts[0].Name);
        Assert.All(scripts, script => Assert.False(string.IsNullOrWhiteSpace(script.Sql)));
    }

    [Fact]
    public void A_shipped_script_holds_no_pragma_or_transaction_statements()
    {
        Assert.All(MigrationCatalog.Embedded, script =>
            Assert.DoesNotMatch(@"(?im)^\s*(PRAGMA|BEGIN|COMMIT|END\s+TRANSACTION|ROLLBACK)\b", script.Sql));
    }

    [Fact]
    public void Parse_reads_the_version_and_name_from_the_file_name()
    {
        var script = MigrationCatalog.Parse("0012_add_event_notes.sql", "SELECT 1;");

        Assert.Equal(new MigrationScript(12, "add_event_notes", "SELECT 1;"), script);
    }

    [Theory]
    [InlineData("1_initial.sql")]
    [InlineData("00001_initial.sql")]
    [InlineData("0001_Initial.sql")]
    [InlineData("0001-initial.sql")]
    [InlineData("0001_initial.txt")]
    [InlineData("initial.sql")]
    [InlineData("0001_.sql")]
    public void Parse_refuses_names_that_break_the_rule(string fileName)
    {
        Assert.Throws<FormatException>(() => MigrationCatalog.Parse(fileName, "SELECT 1;"));
    }

    [Fact]
    public void Parse_refuses_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => MigrationCatalog.Parse(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => MigrationCatalog.Parse("0001_a.sql", null!));
    }

    [Fact]
    public void Order_sorts_by_version()
    {
        var ordered = MigrationCatalog.Order([new(2, "b", "x"), new(1, "a", "x"), new(3, "c", "x")]);

        Assert.Equal([1, 2, 3], ordered.Select(script => script.Version));
    }

    [Fact]
    public void Order_refuses_gaps_repeats_a_late_start_and_an_empty_set()
    {
        Assert.Throws<InvalidOperationException>(() => MigrationCatalog.Order([new(1, "a", "x"), new(3, "c", "x")]));
        Assert.Throws<InvalidOperationException>(() => MigrationCatalog.Order([new(1, "a", "x"), new(1, "b", "x")]));
        Assert.Throws<InvalidOperationException>(() => MigrationCatalog.Order([new(2, "b", "x")]));
        Assert.Throws<InvalidOperationException>(() => MigrationCatalog.Order([]));
        Assert.Throws<ArgumentNullException>(() => MigrationCatalog.Order(null!));
    }

    [Fact]
    public void LoadEmbedded_reads_this_assembly_and_refuses_null()
    {
        Assert.Equal(MigrationCatalog.Embedded, MigrationCatalog.LoadEmbedded(typeof(MigrationCatalog).Assembly));
        Assert.Throws<ArgumentNullException>(() => MigrationCatalog.LoadEmbedded(null!));
    }

    [Fact]
    public void LoadEmbedded_of_an_assembly_with_no_migrations_fails()
    {
        Assert.Throws<InvalidOperationException>(() => MigrationCatalog.LoadEmbedded(Assembly.GetExecutingAssembly()));
    }

    [Fact]
    public void The_resource_names_are_ordered_by_file_name()
    {
        var names = typeof(MigrationCatalog).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("FotoHavn.Storage.Migrations.", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(names.Order(StringComparer.Ordinal), names);
        Assert.Contains("FotoHavn.Storage.Migrations.0001_initial.sql", names);
    }
}
