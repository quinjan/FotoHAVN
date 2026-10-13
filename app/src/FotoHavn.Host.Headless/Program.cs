using Microsoft.Extensions.FileProviders;

// M1 placeholder for the headless dev host: Kestrel on 127.0.0.1 (random port) serving the built UI.
// M2 adds the brain, the fake camera, and the WebSocket bridge.
var uiDist = ResolveUiDist(args);

var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");

var app = builder.Build();
if (uiDist is not null)
{
    var files = new PhysicalFileProvider(uiDist);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
}
else
{
    app.MapGet("/", () => Results.Text("The FOTOHAVN UI is not built.", "text/plain"));
}

await app.StartAsync();
var addresses = app.Urls;
Console.WriteLine($"FOTOHAVN headless host listening on {string.Join(", ", addresses)} (UI: {uiDist ?? "not built"})");
await app.WaitForShutdownAsync();

static string? ResolveUiDist(string[] args)
{
    var index = Array.IndexOf(args, "--ui-dist");
    if (index >= 0 && index + 1 < args.Length)
    {
        return Directory.Exists(args[index + 1]) ? Path.GetFullPath(args[index + 1]) : null;
    }

    for (var folder = new DirectoryInfo(Directory.GetCurrentDirectory()); folder is not null; folder = folder.Parent)
    {
        var candidate = Path.Combine(folder.FullName, "ui", "dist");
        if (File.Exists(Path.Combine(candidate, "index.html")))
        {
            return candidate;
        }
    }

    return null;
}
