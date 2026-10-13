namespace FotoHavn.Host.Windows.Tests;

public sealed class HostOptionsTests
{
    [Fact]
    public void No_arguments_use_the_defaults()
    {
        Assert.Equal(HostOptions.Default, HostOptions.Parse([]));
    }

    [Fact]
    public void Reads_every_option_with_separate_values()
    {
        var options = HostOptions.Parse(["--data", @"C:\run\Data", "--remote-debugging-port", "9333", "--e2e-camera", "Integrated Webcam"]);

        Assert.Equal(new HostOptions(@"C:\run\Data", 9333, "Integrated Webcam"), options);
    }

    [Fact]
    public void Reads_every_option_with_inline_values()
    {
        var options = HostOptions.Parse(["--data=D", "--remote-debugging-port=1", "--e2e-camera=cam=1"]);

        Assert.Equal(new HostOptions("D", 1, "cam=1"), options);
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--data")]
    [InlineData("--data=")]
    [InlineData("--remote-debugging-port", "0")]
    [InlineData("--remote-debugging-port", "65536")]
    [InlineData("--remote-debugging-port", "-1")]
    [InlineData("--remote-debugging-port", "port")]
    [InlineData("data")]
    public void Rejects_invalid_command_lines(params string[] args)
    {
        Assert.Throws<HostOptionsException>(() => HostOptions.Parse(args));
    }
}
