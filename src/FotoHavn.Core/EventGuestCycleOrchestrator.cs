namespace FotoHavn.Core;

public sealed class EventGuestCycleOrchestrator
{
    private readonly IEventFileSystem fileSystem;
    private readonly SemaphoreSlim commandGate = new(1, 1);

    public EventGuestCycleOrchestrator(
        IEventFileSystem fileSystem,
        ICameraBoundary camera,
        IPhotoStripCompositor compositor,
        IApplicationClock clock)
    {
        this.fileSystem = fileSystem;
        Camera = camera;
        Compositor = compositor;
        Clock = clock;
    }

    public ICameraBoundary Camera { get; }

    public IPhotoStripCompositor Compositor { get; }

    public IApplicationClock Clock { get; }

    public async Task<ApplicationPresentation> ExecuteAsync(
        ApplicationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await commandGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return command switch
            {
                LaunchApplication => await LaunchAsync(cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unknown application command."),
            };
        }
        finally
        {
            commandGate.Release();
        }
    }

    private async Task<ApplicationPresentation> LaunchAsync(CancellationToken cancellationToken)
    {
        var savedEvents = await fileSystem.LoadEventsAsync(cancellationToken).ConfigureAwait(false);
        var tiles = new List<EventTilePresentation>(savedEvents.Count + 1)
        {
            new(EventTileKind.NewEvent, "New Event", "Set up a booth Event", "＋"),
        };

        tiles.AddRange(savedEvents
            .OrderByDescending(savedEvent => savedEvent.LastSavedAt)
            .Select(savedEvent => new EventTilePresentation(
                EventTileKind.SavedEvent,
                savedEvent.Name,
                $"Last saved {savedEvent.LastSavedAt.ToLocalTime():g}",
                "▶",
                savedEvent.Id,
                savedEvent.LastSavedAt)));

        return new ApplicationPresentation(
            "Saved Events",
            tiles,
            EmptyStateMessage: null,
            new ApplicationCanvasPresentation(1280, 720, AllowsReflow: false));
    }
}
