using FotoHavn.Host.Headless;

// The headless dev host: the real brain over a WebSocket bridge, the built UI, and the media and preview routes, on
// 127.0.0.1. Usage: dotnet run --project src/FotoHavn.Host.Headless -- [--data <dir>] [--port <n>] [--ui <dir>]
HeadlessOptions options;
try
{
    options = HeadlessOptions.Parse(args);
}
catch (HeadlessOptionsException exception)
{
    await Console.Error.WriteLineAsync(exception.Message);
    await Console.Error.WriteLineAsync("Usage: FotoHavn.Host.Headless [--data <dir>] [--port <n>] [--ui <dir>]");
    return 2;
}

var settings = new HeadlessSettings(
    options.ResolveDataFolder(AppContext.BaseDirectory),
    options.ResolveUiFolder(Directory.GetCurrentDirectory()),
    options.Port);

await using var host = HeadlessApp.Create(settings);
var endpoints = await host.StartAsync();
host.WriteStartup(Console.Out, endpoints);
await host.App.WaitForShutdownAsync();
return 0;
