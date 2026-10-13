using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Host.Headless.Tests;

/// <summary>The headless host composes the real Event store: the real brain opens it on the host's Data folder.</summary>
public sealed class StorageCompositionTests
{
    private static readonly string Hello = $$"""{"type":"hello","protocolVersion":{{ProtocolVersion.Current}}}""";

    [Fact]
    public async Task The_real_brain_opens_the_store_in_the_Data_folder_and_shows_the_empty_Event_list()
    {
        await using var host = await TestHost.StartAsync(realBrain: true);
        using var client = await host.ConnectAsync(Role.Operator);

        var presentation = await WaitForResolvedPresentationAsync(client);

        Assert.Equal(OperatorSurface.EventList, presentation.Surface);
        Assert.Empty(presentation.EventList!.Events);
        Assert.True(File.Exists(Path.Combine(host.DataFolder, "fotohavn.db")));
    }

#if E2E_HOOKS
    [Fact]
    public async Task An_E2E_build_fails_the_stores_first_write_with_the_storage_fault_flag()
    {
        await using var host = await TestHost.StartAsync(realBrain: true, storageFault: FotoHavn.Protocol.Testing.StorageFaultKind.DiskFull);
        using var client = await host.ConnectAsync(Role.Operator);

        var presentation = await WaitForResolvedPresentationAsync(client);

        Assert.Equal(OperatorSurface.StorageError, presentation.Surface);
        Assert.Equal(StorageErrorReason.DiskFull, presentation.StorageError!.Kind);
    }
#else
    [Fact]
    public async Task A_field_build_ignores_the_storage_fault_flag()
    {
        await using var host = await TestHost.StartAsync(realBrain: true, storageFault: FotoHavn.Protocol.Testing.StorageFaultKind.DiskFull);
        using var client = await host.ConnectAsync(Role.Operator);

        var presentation = await WaitForResolvedPresentationAsync(client);

        Assert.Equal(OperatorSurface.EventList, presentation.Surface);
        Assert.Null(presentation.StorageError);
    }
#endif

    /// <summary>Says hello, then reads messages until the operator leaves the Starting surface.</summary>
    private static async Task<OperatorPresentation> WaitForResolvedPresentationAsync(BridgeClient client)
    {
        await client.SendAsync(Hello);
        while (true)
        {
            var presentation = await client.ReceiveMessageAsync() switch
            {
                OperatorWelcomeMessage welcome => welcome.Presentation,
                OperatorPresentationMessage message => message.Presentation,
                var other => throw new InvalidOperationException($"Unexpected {other.GetType().Name}."),
            };
            if (presentation.Surface != OperatorSurface.Starting)
            {
                return presentation;
            }
        }
    }
}