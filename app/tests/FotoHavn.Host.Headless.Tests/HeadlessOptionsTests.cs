using FotoHavn.Protocol.Testing;

namespace FotoHavn.Host.Headless.Tests;

public sealed class HeadlessOptionsTests
{
    [Fact]
    public void No_arguments_use_the_defaults()
    {
        Assert.Equal(new HeadlessOptions(null, 0, null), HeadlessOptions.Parse([]));
    }

    [Fact]
    public void Reads_every_option_with_separate_values()
    {
        Assert.Equal(
            new HeadlessOptions(@"C:\run\Data", 5180, @"C:\ui\dist"),
            HeadlessOptions.Parse(["--data", @"C:\run\Data", "--port", "5180", "--ui", @"C:\ui\dist"]));
    }

    [Fact]
    public void Reads_inline_values_and_the_m1_ui_dist_spelling()
    {
        Assert.Equal(new HeadlessOptions("D=1", 0, "U"), HeadlessOptions.Parse(["--data=D=1", "--port=0", "--ui-dist=U"]));
    }

    [Theory]
    [InlineData("diskFull", StorageFaultKind.DiskFull)]
    [InlineData("accessDenied", StorageFaultKind.AccessDenied)]
    [InlineData("locked", StorageFaultKind.Locked)]
    [InlineData("DISKFULL", StorageFaultKind.DiskFull)]
    public void Reads_the_e2e_storage_fault_in_every_build(string value, StorageFaultKind expected)
    {
        Assert.Equal(expected, HeadlessOptions.Parse(["--e2e-storage-fault", value]).E2EStorageFault);
        Assert.Equal(expected, HeadlessOptions.Parse([$"--e2e-storage-fault={value}"]).E2EStorageFault);
        Assert.Null(HeadlessOptions.Default.E2EStorageFault);
    }

    [Theory]
    [InlineData("--e2e-storage-fault")]
    [InlineData("--e2e-storage-fault", "full")]
    [InlineData("--e2e-storage-fault", "1")]
    [InlineData("--e2e-storage-fault", "9")]
    public void Rejects_a_bad_e2e_storage_fault(params string[] args)
    {
        Assert.Throws<HeadlessOptionsException>(() => HeadlessOptions.Parse(args));
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--data")]
    [InlineData("--data=")]
    [InlineData("--port", "65536")]
    [InlineData("--port", "-1")]
    [InlineData("--port", "port")]
    [InlineData("--ui", " ")]
    [InlineData("data")]
    public void Rejects_invalid_command_lines(params string[] args)
    {
        Assert.Throws<HeadlessOptionsException>(() => HeadlessOptions.Parse(args));
    }

    [Fact]
    public void The_data_folder_defaults_to_Data_beside_the_executable()
    {
        Assert.Equal(Path.GetFullPath(@"C:\app\Data"), HeadlessOptions.Default.ResolveDataFolder(@"C:\app"));
        Assert.Equal(Path.GetFullPath(@"C:\elsewhere"), (HeadlessOptions.Default with { DataFolder = @"C:\elsewhere" }).ResolveDataFolder(@"C:\app"));
    }

    [Fact]
    public void The_ui_folder_is_the_given_folder_or_the_nearest_built_ui_dist()
    {
        var root = TestHost.NewScratchFolder();
        try
        {
            var dist = Directory.CreateDirectory(Path.Combine(root, "ui", "dist")).FullName;
            var nested = Directory.CreateDirectory(Path.Combine(root, "src", "project")).FullName;

            // A ui/dist without index.html isn't a built UI. (The walk may still find one further up, such as the repo's own.)
            Assert.NotEqual(dist, HeadlessOptions.Default.ResolveUiFolder(nested));
            File.WriteAllText(Path.Combine(dist, "index.html"), "<!doctype html>");
            Assert.Equal(dist, HeadlessOptions.Default.ResolveUiFolder(nested));
            Assert.Equal(nested, (HeadlessOptions.Default with { UiFolder = nested }).ResolveUiFolder(root));
            Assert.Null((HeadlessOptions.Default with { UiFolder = Path.Combine(root, "missing") }).ResolveUiFolder(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
