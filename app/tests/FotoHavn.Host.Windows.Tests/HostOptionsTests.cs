using FotoHavn.Protocol.Testing;

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
        var options = HostOptions.Parse(["--data", @"C:\run\Data", "--remote-debugging-port", "9333", "--e2e-camera", "Integrated Webcam", "--webview2-runtime", @"C:\wv2"]);

        Assert.Equal(new HostOptions(@"C:\run\Data", 9333, "Integrated Webcam", @"C:\wv2"), options);
    }

    [Fact]
    public void Reads_every_option_with_inline_values()
    {
        var options = HostOptions.Parse(["--data=D", "--remote-debugging-port=1", "--e2e-camera=cam=1", "--webview2-runtime=R"]);

        Assert.Equal(new HostOptions("D", 1, "cam=1", "R"), options);
    }

    [Theory]
    [InlineData("diskFull", StorageFaultKind.DiskFull)]
    [InlineData("accessDenied", StorageFaultKind.AccessDenied)]
    [InlineData("locked", StorageFaultKind.Locked)]
    [InlineData("Locked", StorageFaultKind.Locked)]
    public void Reads_the_e2e_storage_fault_in_every_build(string value, StorageFaultKind expected)
    {
        Assert.Equal(expected, HostOptions.Parse(["--e2e-storage-fault", value]).E2EStorageFault);
        Assert.Equal(expected, HostOptions.Parse([$"--e2e-storage-fault={value}"]).E2EStorageFault);
        Assert.Null(HostOptions.Default.E2EStorageFault);
    }

    [Theory]
    [InlineData("--e2e-storage-fault")]
    [InlineData("--e2e-storage-fault", "full")]
    [InlineData("--e2e-storage-fault", "1")]
    [InlineData("--e2e-storage-fault", "9")]
    public void Rejects_a_bad_e2e_storage_fault(params string[] args)
    {
        Assert.Throws<HostOptionsException>(() => HostOptions.Parse(args));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--data")]
    [InlineData("--data=")]
    [InlineData("--remote-debugging-port", "0")]
    [InlineData("--remote-debugging-port", "65536")]
    [InlineData("--remote-debugging-port", "-1")]
    [InlineData("--remote-debugging-port", "port")]
    [InlineData("--webview2-runtime")]
    [InlineData("data")]
    public void Rejects_invalid_command_lines(params string[] args)
    {
        Assert.Throws<HostOptionsException>(() => HostOptions.Parse(args));
    }
}
