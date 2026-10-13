using System.Net.Sockets;
using System.Text;

namespace FotoHavn.Host.Headless.Tests.Support;

/// <summary>
/// Sends one HTTP/1.1 request with the exact request target given, over a plain socket. HttpClient normalizes dot
/// segments and some escapes before sending, so traversal attempts need this to reach the server as written.
/// </summary>
internal static class RawHttp
{
    public static async Task<(int Status, string Response)> SendAsync(int port, string method, string target, string host = "127.0.0.1")
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", port, timeout.Token);
        await using var stream = client.GetStream();
        var request = $"{method} {target} HTTP/1.1\r\nHost: {host}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), timeout.Token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var response = await reader.ReadToEndAsync(timeout.Token);
        var statusLine = response.Split("\r\n", 2)[0];
        return (int.Parse(statusLine.Split(' ')[1], System.Globalization.CultureInfo.InvariantCulture), response);
    }
}
