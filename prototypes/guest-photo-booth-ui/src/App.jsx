import React, { useEffect, useMemo, useRef, useState } from "react";
import {
  ArrowLeft,
  ArrowRight,
  Camera,
  Check,
  CircleNotch,
  HardDrive,
  Play,
  Power,
  WarningCircle,
} from "@phosphor-icons/react";

const variants = [
  { key: "A", name: "Quiet stage" },
  { key: "B", name: "Guided split" },
  { key: "C", name: "Full-bleed booth" },
];

const captures = ["peach", "blue", "gold", "mint"];
const acceleratedSecond = 650;
const captureCountdownSeconds = 5;
const photoStripPreviewSeconds = 10;

function getVariant() {
  const value = new URLSearchParams(window.location.search).get("variant")?.toUpperCase();
  return variants.some(({ key }) => key === value) ? value : "A";
}

function useTimeout(callback, delay, deps = []) {
  const callbackRef = useRef(callback);
  callbackRef.current = callback;
  useEffect(() => {
    if (delay === null) return undefined;
    const timer = window.setTimeout(() => callbackRef.current(), delay);
    return () => window.clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [delay, ...deps]);
}

function Brand() {
  return <span className="brand"><span aria-hidden="true">F</span>FotoHAVN</span>;
}

function ExitEventButton({ onExit }) {
  return <button className="exit-event-button" onClick={onExit}><Power />Exit Event</button>;
}

function StartUnavailableScreen({ kind, onRetry, onExit }) {
  const cameraIssue = kind === "camera";
  return (
    <main className="start-unavailable-screen">
      <header><Brand /><ExitEventButton onExit={onExit} /></header>
      <section role="alert">
        <span className="warning-mark"><WarningCircle weight="fill" /></span>
        <p className="event-label">Mika &amp; Paolo's Wedding</p>
        <h1>Please call the operator.</h1>
        <p>{cameraIssue ? "The Camera is disconnected, so photos can’t start yet." : "Storage is unavailable, so FotoHAVN can’t save Captures right now."}</p>
        <div className="operator-detail"><span>{cameraIssue ? <Camera /> : <HardDrive />}</span><div><b>{cameraIssue ? "Camera disconnected" : "Storage unavailable"}</b><small>Start unavailable · no photos have begun</small></div></div>
        <button className="retry-button" onClick={onRetry}><CircleNotch />Retry</button>
      </section>
    </main>
  );
}

function ProgressDots({ completed, active = false }) {
  return (
    <ol className="capture-progress" aria-label={`${completed} of 4 Captures complete`}>
      {captures.map((_, index) => (
        <li key={index} className={index < completed ? "complete" : active && index === completed ? "active" : ""}>
          <span>{index < completed ? <Check weight="bold" /> : index + 1}</span>
          <span className="sr-only">Capture {index + 1}{index < completed ? " complete" : ""}</span>
        </li>
      ))}
    </ol>
  );
}

function CameraScene({ children, className = "" }) {
  return (
    <div className={`camera-scene ${className}`}>
      <div className="scene-wall" />
      <div className="scene-person person-left"><span /></div>
      <div className="scene-person person-right"><span /></div>
      <div className="scene-plant" aria-hidden="true">✦</div>
      {children}
    </div>
  );
}

function PhotoStrip() {
  return (
    <figure className="photo-strip" aria-label="Your four-Capture Photo Strip">
      {captures.map((tone, index) => (
        <div className={`strip-frame ${tone}`} key={tone}>
          <div className="mini-person one" /><div className="mini-person two" />
          <span>{index + 1}</span>
        </div>
      ))}
      <figcaption>MIKA &amp; PAOLO'S WEDDING</figcaption>
    </figure>
  );
}

function StartScreen({ variant, onStart, onExit }) {
  if (variant === "B") {
    return (
      <main className="start-screen start-b">
        <header className="start-utility"><Brand /><ExitEventButton onExit={onExit} /></header>
        <section className="start-copy">
          <p className="event-label">Mika &amp; Paolo's Wedding</p>
          <h1>Four photos.<br />One good time.</h1>
          <p>Look at the Camera. We’ll count you in before every Capture.</p>
          <button className="start-button" onClick={onStart}><Play weight="fill" />Start photos</button>
        </section>
        <CameraScene className="start-camera"><div className="camera-tag"><Camera />Live · Mirrored</div></CameraScene>
      </main>
    );
  }

  if (variant === "C") {
    return (
      <main className="start-screen start-c">
        <CameraScene className="start-camera">
          <header><Brand /><ExitEventButton onExit={onExit} /></header>
          <div className="start-overlay">
            <p className="event-label">Mika &amp; Paolo's Wedding</p>
            <h1>Ready when you are.</h1>
            <p>Four photos. We’ll count you in.</p>
            <button className="start-button light" onClick={onStart}><Play weight="fill" />Start</button>
          </div>
          <div className="camera-tag dark"><Camera />Live · Mirrored</div>
        </CameraScene>
      </main>
    );
  }

  return (
    <main className="start-screen start-a">
      <header><Brand /><ExitEventButton onExit={onExit} /></header>
      <section>
        <p className="event-label">Mika &amp; Paolo's Wedding</p>
        <h1>Let’s take some photos.</h1>
        <p>Four Captures. A quick countdown before each one.</p>
        <button className="start-button" onClick={onStart}><Play weight="fill" />Touch to start</button>
      </section>
      <p className="privacy-note">Photos stay with this Event.</p>
    </main>
  );
}

function ExitConfirmation({ onCancel, onConfirm }) {
  return (
    <div className="dialog-layer">
      <section className="confirm-dialog" role="alertdialog" aria-modal="true" aria-labelledby="exit-title" aria-describedby="exit-copy">
        <h2 id="exit-title">Exit “Mika &amp; Paolo's Wedding”?</h2>
        <p id="exit-copy">The Event will become inactive and FotoHAVN will return to Saved Events. Saved photos are not deleted.</p>
        <footer><button className="dialog-cancel" onClick={onCancel}>Cancel</button><button className="dialog-primary" onClick={onConfirm}>Exit Event</button></footer>
      </section>
    </div>
  );
}

function ExitedEventScreen() {
  return (
    <main className="exited-screen">
      <header><Brand /><span>Operator console</span></header>
      <section><span className="complete-mark"><Check weight="bold" /></span><p className="event-label">Saved Events</p><h1>Event exited.</h1><p>“Mika &amp; Paolo's Wedding” is inactive. The production UI returns to the Saved Events landing page here.</p></section>
    </main>
  );
}

function CaptureScreen({ variant, phase, completed, countdown }) {
  const isCountdown = phase === "countdown";
  const isFlash = phase === "flash";
  const isConfirm = phase === "confirm";
  const instruction = isCountdown ? (countdown >= 3 ? "Get ready" : countdown === 2 ? "Looking good" : "Hold it") : isConfirm ? "Captured" : "";

  if (variant === "B") {
    return (
      <main className={`capture-screen capture-b ${isFlash ? "flashing" : ""}`}>
        <aside>
          <Brand />
          <p className="step-label">Capture {Math.min(completed + 1, 4)} of 4</p>
          <h1>{isConfirm ? "Nice one." : instruction}</h1>
          <p>{isConfirm ? `${4 - completed} more to go.` : "Look into the Camera and stay still at one."}</p>
          <ProgressDots completed={completed} active={isCountdown} />
        </aside>
        <CameraScene>
          <div className="camera-tag"><Camera />Live · Mirrored</div>
          {isCountdown && <div className="countdown compact-count"><small>{instruction}</small><strong>{countdown}</strong></div>}
          {isConfirm && <div className="capture-confirm"><Check weight="bold" /><span>Capture {completed} saved</span></div>}
        </CameraScene>
      </main>
    );
  }

  return (
    <main className={`capture-screen capture-${variant.toLowerCase()} ${isFlash ? "flashing" : ""}`}>
      <header><Brand /><ProgressDots completed={completed} active={isCountdown} /></header>
      <CameraScene>
        <div className="camera-tag"><Camera />Live · Mirrored</div>
        {isCountdown && <div className="countdown"><small>{instruction}</small><strong>{countdown}</strong></div>}
        {isConfirm && <div className="capture-confirm"><Check weight="bold" /><span>Capture {completed} of 4</span><small>{completed < 4 ? "Great — next one coming up" : "That’s all four"}</small></div>}
      </CameraScene>
    </main>
  );
}

function StripScreen({ variant, remaining, leaving }) {
  return (
    <main className={`strip-screen strip-${variant.toLowerCase()} ${leaving ? "leaving" : ""}`}>
      <section className="strip-copy">
        <p className="event-label">All four Captures saved</p>
        <h1>Here’s your Photo Strip.</h1>
        <p>Looking good! The booth will be ready for the next guests in {remaining}.</p>
        <div className="return-progress" aria-label={`Returning to Start in ${remaining} seconds`}><span style={{ width: `${(remaining / photoStripPreviewSeconds) * 100}%` }} /></div>
      </section>
      <PhotoStrip />
    </main>
  );
}

function AssistanceScreen({ kind, completed, onRetry }) {
  const cameraIssue = kind === "camera";
  return (
    <main className="assistance-screen">
      <Brand />
      <section role="alert">
        <span className="warning-mark"><WarningCircle weight="fill" /></span>
        <p className="event-label">We paused your photos</p>
        <h1>Please call the operator.</h1>
        <p>{cameraIssue ? "The Camera isn’t available right now." : "This Capture couldn’t be saved."} Your {completed} completed Capture{completed === 1 ? " is" : "s are"} safe.</p>
        <div className="operator-detail"><span>{cameraIssue ? <Camera /> : <HardDrive />}</span><div><b>{cameraIssue ? "Camera unavailable" : "Storage unavailable"}</b><small>Guest Cycle paused · progress retained</small></div></div>
        <button className="retry-button" onClick={onRetry}><CircleNotch />Retry</button>
      </section>
    </main>
  );
}

function PrototypeControls({ variant, onVariant, onJump, error }) {
  const currentIndex = variants.findIndex(({ key }) => key === variant);
  const move = (delta) => onVariant(variants[(currentIndex + delta + variants.length) % variants.length].key);

  useEffect(() => {
    const onKey = (event) => {
      const active = document.activeElement;
      if (["INPUT", "TEXTAREA", "SELECT"].includes(active?.tagName) || active?.isContentEditable) return;
      if (event.key === "ArrowLeft") move(-1);
      if (event.key === "ArrowRight") move(1);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  if (import.meta.env.PROD) return null;
  return (
    <nav className="prototype-controls" aria-label="Prototype review controls">
      <div className="variant-switcher">
        <button onClick={() => move(-1)} aria-label="Previous variant"><ArrowLeft /></button>
        <span><b>{variant}</b> · {variants[currentIndex].name}</span>
        <button onClick={() => move(1)} aria-label="Next variant"><ArrowRight /></button>
      </div>
      <div className="state-jumps">
        <button onClick={() => onJump("start")}>Start</button>
        <button onClick={() => onJump("exit")}>Exit dialog</button>
        <button onClick={() => onJump("start-camera")}>Start camera off</button>
        <button onClick={() => onJump("start-storage")}>Start storage off</button>
        <button onClick={() => onJump("countdown")}>Countdown</button>
        <button onClick={() => onJump("strip")}>Photo Strip</button>
        <button className={error === "camera" ? "selected" : ""} onClick={() => onJump("camera")}>Camera issue</button>
        <button className={error === "storage" ? "selected" : ""} onClick={() => onJump("storage")}>Storage issue</button>
      </div>
    </nav>
  );
}

export function App() {
  const [variant, setVariant] = useState(getVariant);
  const [phase, setPhase] = useState("start");
  const [completed, setCompleted] = useState(0);
  const [countdown, setCountdown] = useState(captureCountdownSeconds);
  const [remaining, setRemaining] = useState(photoStripPreviewSeconds);
  const [error, setError] = useState(null);
  const [leaving, setLeaving] = useState(false);
  const [exitDialog, setExitDialog] = useState(false);
  const [exited, setExited] = useState(false);
  const [startFailure, setStartFailure] = useState(null);

  const changeVariant = (key) => {
    const url = new URL(window.location.href);
    url.searchParams.set("variant", key);
    window.history.replaceState({}, "", url);
    setVariant(key);
  };

  const startCycle = () => {
    setCompleted(0); setCountdown(captureCountdownSeconds); setError(null); setStartFailure(null); setLeaving(false); setExitDialog(false); setPhase("countdown");
  };

  useTimeout(() => {
    if (countdown > 1) setCountdown((value) => value - 1);
    else setPhase("flash");
  }, phase === "countdown" ? acceleratedSecond : null, [phase, countdown, completed]);

  useTimeout(() => {
    setCompleted((value) => value + 1);
    setPhase("confirm");
  }, phase === "flash" ? 600 : null, [phase]);

  useTimeout(() => {
    if (completed >= 4) {
      setRemaining(photoStripPreviewSeconds); setPhase("strip");
    } else {
      setCountdown(captureCountdownSeconds); setPhase("countdown");
    }
  }, phase === "confirm" ? 900 : null, [phase, completed]);

  useTimeout(() => {
    if (remaining > 1) setRemaining((value) => value - 1);
    else setLeaving(true);
  }, phase === "strip" && !leaving ? acceleratedSecond : null, [phase, remaining, leaving]);

  useTimeout(() => {
    setPhase("start"); setCompleted(0); setLeaving(false);
  }, phase === "strip" && leaving ? 450 : null, [phase, leaving]);

  const jump = (target) => {
    setError(null); setStartFailure(null); setLeaving(false); setExitDialog(false); setExited(false);
    if (target === "start") { setCompleted(0); setPhase("start"); return; }
    if (target === "exit") { setCompleted(0); setPhase("start"); setExitDialog(true); return; }
    if (target === "start-camera") { setCompleted(0); setPhase("start"); setStartFailure("camera"); return; }
    if (target === "start-storage") { setCompleted(0); setPhase("start"); setStartFailure("storage"); return; }
    if (target === "countdown") { setCompleted(1); setCountdown(captureCountdownSeconds); setPhase("countdown"); return; }
    if (target === "strip") { setCompleted(4); setRemaining(photoStripPreviewSeconds); setPhase("strip"); return; }
    setCompleted(2); setError(target); setPhase("assistance");
  };

  const screen = useMemo(() => {
    if (exited) return <ExitedEventScreen />;
    if (phase === "start" && startFailure) return <StartUnavailableScreen kind={startFailure} onRetry={() => setStartFailure(null)} onExit={() => setExitDialog(true)} />;
    if (phase === "start") return <StartScreen variant={variant} onStart={startCycle} onExit={() => setExitDialog(true)} />;
    if (phase === "strip") return <StripScreen variant={variant} remaining={remaining} leaving={leaving} />;
    if (phase === "assistance") return <AssistanceScreen kind={error} completed={completed} onRetry={() => { setError(null); setCountdown(captureCountdownSeconds); setPhase("countdown"); }} />;
    return <CaptureScreen variant={variant} phase={phase} completed={completed} countdown={countdown} />;
  }, [variant, phase, completed, countdown, remaining, error, leaving, exited, startFailure]);

  return (
    <div className={`prototype-stage variant-${variant}`}>
      {screen}
      {exitDialog && <ExitConfirmation onCancel={() => setExitDialog(false)} onConfirm={() => { setExitDialog(false); setExited(true); }} />}
      <PrototypeControls variant={variant} onVariant={changeVariant} onJump={jump} error={error} />
    </div>
  );
}
