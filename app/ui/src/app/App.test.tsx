import { StrictMode } from "react";
import { act, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { ProtocolBrainClient } from "../brain/protocolClient.ts";
import { guestStart, operatorEventListEmpty, operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import type { OperatorPresentation } from "../protocol/generated.ts";
import { ManualTransport } from "../test/manualTransport.ts";
import { App } from "./App.tsx";

function renderApp(options: { strict?: boolean; fixtureNames?: readonly string[] } = {}) {
  const transport = new ManualTransport();
  const client = new ProtocolBrainClient(transport);
  client.connect();
  const app = <App client={client} {...(options.fixtureNames === undefined ? {} : { fixtureNames: options.fixtureNames })} />;
  render(options.strict === true ? <StrictMode>{app}</StrictMode> : app);
  const deliver = (message: unknown) => {
    act(() => {
      transport.receive(message);
    });
  };
  return { transport, client, deliver };
}

const root = document.documentElement;

describe("App root routing", () => {
  it("shows the connecting screen until welcome, without a role", () => {
    const { transport } = renderApp();
    act(() => {
      transport.open();
    });
    expect(screen.getByRole("main")).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("heading", { level: 1, name: "FOTOHAVN" })).toBeVisible();
    expect(root.dataset.fotohavnStatus).toBe("connecting");
    expect(root.dataset.fotohavnRole).toBeUndefined();
  });

  it("routes an operator welcome to the operator surfaces and marks the role and revision", () => {
    const { transport, deliver } = renderApp({ strict: true });
    act(() => {
      transport.open();
    });
    deliver(operatorEventListEmpty);

    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getByText("No Events yet.")).toBeVisible();
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "eventList");
    expect(root.dataset).toMatchObject({ fotohavnStatus: "connected", fotohavnRole: "operator", fotohavnRevision: "1" });
    expect(transport.sent.filter((message) => message.type === "hello")).toHaveLength(1);
  });

  it("routes a guest welcome to the guest surfaces", () => {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver(guestStart);

    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
    expect(screen.queryByRole("button", { name: "NEW EVENT" })).not.toBeInTheDocument();
    expect(root.dataset).toMatchObject({ fotohavnRole: "guest", fotohavnRevision: "1" });
  });

  it("updates the revision mark after each accepted snapshot and ignores stale ones", () => {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver(operatorEventListEmpty);
    deliver({ type: "presentation", role: "operator", revision: 2, presentation: operatorEventListWithRows.presentation });
    expect(root.dataset.fotohavnRevision).toBe("2");
    expect(screen.getByRole("heading", { level: 2, name: "Mara's 30th" })).toBeVisible();

    deliver({ type: "presentation", role: "operator", revision: 2, presentation: operatorEventListEmpty.presentation });
    expect(root.dataset.fotohavnRevision).toBe("2");
    expect(screen.queryByText("No Events yet.")).not.toBeInTheDocument();
  });

  it("keeps the last snapshot and announces reconnecting while the connection is down", () => {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver(guestStart);
    act(() => {
      transport.close();
    });

    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByText("Reconnecting to the booth.")).toBeVisible();
    expect(root.dataset).toMatchObject({ fotohavnStatus: "reconnecting", fotohavnRole: "guest" });

    act(() => {
      transport.open();
    });
    deliver(guestStart);
    expect(screen.queryByText("Reconnecting to the booth.")).not.toBeInTheDocument();
  });

  it("shows the fixed protocol mismatch screen and clears the role", () => {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver(operatorEventListEmpty);
    deliver({ type: "protocolMismatch", brainVersion: 1, uiVersion: 0 });

    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("data-fotohavn-screen", "protocolMismatch");
    expect(screen.getByRole("heading", { level: 1, name: "A little mix-up." })).toBeVisible();
    expect(main).toHaveTextContent("This screen and the booth are out of step. Restart FOTOHAVN to fix it.");
    expect(main).toHaveTextContent("Booth version 1 · Screen version 0");
    expect(root.dataset.fotohavnStatus).toBe("protocolMismatch");
    expect(root.dataset.fotohavnRole).toBeUndefined();
    expect(root.dataset.fotohavnRevision).toBeUndefined();
  });

  it("shows the not-connected screen without a bridge, listing fixtures only when given", () => {
    const client = new ProtocolBrainClient(null);
    const { unmount } = render(<App client={client} />);
    expect(screen.getByRole("heading", { level: 1, name: "Not connected." })).toBeVisible();
    expect(screen.queryByRole("navigation")).not.toBeInTheDocument();
    expect(root.dataset.fotohavnStatus).toBe("notConnected");
    unmount();

    render(<App client={client} fixtureNames={["guest-start"]} />);
    expect(screen.getByRole("link", { name: "guest-start" })).toHaveAttribute("href", "?fixture=guest-start");
  });
});

describe("Operator surfaces", () => {
  function operator(presentation: Partial<OperatorPresentation>) {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver({ ...operatorEventListEmpty, presentation: { ...operatorEventListEmpty.presentation, ...presentation } });
  }

  it("renders the fatal surface with the brain's reason", () => {
    operator({ surface: "fatal", eventList: null, fatal: { reason: "The Data folder can't be written." } });
    expect(screen.getByRole("heading", { level: 1, name: "Something needs attention." })).toBeVisible();
    expect(screen.getByText("The Data folder can't be written.")).toBeVisible();
  });

  it("renders the fatal surface without a reason", () => {
    operator({ surface: "fatal", eventList: null, fatal: null });
    expect(screen.getByRole("heading", { level: 1, name: "Something needs attention." })).toBeVisible();
  });

  it("renders a placeholder for surfaces later milestones build", () => {
    operator({ surface: "eventSetup", eventList: null });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "eventSetup");
    expect(screen.getByText("This view isn't ready yet.")).toBeVisible();
  });

  it("renders the starting surface until the brain answers, with no placeholder", async () => {
    operator({ surface: "starting", eventList: null });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "starting");
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(await screen.findByText("Opening the Data folder…")).toBeVisible();
    expect(screen.queryByText("This view isn't ready yet.")).not.toBeInTheDocument();
  });

  it("treats a missing event list as empty", () => {
    operator({ eventList: null });
    expect(screen.getByText("No Events yet.")).toBeVisible();
  });

  it("renders the storage error surface from the presentation and sends RetryStorage from TRY AGAIN", async () => {
    const { transport, deliver } = renderApp();
    act(() => {
      transport.open();
    });
    deliver({
      ...operatorEventListEmpty,
      presentation: {
        ...operatorEventListEmpty.presentation,
        surface: "storageError",
        eventList: null,
        storageError: { kind: "accessDenied", dataPath: "D:\\FOTOHAVN\\Data" },
      },
    });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "storageError");
    expect(screen.getByRole("heading", { level: 1, name: "The Data folder needs attention." })).toBeVisible();
    expect(await screen.findByText("FOTOHAVN can't write to the Data folder. Check its permissions, then try again.")).toBeVisible();
    expect(screen.getByText("D:\\FOTOHAVN\\Data")).toBeVisible();

    act(() => {
      screen.getByRole("button", { name: "TRY AGAIN" }).click();
    });
    expect(transport.sent).toContainEqual(expect.objectContaining({ type: "command", name: "RetryStorage", payload: {} }));
  });

  it("falls back to the placeholder when a storage error surface carries no error", () => {
    operator({ surface: "storageError", eventList: null, storageError: null });
    expect(screen.getByText("This view isn't ready yet.")).toBeVisible();
  });

  it("renders the camera diagnostics surface from the presentation, read-only outside an E2E build", () => {
    operator({
      surface: "cameraDiagnostics",
      eventList: null,
      cameraDiagnostics: {
        cameras: [{ deviceId: "fake-camera", name: "Fake camera" }],
        selectedDeviceId: "fake-camera",
        state: "notAvailable",
        reason: "privacyDenied",
        format: null,
        mirrored: false,
        lastCapture: null,
        lastComposition: null,
      },
    });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "cameraDiagnostics");
    expect(screen.getByText("CAMERA DIAGNOSTICS")).toBeVisible();
    expect(screen.getByText("Not available. Camera access is off in Windows privacy settings.")).toBeVisible();
    expect(screen.queryByRole("button", { name: "CAPTURE" })).not.toBeInTheDocument();
  });

  it("falls back to the placeholder when a camera diagnostics surface carries no view", () => {
    operator({ surface: "cameraDiagnostics", eventList: null, cameraDiagnostics: null });
    expect(screen.getByText("This view isn't ready yet.")).toBeVisible();
  });

  it("shows the brain's notice", () => {
    operator({ overlays: { confirmation: null, identifyDisplays: null, notice: { code: "saveFailed" } } });
    expect(screen.getAllByRole("status").map((status) => status.textContent)).toContain("The Event couldn't be saved.");
  });
});
