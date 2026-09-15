// Local QA only: hold/fail image requests without changing production assets.
// Run after starting the production app on 3002, then inspect localhost:3003/FOTOHAVN.
import http from "node:http";

const held = new Set();
const target = process.env.HERO_QA_IMAGE ?? "exterior";
if (!["exterior", "interior"].includes(target)) {
  throw new Error("HERO_QA_IMAGE must be exterior or interior");
}
let mode = "hold";

function fail(response) {
  response.writeHead(404, { "Cache-Control": "no-store" });
  response.end("Deliberate local hero-image failure");
}

const server = http.createServer((request, response) => {
  const url = new URL(request.url, "http://localhost:3003");
  if (url.pathname === "/__qa/fail-image") {
    mode = "fail";
    for (const pending of held) fail(pending);
    held.clear();
    response.end("Held image requests failed.");
    return;
  }
  if (url.pathname === "/__qa/restore-image") {
    mode = "pass";
    response.end("Image requests restored for the next retry.");
    return;
  }
  if (
    mode !== "pass" &&
    url.pathname.startsWith(`/FOTOHAVN/images/hero/${target}`)
  ) {
    if (mode === "fail") fail(response);
    else {
      held.add(response);
      response.on("close", () => held.delete(response));
    }
    return;
  }
  const upstream = http.request(
    {
      hostname: "localhost",
      port: 3002,
      path: request.url,
      method: request.method,
      headers: { ...request.headers, host: "localhost:3002" },
    },
    (result) => {
      response.writeHead(result.statusCode ?? 502, {
        ...result.headers,
        "cache-control": "no-store",
      });
      result.pipe(response);
    },
  );
  upstream.on("error", () => {
    if (!response.headersSent) response.writeHead(502);
    response.end("Start the production app on localhost:3002 first.");
  });
  request.pipe(upstream);
});

server.listen(3003, "localhost", () => {
  console.log(`Hero QA proxy on localhost:3003; holding ${target} images.`);
});
