using Microsoft.Extensions.Primitives;

namespace FotoHavn.Host.Headless.Tests;

public sealed class LoopbackGuardTests
{
    [Theory]
    [InlineData("127.0.0.1", true)]
    [InlineData("127.1.2.3", true)]
    [InlineData("localhost", true)]
    [InlineData("LocalHost", true)]
    [InlineData("[::1]", true)]
    [InlineData("::1", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("10.0.0.1", false)]
    [InlineData("localhost.example.com", false)]
    [InlineData("[::2]", false)]
    public void Recognizes_loopback_hosts(string? host, bool loopback)
    {
        Assert.Equal(loopback, LoopbackGuard.IsLoopbackHost(host));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("http://127.0.0.1:5173", true)]
    [InlineData("http://localhost:5173", true)]
    [InlineData("https://localhost", true)]
    [InlineData("http://[::1]:8080", true)]
    [InlineData("null", false)]
    [InlineData("file://", false)]
    [InlineData("ws://127.0.0.1", false)]
    [InlineData("http://example.com", false)]
    public void Allows_only_missing_or_loopback_origins(string? origin, bool allowed)
    {
        Assert.Equal(allowed, LoopbackGuard.IsAllowedOrigin(origin is null ? StringValues.Empty : new StringValues(origin)));
    }

    [Fact]
    public void Two_origins_are_refused()
    {
        Assert.False(LoopbackGuard.IsAllowedOrigin(new StringValues(["http://localhost", "http://localhost"])));
    }
}
