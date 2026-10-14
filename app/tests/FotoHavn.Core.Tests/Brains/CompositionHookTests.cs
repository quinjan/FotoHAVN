using FotoHavn.Core.Tests.Support;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Core.Tests.Brains;

// TODO(M7 wave B2): replace with the real behavior tests when the brain composes into the preview cache.
public sealed class CompositionHookTests
{
    [Theory]
    [InlineData(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip","lookId":"naturale-v1"}""")]
    [InlineData(TestHookNames.ComposeBlankPreview, """{"templateId":"panorama-four"}""")]
    public void The_composition_hooks_are_not_supported_yet(string name, string args)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.NotSupported), client.Single());
    }

    [Theory]
    [InlineData(TestHookNames.ComposeDiagnostics, """{"templateId":"signature-strip"}""")]
    [InlineData(TestHookNames.ComposeBlankPreview, """{}""")]
    public void The_composition_hooks_validate_their_arguments(string name, string args)
    {
        using var harness = BrainHarness.WithHooks();
        var client = harness.Connect(Role.Operator);

        var id = client.Test(name, args);
        harness.Run();

        Assert.Equal(TestResultMessage.Failure(id, TestHookError.InvalidArgs), client.Single());
    }

    [Fact]
    public void The_brain_requires_a_catalog_a_compositor_and_a_preview_store()
    {
        using var harness = new BrainHarness();

        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Catalog = null! }));
        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Compositor = null! }));
        Assert.Throws<ArgumentNullException>(() => new Brain(harness.Dependencies with { Previews = null! }));
    }
}
