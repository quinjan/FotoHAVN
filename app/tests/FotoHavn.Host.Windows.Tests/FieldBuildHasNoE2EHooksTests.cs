using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace FotoHavn.Host.Windows.Tests;

/// <summary>
/// Publish guard, by reflection: the normally built FOTOHAVN and FotoHavn.* assemblies contain no type in an
/// <c>.E2E</c> namespace. Type metadata is read directly, so internal and nested types count too.
/// </summary>
public sealed class FieldBuildHasNoE2EHooksTests
{
    private static readonly string[] RequiredAssemblies =
    [
        "FOTOHAVN.dll",
        "FotoHavn.Camera.Windows.dll",
        "FotoHavn.Catalog.dll",
        "FotoHavn.Core.dll",
        "FotoHavn.Protocol.dll",
        "FotoHavn.Storage.dll",
    ];

    [Fact]
    public void Product_assemblies_are_beside_the_tests()
    {
        var found = ProductAssemblies().Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(RequiredAssemblies, name => Assert.Contains(name, found));
    }

    [Fact]
    public void Product_assemblies_contain_no_E2E_namespace()
    {
        var offending = new List<string>();
        var hostNamespaceSeen = false;

        foreach (var path in ProductAssemblies())
        {
            foreach (var typeName in TypeNamespaces(path))
            {
                hostNamespaceSeen |= typeName.Namespace == "FotoHavn.Host.Windows";
                if (typeName.Namespace.Contains(".E2E", StringComparison.Ordinal))
                {
                    offending.Add($"{Path.GetFileName(path)}: {typeName.Namespace}.{typeName.Name}");
                }
            }
        }

        Assert.True(hostNamespaceSeen, "The scan found no FotoHavn.Host.Windows types, so it proves nothing.");
        Assert.Empty(offending);
    }

    [Fact]
    public void Hook_channel_is_not_compiled_in()
    {
        Assert.False(TestHooks.TestHookChannel.IsCompiled);
    }

    private static IEnumerable<string> ProductAssemblies() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(path =>
            {
                var name = Path.GetFileNameWithoutExtension(path);
                return name == "FOTOHAVN"
                    || (name.StartsWith("FotoHavn.", StringComparison.Ordinal) && !name.EndsWith(".Tests", StringComparison.Ordinal));
            });

    private static List<(string Namespace, string Name)> TypeNamespaces(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var names = new List<(string, string)>();
        foreach (var handle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(handle);
            var declaring = type;
            while (declaring.GetDeclaringType() is { IsNil: false } parent)
            {
                declaring = metadata.GetTypeDefinition(parent);
            }

            names.Add((metadata.GetString(declaring.Namespace), metadata.GetString(type.Name)));
        }

        return names;
    }
}
