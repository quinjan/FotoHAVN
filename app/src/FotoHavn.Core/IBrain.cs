using FotoHavn.Protocol;

namespace FotoHavn.Core;

/// <summary>
/// The brain's entry point for hosts. A host connects each UI (one WebView2, or one WebSocket) with the role it assigned
/// and passes every message from that UI to <see cref="IBrainConnection.Receive"/>; the brain answers and pushes
/// presentations through the connection's <c>send</c> callback.
/// </summary>
/// <remarks>
/// Threading: every member may be called from any thread and returns at once. The brain posts the work to its
/// <see cref="IBrainExecutor"/> and touches its state only there, one work item at a time, in the order posted.
/// </remarks>
public interface IBrain : IDisposable
{
    /// <summary>
    /// Opens a connection for one UI. The brain sends nothing until the UI's <c>hello</c>, then answers it with
    /// <c>welcome</c> (or <c>protocolMismatch</c>) and pushes every later presentation of <paramref name="role"/>.
    /// Several connections may share a role; each gets every presentation of that role.
    /// </summary>
    /// <param name="role">The role the host assigned. The page never declares or changes it.</param>
    /// <param name="send">
    /// Delivers one serialized server message to this UI. The brain calls it only on its executor thread, one message at a
    /// time, in order. It must not block or throw: a host marshals it (WPF: <c>Dispatcher.BeginInvoke</c> to
    /// <c>PostWebMessageAsJson</c>; headless: a per-socket send queue). It may still be called briefly after
    /// <see cref="IDisposable.Dispose"/>; the host drops those messages.
    /// </param>
    IBrainConnection Connect(Role role, Action<string> send);
}

/// <summary>One UI's connection to the brain. Disposing it (window closed, WebView2 recreated, socket closed) removes it.</summary>
public interface IBrainConnection : IDisposable
{
    Role Role { get; }

    /// <summary>
    /// Hands the brain one raw message from the UI (<c>WebMessageAsJson</c>, or one WebSocket text frame). The brain
    /// validates it with <see cref="ClientMessageParser"/> on its executor and answers through <c>send</c>.
    /// </summary>
    void Receive(string json);
}

/// <summary>
/// The brain's single-threaded executor. Every brain state change runs here: received messages, connects and
/// disconnects, display changes, clock callbacks, and completions of host hook tasks.
/// </summary>
public interface IBrainExecutor
{
    /// <summary>
    /// Queues <paramref name="work"/> to run after everything posted before it, never concurrently with other work.
    /// Safe from any thread, including from inside a work item (which then runs later, not inline). A work item that
    /// throws is a brain bug: the executor reports it (the host logs it) and keeps running.
    /// </summary>
    void Post(Action work);

    /// <summary>Whether the calling thread is running a work item of this executor, for assertions.</summary>
    bool IsCurrentThread { get; }
}

/// <summary>The brain's only source of time and timers.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>
    /// Calls <paramref name="callback"/> once after <paramref name="delay"/>, on any thread; the brain posts it to its
    /// executor. Disposing the result cancels it, best effort: the brain ignores a callback that arrives after cancelling.
    /// </summary>
    IDisposable Schedule(TimeSpan delay, Action callback);
}
