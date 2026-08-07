import { useEffect, useRef, useState } from "react";
import "@fontsource/inter/latin-400.css";
import "@fontsource/inter/latin-600.css";
import "@fontsource/inter/latin-700.css";
import "@fontsource/inter/latin-800.css";
import {
  ArrowExit20Regular,
  ArrowSync20Regular,
  Dismiss20Regular,
  Key20Regular,
  Play20Regular,
} from "@fluentui/react-icons";

const HOLD_DURATION_MS = 1500;
const REFERENCE_STATE = new URLSearchParams(window.location.search).get("state");

export function App() {
  const [screen, setScreen] = useState("guest");
  const [holdState, setHoldState] = useState(REFERENCE_STATE === "holding" ? "holding" : "idle");
  const [holdProgress, setHoldProgress] = useState(REFERENCE_STATE === "holding" ? 0.72 : 0);
  const [dialogState, setDialogState] = useState(REFERENCE_STATE === "confirmation" ? "confirmation" : "closed");
  const [guestAction, setGuestAction] = useState("idle");
  const holdStartedAt = useRef(0);
  const holdTimer = useRef(null);
  const animationFrame = useRef(null);
  const operatorButton = useRef(null);
  const safeAction = useRef(null);
  const dialog = useRef(null);

  const clearHold = () => {
    window.clearTimeout(holdTimer.current);
    window.cancelAnimationFrame(animationFrame.current);
    holdTimer.current = null;
    animationFrame.current = null;
  };

  const cancelHold = () => {
    clearHold();
    setHoldState("idle");
    setHoldProgress(0);
  };

  const openConfirmation = () => {
    clearHold();
    setHoldState("idle");
    setHoldProgress(0);
    setDialogState("confirmation");
  };

  const updateHoldProgress = () => {
    const elapsed = performance.now() - holdStartedAt.current;
    setHoldProgress(Math.min(elapsed / HOLD_DURATION_MS, 1));
    if (elapsed < HOLD_DURATION_MS) {
      animationFrame.current = window.requestAnimationFrame(updateHoldProgress);
    }
  };

  const beginHold = () => {
    if (holdState === "holding" || dialogState !== "closed") return;
    holdStartedAt.current = performance.now();
    setHoldState("holding");
    setHoldProgress(0);
    animationFrame.current = window.requestAnimationFrame(updateHoldProgress);
    holdTimer.current = window.setTimeout(openConfirmation, HOLD_DURATION_MS);
  };

  useEffect(() => () => clearHold(), []);

  useEffect(() => {
    if (dialogState === "confirmation") safeAction.current?.focus();
  }, [dialogState]);

  const closeConfirmation = () => {
    if (dialogState !== "confirmation") return;
    setDialogState("closed");
    window.requestAnimationFrame(() => operatorButton.current?.focus());
  };

  const confirmExit = () => {
    setDialogState("exiting");
    window.setTimeout(() => {
      setDialogState("closed");
      setScreen("saved");
    }, 1100);
  };

  const handleDialogKeyDown = (event) => {
    if (event.key === "Escape" && dialogState === "confirmation") {
      event.preventDefault();
      closeConfirmation();
      return;
    }
    if (event.key !== "Tab") return;
    const controls = [...dialog.current.querySelectorAll("button:not(:disabled)")];
    const first = controls[0];
    const last = controls.at(-1);
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  };

  const handleOperatorKeyDown = (event) => {
    if ((event.key === " " || event.key === "Enter") && !event.repeat) {
      event.preventDefault();
      beginHold();
    }
  };

  const handleOperatorKeyUp = (event) => {
    if (event.key === " " || event.key === "Enter") {
      event.preventDefault();
      if (dialogState === "closed") cancelHold();
    }
  };

  const startGuestCycle = () => {
    if (guestAction !== "idle") return;
    setGuestAction("starting");
    window.setTimeout(() => setGuestAction("idle"), 1200);
  };

  if (screen === "saved") {
    return (
      <main className="app-shell operator-console">
        <header className="app-header">
          <Brand />
          <span className="console-label">OPERATOR CONSOLE</span>
        </header>
        <section className="saved-events" aria-labelledby="saved-events-heading">
          <p className="eyebrow">FOTOHAVN</p>
          <h1 id="saved-events-heading">Saved Events</h1>
          <p>The Event is no longer active. The Camera is available for another Event.</p>
          <button className="secondary-action" type="button" onClick={() => setScreen("guest")}>
            Return to prototype
          </button>
        </section>
        <div className="completion-toast" role="status">Event exited.</div>
      </main>
    );
  }

  return (
    <main className="app-shell">
      <header className="app-header">
        <Brand />
        <button
          ref={operatorButton}
          className={`operator-access ${holdState === "holding" ? "is-holding" : ""}`}
          type="button"
          aria-label="Hold to exit Event"
          aria-describedby="operator-hold-instructions"
          onPointerDown={(event) => {
            if (event.button !== 0) return;
            event.currentTarget.setPointerCapture(event.pointerId);
            beginHold();
          }}
          onPointerUp={() => dialogState === "closed" && cancelHold()}
          onPointerCancel={cancelHold}
          onLostPointerCapture={() => dialogState === "closed" && cancelHold()}
          onKeyDown={handleOperatorKeyDown}
          onKeyUp={handleOperatorKeyUp}
          onBlur={() => dialogState === "closed" && cancelHold()}
        >
          <Key20Regular aria-hidden="true" />
          <span>{holdState === "holding" ? "Keep holding…" : "Exit Event"}</span>
          {holdState === "holding" && <ArrowSync20Regular className="hold-spinner" aria-hidden="true" />}
          <progress className="hold-progress" max="1" value={holdProgress} aria-label="Exit Event hold progress" />
        </button>
        <span id="operator-hold-instructions" className="sr-only">
          Press and hold for one and a half seconds to open the Exit Event confirmation.
        </span>
      </header>

      <section className="guest-start" aria-labelledby="guest-heading">
        <p className="event-name">UX AUDIT TEST EVENT</p>
        <h1 id="guest-heading">Let’s take some photos.</h1>
        <p className="guest-copy">Four Captures. A quick countdown before each one.</p>
        <button className="start-button" type="button" onClick={startGuestCycle} disabled={guestAction !== "idle"}>
          {guestAction === "starting" ? (
            <><ArrowSync20Regular className="busy-icon" aria-hidden="true" /> Starting…</>
          ) : (
            <><Play20Regular aria-hidden="true" /> Touch to start</>
          )}
        </button>
      </section>

      <p className="privacy-note">Photos stay with this Event.</p>

      {dialogState !== "closed" && (
        <div className="dialog-backdrop">
          <section
            ref={dialog}
            className="confirmation-dialog"
            role="dialog"
            aria-modal="true"
            aria-labelledby="exit-dialog-title"
            aria-describedby="exit-dialog-description"
            onKeyDown={handleDialogKeyDown}
          >
            <button className="dialog-close" type="button" aria-label="Close" onClick={closeConfirmation} disabled={dialogState === "exiting"}>
              <Dismiss20Regular aria-hidden="true" />
            </button>
            <h2 id="exit-dialog-title">Exit Event?</h2>
            <p id="exit-dialog-description">The Camera will be released and FotoHAVN will return to Saved Events.</p>
            <div className="dialog-actions">
              <button ref={safeAction} className="secondary-action" type="button" onClick={closeConfirmation} disabled={dialogState === "exiting"}>
                Keep Event Active
              </button>
              <button className="danger-action" type="button" onClick={confirmExit} disabled={dialogState === "exiting"}>
                {dialogState === "exiting" ? (
                  <><ArrowSync20Regular className="busy-icon" aria-hidden="true" /> Exiting Event…</>
                ) : (
                  <><ArrowExit20Regular aria-hidden="true" /> Exit Event</>
                )}
              </button>
            </div>
          </section>
        </div>
      )}

      <div className="sr-only" role="status" aria-live="polite">
        {holdState === "holding" ? `Keep holding. ${Math.round(holdProgress * 100)} percent complete.` : ""}
      </div>
    </main>
  );
}

function Brand() {
  return (
    <div className="brand" aria-label="FotoHAVN">
      <span className="brand-mark" aria-hidden="true">F</span>
      <span>FotoHAVN</span>
    </div>
  );
}
