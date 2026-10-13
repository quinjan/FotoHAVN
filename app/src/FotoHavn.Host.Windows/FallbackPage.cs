namespace FotoHavn.Host.Windows;

/// <summary>Shown when <c>wwwroot/index.html</c> is missing beside the executable.</summary>
internal static class FallbackPage
{
    public const string Message = "The FOTOHAVN UI is not built.";

    public const string Html = """
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>FOTOHAVN</title>
          <style>
            html, body { height: 100%; margin: 0; background: #000; color: #fff; font-family: "Segoe UI", sans-serif; }
            main { height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.5rem; }
            h1 { margin: 0; font-size: 2rem; letter-spacing: 0.08em; }
            p { margin: 0; }
          </style>
        </head>
        <body>
          <main>
            <h1>FOTOHAVN</h1>
            <p>The FOTOHAVN UI is not built.</p>
            <p>Run <code>npm run build</code> in <code>app/ui</code>, then build the host again.</p>
          </main>
        </body>
        </html>
        """;
}
