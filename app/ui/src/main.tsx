import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./app/App.tsx";
import { createBrainClient, exposeE2EHandle } from "./brain/createBrainClient.ts";
import { env } from "./env.ts";
import { inlineFixtureNames } from "./fixtures/inlineFixtures.ts";
import "./styles/global.css";

const root = document.getElementById("root");
if (root === null) {
  throw new Error("FOTOHAVN: #root element is missing from index.html.");
}

// Created and connected once, outside React, so StrictMode's double effects can't send a second hello.
const client = createBrainClient({ target: window, env });
client.connect();
exposeE2EHandle(window, client, env);

createRoot(root).render(
  <StrictMode>
    <App client={client} fixtureNames={env.dev || env.e2e ? inlineFixtureNames : []} />
  </StrictMode>,
);
