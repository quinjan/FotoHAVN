import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app/App.tsx";
import { createBrainClient, exposeE2EHandle } from "./brain/createBrainClient.ts";
import { env } from "./env.ts";
import "./styles/global.css";

/**
 * The fixture lane, only in development and E2E builds. Vite replaces both conditions at build time, so in a field
 * build the branch and its dynamic import are dropped and dist/ holds no fixture code or data.
 */
async function loadFixtureLane(): Promise<typeof import("./fixtures/fixtureLane.ts") | null> {
  if (import.meta.env.DEV || import.meta.env.VITE_E2E === "true") {
    return await import("./fixtures/fixtureLane.ts");
  }
  return null;
}

/**
 * The fixture gallery (`?gallery`, `?gallery=<fixture id>`, `?gallery=manifest`), only in development and E2E builds,
 * behind the same build-time guard: field builds contain no gallery or component fixture code.
 */
async function loadGallery(): Promise<{ readonly mount: (root: HTMLElement) => void } | null> {
  if (import.meta.env.DEV || import.meta.env.VITE_E2E === "true") {
    const selection = new URLSearchParams(window.location.search).get("gallery");
    if (selection !== null) {
      const { mountGallery } = await import("./gallery/mountGallery.tsx");
      return { mount: (root) => { mountGallery(root, selection); } };
    }
  }
  return null;
}

async function start(): Promise<void> {
  const root = document.getElementById("root");
  if (root === null) {
    throw new Error("FOTOHAVN: #root element is missing from index.html.");
  }

  const gallery = await loadGallery();
  if (gallery !== null) {
    gallery.mount(root);
    return;
  }

  const lane = await loadFixtureLane();

  // Created and connected once, outside React, so StrictMode's double effects can't send a second hello.
  const client = createBrainClient({
    target: window,
    env,
    ...(lane === null ? {} : { fixtures: lane.createFixtureLaneTransport }),
  });
  client.connect();
  exposeE2EHandle(window, client, env);

  createRoot(root).render(
    <StrictMode>
      <App client={client} fixtureNames={lane?.fixtureNames ?? []} />
    </StrictMode>,
  );
}

void start();
