#pragma warning disable CA1822 // Wave A stubs: the wave B owner's handlers use brain state, then delete this line.
using FotoHavn.Core.Storage;
using FotoHavn.Protocol;
using FotoHavn.Protocol.Presentations;

namespace FotoHavn.Core;

// The Event list (plan section 8, Event list; M6 contract section 4). Owner: M6 wave B1a. Wave A defined the rendering of the
// rows (the shape is protocol 6) and left every command a stub that answers rejected notAvailable.
//
// B1a: implement the six commands below; refresh `_eventSummaries` after a save or a delete (re-list through the store off the
// executor and post the result back, like Brain.Storage.cs does at open); show the delete confirmation by setting
// `_confirmation` (kind deleteEvent, the Event's name, the counts) and clear it on confirm or cancel; open a folder only through
// RequestOpenFolder(path) (Brain.Operator.cs). Rejections: EditEvent and DeleteEvent of the running Event, and StartEvent of any,
// are rejected eventRunning; an unknown event id is notAvailable.
public sealed partial class Brain
{
    /// <summary>The Event list as the operator sees it: the saved Events, newest first, with the running one marked and every row's actions decided here.</summary>
    private EventListView RenderEventList()
    {
        var runningId = ActiveEventId;
        return new EventListView([.. _eventSummaries.Select(summary => ToRow(summary, runningId))], NewEventAvailable: true);
    }

    private static EventListRow ToRow(EventSummary summary, string? runningId)
    {
        var anyRunning = runningId is not null;
        var running = anyRunning && string.Equals(summary.Id, runningId, StringComparison.Ordinal);
        return new EventListRow(
            summary.Id,
            summary.Name,
            summary.EventDate,
            summary.Venue,
            summary.CompletedGuestCycles,
            summary.LastUsedAt,
            running,
            new EventRowActions(
                CanStart: !anyRunning,
                CanEdit: !running,
                CanDelete: !running,
                CanOpenFolder: true,
                CanOpenGallery: true),
            anyRunning ? StartDisabledReason.EventRunning : null);
    }

    // TODO(M6 wave B1a): the Event list commands.
    private CommandResultMessage OpenNewEvent(string id) => NotAvailable(id);

    private CommandResultMessage EditEvent(string id, EditEventPayload payload) => NotAvailable(id);

    private CommandResultMessage DeleteEvent(string id, DeleteEventPayload payload) => NotAvailable(id);

    private CommandResultMessage ConfirmDeleteEvent(string id) => NotAvailable(id);

    private CommandResultMessage CancelDeleteEvent(string id) => NotAvailable(id);

    private CommandResultMessage OpenEventFolder(string id, OpenEventFolderPayload payload) => NotAvailable(id);
}
