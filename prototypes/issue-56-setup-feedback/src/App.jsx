import { useEffect, useRef, useState } from "react";
import {
  ArrowLeft20Regular,
  ArrowRight20Regular,
  Camera20Regular,
  CheckmarkCircle20Regular,
  Delete20Regular,
  DismissCircle20Regular,
  Edit20Regular,
  Power20Regular,
  Play20Regular,
  Print20Regular,
  SpinnerIos20Regular,
  Warning20Regular,
} from "@fluentui/react-icons";

const scenarios = ["open", "save", "start", "storage", "exit", "delete"];
const scenarioNames = {
  open: "Open",
  save: "Save",
  start: "Start",
  storage: "Storage",
  exit: "Exit",
  delete: "Delete",
};

const wait = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));

function readScenario() {
  const value = new URLSearchParams(window.location.search).get("scenario");
  return scenarios.includes(value) ? value : "save";
}

function AppHeader({ activeEvent = false, onExit, exitButtonRef }) {
  return (
    <header className="app-header">
      <div className="brand"><span className="brand-mark">F</span><strong>FotoHAVN</strong></div>
      {activeEvent ? (
        <button ref={exitButtonRef} className="header-action" onClick={onExit}><Power20Regular /> Exit Event</button>
      ) : (
        <span className="console-label">OPERATOR CONSOLE</span>
      )}
    </header>
  );
}

function AccessibleDialog({ labelledBy, onDismiss, returnFocusRef, children }) {
  const dialogRef = useRef(null);

  useEffect(() => {
    const previousFocus = document.activeElement;
    const dialog = dialogRef.current;
    const prototypeSwitcher = document.querySelector(".prototype-switcher");
    prototypeSwitcher?.setAttribute("inert", "");
    prototypeSwitcher?.setAttribute("aria-hidden", "true");
    const focusables = dialog?.querySelectorAll('button:not(:disabled), input:not(:disabled), select:not(:disabled), [tabindex]:not([tabindex="-1"])');
    const preferred = dialog?.querySelector("[data-autofocus]") ?? focusables?.[0];
    preferred?.focus();

    return () => {
      prototypeSwitcher?.removeAttribute("inert");
      prototypeSwitcher?.removeAttribute("aria-hidden");
      if (returnFocusRef?.current?.isConnected) returnFocusRef.current.focus();
      else if (previousFocus?.isConnected && previousFocus !== document.body) previousFocus.focus();
      else document.querySelector("h1")?.focus();
    };
  }, []);

  function handleKeyDown(event) {
    if (event.key === "Escape") {
      event.preventDefault();
      onDismiss();
      return;
    }

    if (event.key !== "Tab") return;
    const focusables = [...dialogRef.current.querySelectorAll('button:not(:disabled), input:not(:disabled), select:not(:disabled), [tabindex]:not([tabindex="-1"])')];
    if (focusables.length === 0) return;
    event.preventDefault();
    const currentIndex = focusables.indexOf(document.activeElement);
    const direction = event.shiftKey ? -1 : 1;
    const nextIndex = (currentIndex + direction + focusables.length) % focusables.length;
    focusables[nextIndex].focus();
  }

  return (
    <div className="modal-layer">
      <section ref={dialogRef} className="confirmation" role="dialog" aria-modal="true" aria-labelledby={labelledBy} onKeyDown={handleKeyDown}>
        {children}
      </section>
    </div>
  );
}

function StatusIcon({ kind }) {
  if (kind === "checking") return <SpinnerIos20Regular className="status-icon spinner" />;
  if (kind === "warning") return <Warning20Regular className="status-icon warning" />;
  if (kind === "error") return <DismissCircle20Regular className="status-icon error" />;
  return null;
}

function FieldMessage({ kind, children }) {
  return (
    <div className={`field-message ${kind}`} role={kind === "error" ? "alert" : undefined}>
      <StatusIcon kind={kind} />
      <span>{children}</span>
    </div>
  );
}

function InlineFeedback({ state, action, message, onRetry, onChooseCamera, retryRef }) {
  if (state === "idle") return <p className="action-hint">{message}</p>;
  if (state === "busy") {
    return <div className="action-feedback busy" role="status"><SpinnerIos20Regular className="spinner" /> <span>{message}</span></div>;
  }
  if (state === "success") {
    return <div className="action-feedback success" role="status"><CheckmarkCircle20Regular /> <span>{message}</span></div>;
  }
  return (
    <div className="action-feedback error" role="alert">
      <DismissCircle20Regular />
      <div><strong>{message}</strong><span>Your Event details are still here.</span></div>
      {action === "start" && <button className="text-action" onClick={onChooseCamera}>Choose another Camera</button>}
      <button ref={retryRef} className="outline-action compact" onClick={onRetry}>Try Again</button>
    </div>
  );
}

function SetupScreen({ initialAction = "save", initialCamera = "", insufficientStorage = false, onCancel, onSaved }) {
  const [eventName, setEventName] = useState("UX Audit Test Event");
  const [camera, setCamera] = useState(initialCamera);
  const [cameraCheck, setCameraCheck] = useState(initialCamera ? "checking" : "idle");
  const [printer, setPrinter] = useState("none");
  const [state, setState] = useState("idle");
  const [action, setAction] = useState(initialAction);
  const cameraRef = useRef(null);

  useEffect(() => {
    setAction(initialAction);
    setState("idle");
  }, [initialAction]);

  useEffect(() => {
    cameraRef.current?.focus({ preventScroll: true });
  }, []);

  useEffect(() => {
    if (!camera) {
      setCameraCheck("idle");
      return undefined;
    }

    setCameraCheck("checking");
    setState("idle");
    const timer = window.setTimeout(() => {
      setCameraCheck(camera === "unavailable" ? "error" : "ready");
    }, 450);
    return () => window.clearTimeout(timer);
  }, [camera]);

  const nameReady = eventName.trim().length > 0;
  const cameraReady = cameraCheck === "ready";
  const storageReady = !insufficientStorage;
  const ready = nameReady && cameraReady && storageReady;

  async function runAction(nextAction = action) {
    setAction(nextAction);
    if (!ready) return;
    setState("busy");
    await wait(1100);
    setState("success");
    if (nextAction === "save") {
      await wait(650);
      onSaved();
    } else {
      await wait(650);
      setState("idle");
    }
  }

  return (
    <div className="app-shell setup-shell">
      <AppHeader />
      <div className="scrim">
        <section className={`setup-dialog ${state === "error" ? "has-error" : ""} ${insufficientStorage ? "has-storage-error" : ""}`} aria-labelledby="setup-title">
          <div className="setup-heading">
            <span className="eyebrow">EVENT SETUP</span>
            <h1 id="setup-title">New Event</h1>
            <p>Name the Event and choose its Camera and Printer.</p>
          </div>

          <div className="setup-content">
            <div className="form-section">
              <div className="field-row">
                <label htmlFor="event-name">Event name</label>
                <input id="event-name" value={eventName} aria-describedby={!nameReady ? "event-name-message" : undefined} onChange={(event) => { setEventName(event.target.value); setState("idle"); }} />
                {!nameReady && <div id="event-name-message"><FieldMessage kind="warning">Enter an Event name to continue.</FieldMessage></div>}
              </div>

              <div className="field-row">
                <label htmlFor="camera">Camera</label>
                <select ref={cameraRef} id="camera" value={camera} aria-describedby={cameraCheck !== "ready" ? "camera-message" : undefined} onChange={(event) => setCamera(event.target.value)}>
                  <option value="">Select Camera</option>
                  <option value="ready">FJ Camera 01 (3:2)</option>
                  <option value="unavailable">FJ Camera 02 (unavailable)</option>
                </select>
                {cameraCheck === "checking" ? (
                  <div id="camera-message"><FieldMessage kind="checking">Checking Camera…</FieldMessage></div>
                ) : cameraCheck === "error" ? (
                  <div id="camera-message"><FieldMessage kind="error">Camera unavailable. Choose another Camera or try again.</FieldMessage></div>
                ) : !camera ? (
                  <div id="camera-message"><FieldMessage kind="warning">Select a Camera to continue.</FieldMessage></div>
                ) : null}
              </div>

              <div className="field-row">
                <label htmlFor="printer">Printer <span>(optional)</span></label>
                <select id="printer" value={printer} onChange={(event) => setPrinter(event.target.value)}>
                  <option value="none">Not printing</option>
                  <option value="dnp">DNP DS620</option>
                </select>
              </div>

              <div className="field-row storage-row">
                <span className="field-label">Storage</span>
                <div className="storage-details">
                  <span className="storage-path">C:\Program Files\FotoHAVN\Events</span>
                  <span className="storage-value">{insufficientStorage ? "480 MB free" : "120 GB free"}</span>
                </div>
                {insufficientStorage && <FieldMessage kind="error">Not enough space. Free up at least 1 GB to continue.</FieldMessage>}
              </div>
            </div>

            <div className="preview-section">
              <h2>Live preview</h2>
              <div className={`preview-viewport ${cameraReady ? "active" : ""}`}>
                {cameraReady ? <Camera20Regular className="preview-camera" /> : <span>{cameraCheck === "checking" ? "Checking Camera…" : cameraCheck === "error" ? "Preview unavailable. Choose another Camera or try again." : "Select a Camera to start the preview."}</span>}
                <small>3:2 Capture area</small>
              </div>
            </div>
          </div>

          <footer className="setup-footer">
            <button className="text-action" onClick={onCancel}>Cancel</button>
            <div className="footer-actions">
              <div className="button-row">
                <button className="outline-action" disabled={!ready || state === "busy"} onClick={() => runAction("save")}>{state === "busy" && action === "save" && <SpinnerIos20Regular className="spinner" />}{state === "busy" && action === "save" ? "Saving Event…" : "Save & Close"}</button>
                <button className="primary-action" disabled={!ready || state === "busy"} onClick={() => runAction("start")}>
                  {state === "busy" && action === "start" && <SpinnerIos20Regular className="spinner" />}
                  {state === "busy" && action === "start" ? "Starting Event…" : state === "success" && action === "start" ? "Event Ready" : "Save & Start Event"}
                </button>
              </div>
            </div>
          </footer>
        </section>
      </div>
    </div>
  );
}

function SavedEventsScreen({ mode, notice = "", onOpenSetup, onStartEvent }) {
  const [dialog, setDialog] = useState(mode === "delete");
  const [state, setState] = useState("idle");
  const [deleted, setDeleted] = useState(false);
  const [toast, setToast] = useState(notice);
  const deleteTriggerRef = useRef(null);
  const toastRef = useRef(null);

  useEffect(() => {
    setDialog(mode === "delete");
    setState("idle");
    setDeleted(false);
    setToast(notice);
  }, [mode, notice]);

  useEffect(() => {
    if (toast) toastRef.current?.focus();
  }, [toast]);

  async function openSetup() {
    setState("busy");
    await wait(1100);
    onOpenSetup();
  }

  async function deleteEvent() {
    setState("busy");
    await wait(1100);
    setState("success");
    await wait(650);
    setDeleted(true);
    setDialog(false);
    setState("idle");
    setToast("Event deleted.");
    requestAnimationFrame(() => toastRef.current?.focus());
  }

  return (
    <div className="app-shell">
      <div className="modal-background" aria-hidden={dialog ? "true" : undefined} inert={dialog ? true : undefined}>
        <AppHeader />
        <main className="saved-events">
          <span className="eyebrow">SAVED EVENTS</span>
          <h1 tabIndex="-1">Choose an Event</h1>
          <p>Open one to adjust its setup, or start a Guest Cycle.</p>
          <div className="event-grid">
            <button className="new-event-card" aria-label="New Event" onClick={openSetup} disabled={state === "busy"}>
              {state === "busy" ? <SpinnerIos20Regular className="spinner large" /> : <span className="plus">+</span>}
              <strong>{state === "busy" ? "Opening Event setup…" : "New Event"}</strong>
              <span>{state === "busy" ? "Please wait" : "Set up a new booth run"}</span>
            </button>
            {!deleted && (
              <article className="event-card">
                <div className="event-card-actions">
                  <button aria-label="Edit Event" onClick={onOpenSetup}><Edit20Regular /></button>
                  <button ref={deleteTriggerRef} aria-label="Delete Event" onClick={() => setDialog(true)}><Delete20Regular /></button>
                </div>
                <div className="event-card-copy"><strong>UX Audit Test Event</strong><span>Saved today, 3:49 AM</span></div>
                <div className="event-readiness"><CheckmarkCircle20Regular /><span>Camera ready · Not printing · Storage ready</span></div>
                <button className="event-start" onClick={onStartEvent}><Play20Regular /> Start Event</button>
              </article>
            )}
          </div>
          {toast && <div ref={toastRef} className="toast" role="status" tabIndex="-1"><CheckmarkCircle20Regular /> {toast}</div>}
        </main>
      </div>
      {dialog && (
        <AccessibleDialog labelledBy="delete-title" onDismiss={() => state !== "busy" && setDialog(false)} returnFocusRef={deleteTriggerRef}>
          <span className="danger-icon"><Delete20Regular /></span>
          <h2 id="delete-title">Delete “UX Audit Test Event”?</h2>
          <p>This permanently deletes the Event, its Guest Cycles, and saved photos.</p>
          <div className="identity-panel"><strong>UX Audit Test Event</strong><span>Event 01JZ-7M2K</span></div>
          <InlineFeedback state={state} action="delete" message={state === "busy" ? "Deleting Event…" : state === "success" ? "Event deleted." : "This action cannot be undone."} onRetry={deleteEvent} />
          <div className="button-row"><button data-autofocus className="outline-action" disabled={state === "busy"} onClick={() => setDialog(false)}>Cancel</button><button className="danger-action" disabled={state === "busy" || state === "success"} onClick={deleteEvent}>{state === "busy" && <SpinnerIos20Regular className="spinner" />}{state === "busy" ? "Deleting Event…" : "Delete Event"}</button></div>
        </AccessibleDialog>
      )}
    </div>
  );
}

function ActiveEventScreen({ onReturn }) {
  const [dialog, setDialog] = useState(true);
  const [state, setState] = useState("idle");
  const exitTriggerRef = useRef(null);

  async function exitEvent() {
    setState("busy");
    await wait(1100);
    setState("success");
    await wait(650);
    onReturn();
  }

  return (
    <div className="app-shell active-event">
      <div className="modal-background" aria-hidden={dialog ? "true" : undefined} inert={dialog ? true : undefined}>
        <AppHeader activeEvent onExit={() => setDialog(true)} exitButtonRef={exitTriggerRef} />
        <main className="guest-start"><span className="eyebrow">UX AUDIT TEST EVENT</span><h1 tabIndex="-1">Let’s take some photos.</h1><p>We’ll take four photos, with a short countdown before each one.</p><button className="guest-action">Touch to start</button></main>
      </div>
      {dialog && (
        <AccessibleDialog labelledBy="exit-title" onDismiss={() => state !== "busy" && setDialog(false)} returnFocusRef={exitTriggerRef}>
          <span className="neutral-icon"><Power20Regular /></span>
          <h2 id="exit-title">Exit Event?</h2>
          <p>The Camera will be released and FotoHAVN will return to Saved Events.</p>
          <div className="button-row"><button data-autofocus className="outline-action" disabled={state === "busy"} onClick={() => setDialog(false)}>Keep Event Active</button><button className="danger-action" disabled={state === "busy"} onClick={exitEvent}>{state === "busy" && <SpinnerIos20Regular className="spinner" />}{state === "busy" ? "Exiting Event…" : "Exit Event"}</button></div>
        </AccessibleDialog>
      )}
    </div>
  );
}

function PrototypeSwitcher({ scenario, onChange }) {
  const index = scenarios.indexOf(scenario);
  const cycle = (direction) => onChange(scenarios[(index + direction + scenarios.length) % scenarios.length]);
  return (
    <nav className="prototype-switcher" aria-label="Prototype action scenario">
      <button aria-label="Previous scenario" onClick={() => cycle(-1)}><ArrowLeft20Regular /></button>
      <span><small>PROTOTYPE SCENARIO</small><strong>{scenarioNames[scenario]}</strong></span>
      <button aria-label="Next scenario" onClick={() => cycle(1)}><ArrowRight20Regular /></button>
    </nav>
  );
}

export function App() {
  const [scenario, setScenario] = useState(readScenario);
  const [notice, setNotice] = useState("");

  function changeScenario(next, nextNotice = "") {
    const url = new URL(window.location.href);
    url.searchParams.set("scenario", next);
    window.history.replaceState({}, "", url);
    setScenario(next);
    setNotice(nextNotice);
  }

  useEffect(() => {
    const onKeyDown = (event) => {
      if (document.querySelector('[role="dialog"]')) return;
      const tag = document.activeElement?.tagName;
      if (["INPUT", "TEXTAREA", "SELECT"].includes(tag) || document.activeElement?.isContentEditable) return;
      if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        const index = scenarios.indexOf(scenario);
        const direction = event.key === "ArrowLeft" ? -1 : 1;
        changeScenario(scenarios[(index + direction + scenarios.length) % scenarios.length]);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [scenario]);

  return (
    <>
      {(scenario === "save" || scenario === "start" || scenario === "storage") && <SetupScreen key={scenario} initialAction={scenario === "start" ? "start" : "save"} initialCamera={scenario === "storage" ? "ready" : ""} insufficientStorage={scenario === "storage"} onCancel={() => changeScenario("open")} onSaved={() => changeScenario("open", "Event saved.")} />}
      {(scenario === "open" || scenario === "delete") && <SavedEventsScreen mode={scenario} notice={notice} onOpenSetup={() => changeScenario("save")} onStartEvent={() => changeScenario("start")} />}
      {scenario === "exit" && <ActiveEventScreen onReturn={() => changeScenario("open")} />}
      {import.meta.env.DEV && <PrototypeSwitcher scenario={scenario} onChange={changeScenario} />}
    </>
  );
}
