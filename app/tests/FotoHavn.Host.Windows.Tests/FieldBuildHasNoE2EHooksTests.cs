using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Proves the metadata reader below finds AssemblyMetadata entries; an E2E build adds ("FotoHavnE2E", "true") the same way.
[assembly: AssemblyMetadata("FotoHavn.Host.Windows.Tests.MetadataProbe", "readable")]

namespace FotoHavn.Host.Windows.Tests;

/// <summary>
/// Publish guard, by reflection: the normally built FOTOHAVN and FotoHavn.* assemblies contain no type in an
/// <c>.E2E</c> namespace and no <c>FotoHavnE2E</c> assembly metadata, and the copied UI is not the E2E build.
/// Metadata is read directly, so internal and nested types count too.
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
        SkipInE2EBuild();
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
    public void Metadata_reader_finds_assembly_metadata()
    {
        var entries = AssemblyMetadata(typeof(FieldBuildHasNoE2EHooksTests).Assembly.Location);

        Assert.Contains(new KeyValuePair<string?, string?>("FotoHavn.Host.Windows.Tests.MetadataProbe", "readable"), entries);
    }

    [Fact]
    public void Product_assemblies_carry_no_E2E_build_marker()
    {
        SkipInE2EBuild();
        var marked = ProductAssemblies()
            .Where(path => AssemblyMetadata(path).Any(entry => entry.Key == "FotoHavnE2E"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(marked);
    }

    [Fact]
    public void Copied_UI_is_not_the_E2E_build()
    {
        SkipInE2EBuild();
        Assert.False(
            File.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot", "e2e-build.txt")),
            "wwwroot/ holds the E2E UI build (ui/dist-e2e); field builds copy ui/dist.");
    }

    [Fact]
    public void Hook_channel_is_not_compiled_in()
    {
        SkipInE2EBuild();
        Assert.False(TestHooks.TestHookChannel.IsCompiled);
    }

    /// <summary>These tests assert facts about field builds; an E2E build contains the hooks by design.</summary>
    private static void SkipInE2EBuild()
    {
#if E2E_HOOKS
        Assert.Skip("not run: asserts a field build; this is an E2E build (hooks compiled in on purpose).");
#endif
    }

    /// <summary>The <c>[assembly: AssemblyMetadata(key, value)]</c> entries, read without loading the assembly.</summary>
    private static List<KeyValuePair<string?, string?>> AssemblyMetadata(string path)
    {
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var entries = new List<KeyValuePair<string?, string?>>();
        foreach (var handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = metadata.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var constructor = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference
                || metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)constructor.Parent).Name) != "AssemblyMetadataAttribute")
            {
                continue;
            }

            var value = metadata.GetBlobReader(attribute.Value);
            if (value.ReadUInt16() == 1)
            {
                entries.Add(new(value.ReadSerializedString(), value.ReadSerializedString()));
            }
        }

        return entries;
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
