// THROWAWAY PROTOTYPE SERVER — intentionally dependency-free.
import { createReadStream, statSync } from "node:fs";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";

const root = process.cwd();
const port = 4180;
const types = {
  ".html": "text/html; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
};

createServer((request, response) => {
  const requestPath = decodeURIComponent(new URL(request.url, `http://${request.headers.host}`).pathname);
  let file = normalize(join(root, requestPath));
  if (!file.startsWith(root)) return response.writeHead(403).end("Forbidden");

  try {
    if (statSync(file).isDirectory()) file = join(file, "index.html");
    response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" });
    createReadStream(file).pipe(response);
  } catch {
    response.writeHead(404).end("Not found");
  }
}).listen(port, () => {
  console.log(`FotoHAVN Camera Tuning prototype: http://localhost:${port}/prototypes/event-camera-tuning/`);
  console.log("Variants: ?variant=A, ?variant=B, ?variant=C · Modes: &mode=create, &mode=edit");
});
