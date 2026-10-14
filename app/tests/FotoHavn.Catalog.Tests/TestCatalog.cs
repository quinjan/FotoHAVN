using FotoHavn.Core.Catalog;

namespace FotoHavn.Catalog.Tests;

/// <summary>The real Looks plus one artwork-free Template whose single window leaves paper showing, with hooks for failure paths.</summary>
internal sealed class TestCatalog : ICatalog
{
    public const string PlainId = "plain";

    private readonly CatalogData _real = new();

    public Exception? FindTemplateFailure { get; init; }

    public Action? OnFindLook { get; init; }

    public IReadOnlyList<TemplateDefinition> Templates { get; } =
    [
        new(PlainId, "Plain", "A test Template.", 400, 300, [new WindowRect(100, 50, 200, 100)], HasArtwork: false),
    ];

    public IReadOnlyList<LookDefinition> Looks => _real.Looks;

    public string DefaultLookId => _real.DefaultLookId;

    public TemplateDefinition? FindTemplate(string id) =>
        FindTemplateFailure is { } failure ? throw failure : Templates.FirstOrDefault(t => t.Id == id);

    public LookDefinition? FindLook(string id)
    {
        OnFindLook?.Invoke();
        return _real.FindLook(id);
    }
}
