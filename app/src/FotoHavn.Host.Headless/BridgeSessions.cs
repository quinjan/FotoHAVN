using System.Collections.Concurrent;
using FotoHavn.Protocol;

namespace FotoHavn.Host.Headless;

/// <summary>The open bridge sessions, so the host can find a role's sockets (the headless <c>test.crashUi</c>).</summary>
internal sealed class BridgeSessions
{
    private readonly ConcurrentDictionary<BridgeSession, byte> _sessions = new();

    public void Add(BridgeSession session) => _sessions.TryAdd(session, 0);

    public void Remove(BridgeSession session) => _sessions.TryRemove(session, out _);

    public int Count(Role role) => _sessions.Keys.Count(session => session.Role == role);

    /// <summary>Aborts every socket of <paramref name="role"/> without a close handshake; returns how many.</summary>
    public int AbortRole(Role role)
    {
        var aborted = 0;
        foreach (var session in _sessions.Keys.Where(session => session.Role == role))
        {
            session.Abort();
            aborted++;
        }

        return aborted;
    }
}
