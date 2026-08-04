import { useEffect, useMemo, useState } from "react";
import {
  ArrowClockwise,
  ArrowLeft,
  ArrowRight,
  Camera,
  Check,
  CircleNotch,
  GearSix,
  Play,
  Plus,
  Power,
  SlidersHorizontal,
  Trash,
  WarningCircle,
  X,
} from "@phosphor-icons/react";

const variants = [
  { key: "A", name: "Attio modal" },
  { key: "B", name: "Preview workspace" },
  { key: "C", name: "Setup rail" },
];

const savedEvents = [
  { id: 1, name: "Mika & Paolo's Wedding", saved: "Saved today, 9:18 PM" },
  { id: 2, name: "Saturday Market", saved: "Saved Jul 28, 2026, 11:06 AM" },
  { id: 3, name: "Year-End Party", saved: "Saved Jul 17, 2026, 9:18 PM" },
];

const tuningDefaults = { brightness: 12, contrast: 8, exposure: 3, whiteBalance: 5600 };
const incompleteDeletionEventId = 3;
const deletionRecoveryKey = "fotohavn-prototype-deletion-recovery";

function loadDeletionRecovery() {
  const params = new URLSearchParams(window.location.search);
  if (params.get("resetDeletion") === "1") {
    window.localStorage.removeItem(deletionRecoveryKey);
    params.delete("resetDeletion");
    const nextUrl = `${window.location.pathname}${params.size ? `?${params}` : ""}`;
    window.history.replaceState({}, "", nextUrl);
    return [];
  }
  try {
    return JSON.parse(window.localStorage.getItem(deletionRecoveryKey) ?? "[]");
  } catch {
    return [];
  }
}

function getVariant() {
  const key = new URLSearchParams(window.location.search).get("variant")?.toUpperCase();
  return variants.some((item) => item.key === key) ? key : "A";
}

function PrototypeSwitcher({ current, onChange }) {
  const index = variants.findIndex((item) => item.key === current);
  const move = (direction) => onChange(variants[(index + direction + variants.length) % variants.length].key);

  useEffect(() => {
    const onKey = (event) => {
      const tag = document.activeElement?.tagName;
      if (["INPUT", "TEXTAREA", "SELECT"].includes(tag) || document.activeElement?.isContentEditable) return;
      if (event.key === "ArrowLeft") move(-1);
      if (event.key === "ArrowRight") move(1);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  if (import.meta.env.PROD) return null;
  const variant = variants[index];
  return (
    <nav className="prototype-switcher" aria-label="Prototype variants">
      <button aria-label="Previous variant" onClick={() => move(-1)}><ArrowLeft /></button>
      <span><b>{variant.key}</b> · {variant.name}</span>
      <button aria-label="Next variant" onClick={() => move(1)}><ArrowRight /></button>
    </nav>
  );
}

function Status({ children = "Ready", muted = false }) {
  return <span className={`status ${muted ? "muted" : ""}`}><span className="status-dot" />{children}</span>;
}

function EventCard({ event, onEdit, onStart, onDelete }) {
  return (
    <article className="event-card">
      <button className="card-start" onClick={() => onStart(event)} aria-label={`Start ${event.name}`}>
        <span className="card-copy"><strong>{event.name}</strong><small>{event.saved}</small></span>
        <span className="play-reveal"><Play weight="fill" /> <span>Start Event</span></span>
      </button>
      <button className="icon-button delete" onClick={() => onDelete(event)} aria-label={`Delete ${event.name}`}><Trash /></button>
      <button className="icon-button gear" onClick={() => onEdit(event)} aria-label={`Edit ${event.name}`}><GearSix /></button>
    </article>
  );
}

function QuarantinedEventCard({ event, onRetry }) {
  return (
    <article className="event-card quarantined-card" aria-labelledby={`quarantined-${event.id}`}>
      <div className="quarantined-copy">
        <span className="quarantine-label"><WarningCircle />Deletion incomplete</span>
        <strong id={`quarantined-${event.id}`}>{event.name}</strong>
        <small>Some files may already be gone. This Event is unavailable.</small>
      </div>
      <button className="retry-deletion" onClick={() => onRetry(event)}><ArrowClockwise />Retry Deletion</button>
    </article>
  );
}

function Landing({ events, quarantinedIds, onNew, onEdit, onStart, onDelete, onRetry }) {
  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#" onClick={(event) => event.preventDefault()}><span>F</span>FotoHAVN</a>
        <span className="console-label">Operator console</span>
      </header>
      <main className="landing">
        <p className="eyebrow">Saved Events</p>
        <h1>Choose an Event</h1>
        <p className="lead">Open one to adjust its setup, or start a Guest Cycle.</p>
        <div className="event-grid">
          <button className="new-event" onClick={onNew}>
            <span className="new-icon"><Plus /></span>
            <strong>New Event</strong>
            <small>Set up a new booth run</small>
          </button>
          {events.map((event) => quarantinedIds.includes(event.id)
            ? <QuarantinedEventCard key={event.id} event={event} onRetry={onRetry} />
            : <EventCard key={event.id} event={event} onEdit={onEdit} onStart={onStart} onDelete={onDelete} />)}
        </div>
      </main>
    </div>
  );
}

function TuningControl({ icon: Icon, label, value, min, max, step = 1, suffix, dirty, onChange }) {
  return (
    <label className={`tuning-control ${dirty ? "dirty" : ""}`}>
      <span className="tuning-name"><Icon />{label}</span>
      <input type="range" min={min} max={max} step={step} value={value} onChange={(event) => onChange(Number(event.target.value))} />
      <output>{value > 0 && suffix !== "K" ? "+" : ""}{suffix === "EV" ? (value / 10).toFixed(1) : value}{suffix === "K" ? "K" : suffix === "EV" ? "" : ""}</output>
    </label>
  );
}

function SetupModal({ variant, event, isNew, initialExpanded, onCancel, onSave, onStart }) {
  const [name, setName] = useState(event?.name ?? "");
  const [camera, setCamera] = useState(event ? "Logitech BRIO" : "");
  const [printer, setPrinter] = useState("No Printer");
  const [expanded, setExpanded] = useState(initialExpanded);
  const [tuning, setTuning] = useState(tuningDefaults);
  const [errors, setErrors] = useState({});
  const [confirm, setConfirm] = useState(null);
  const baseName = event?.name ?? "";
  const baseCamera = event ? "Logitech BRIO" : "";
  const basePrinter = "No Printer";
  const tuningDirty = Object.keys(tuning).some((key) => tuning[key] !== tuningDefaults[key]);
  const dirty = name !== baseName || camera !== baseCamera || printer !== basePrinter || tuningDirty;

  const validate = (intent) => {
    const next = {};
    if (!name.trim()) next.name = "Enter an Event name.";
    if (!camera) next.camera = "Choose one Camera to continue.";
    setErrors(next);
    if (Object.keys(next).length) return;
    if (!isNew && dirty) setConfirm(intent);
    else intent === "start" ? onStart({ name, printer }) : onSave({ name, printer });
  };

  const cancel = () => dirty ? setConfirm("discard") : onCancel();
  const runConfirmed = () => {
    if (confirm === "discard") onCancel();
    else if (confirm === "start") onStart({ name, printer });
    else onSave({ name, printer });
  };

  const modalTitle = isNew ? "New Event" : "Edit Event";
  return (
    <div className="modal-layer" role="presentation">
      <section className={`setup-modal variant-${variant}`} role="dialog" aria-modal="true" aria-labelledby="setup-title">
        <header className="modal-header">
          <div><p className="eyebrow">Event setup</p><h2 id="setup-title">{modalTitle}</h2><p>{isNew ? "Name the Event, choose the Camera, and confirm the printing option for this booth run." : "Update the Event name, Camera, or printing option. Camera Tuning is available when you need it."}</p></div>
          <button className="close-button" onClick={cancel} aria-label="Close Event setup"><X /></button>
        </header>

        <div className="setup-content">
          <section className="identity-grid">
            <label className={`field ${name !== baseName ? "dirty" : ""} ${errors.name ? "invalid" : ""}`}>
              <span>Event name</span>
              <input value={name} autoFocus={isNew} onChange={(e) => { setName(e.target.value); setErrors((old) => ({ ...old, name: null })); }} />
              <small>{errors.name ?? "Names do not need to be unique."}</small>
            </label>
            <label className={`field printer-field ${printer !== basePrinter ? "dirty" : ""} ${errors.printer ? "invalid" : ""}`}>
              <span>Printer</span>
              <select value={printer} onChange={(e) => { setPrinter(e.target.value); setErrors((old) => ({ ...old, printer: null })); }}>
                <option>No Printer</option>
                <option>DNP DS-RX1HS</option>
              </select>
              <small>No Printer is valid for this field test.</small>
            </label>
          </section>

          <section className={`camera-section ${tuningDirty ? "dirty-section" : ""}`}>
            <div className="camera-head">
              <div><h3>Camera</h3><p>Choose the Camera used when this Event is saved or started.</p></div>
              <button className="tuning-toggle" onClick={() => setExpanded((value) => !value)} aria-expanded={expanded} aria-label="Camera tuning" title="Camera tuning"><GearSix /></button>
            </div>
            <label className={`field camera-select ${camera !== baseCamera ? "dirty" : ""} ${errors.camera ? "invalid" : ""}`}>
              <span>Selected Camera</span>
              <select value={camera} onChange={(e) => { setCamera(e.target.value); setErrors((old) => ({ ...old, camera: null })); }}>
                <option value="">Choose a Camera</option><option>Logitech BRIO</option><option>Canon EOS Webcam Utility</option>
              </select>
              {!camera && <small>{errors.camera ?? "One Camera is required."}</small>}
            </label>

            {expanded && camera && <div className="tuning-workspace">
              <figure className="preview"><img src="/assets/wedding-camera-preview.png" alt="Mirrored live preview from Logitech BRIO" /><figcaption>Live · Mirrored</figcaption></figure>
              <div className="tuning-panel">
                <div className="tuning-heading"><div><p className="eyebrow">Camera tuning</p><small>Tuning is remembered per Camera for this Event.</small></div><button onClick={() => setTuning(tuningDefaults)}>Reset</button></div>
                <TuningControl icon={Plus} label="Brightness" value={tuning.brightness} min={-20} max={20} suffix="" dirty={tuning.brightness !== tuningDefaults.brightness} onChange={(value) => setTuning({ ...tuning, brightness: value })} />
                <TuningControl icon={SlidersHorizontal} label="Contrast" value={tuning.contrast} min={-20} max={20} suffix="" dirty={tuning.contrast !== tuningDefaults.contrast} onChange={(value) => setTuning({ ...tuning, contrast: value })} />
                <TuningControl icon={Camera} label="Exposure compensation" value={tuning.exposure} min={-20} max={20} suffix="EV" dirty={tuning.exposure !== tuningDefaults.exposure} onChange={(value) => setTuning({ ...tuning, exposure: value })} />
                <TuningControl icon={SlidersHorizontal} label="White balance" value={tuning.whiteBalance} min={3200} max={7000} step={100} suffix="K" dirty={tuning.whiteBalance !== tuningDefaults.whiteBalance} onChange={(value) => setTuning({ ...tuning, whiteBalance: value })} />
              </div>
            </div>}
          </section>
        </div>

        <footer className="modal-footer"><button className="text-button" onClick={cancel}>Cancel</button><div><button className="outline-button" onClick={() => validate("save")}>Save &amp; Close</button><button className="primary-button" onClick={() => validate("start")}>Save &amp; Start Event</button></div></footer>
      </section>
      {confirm && <Confirmation
        title={confirm === "discard" ? (isNew ? "Discard new Event?" : "Discard changes?") : confirm === "start" ? `Save changes and start “${name}”?` : `Save changes to “${name}”?`}
        body={confirm === "discard" ? (isNew ? "The Event and its Camera Tuning will not be created." : "Your last-saved Event setup will be restored.") : confirm === "start" ? "The new setup applies only to future Guest Cycles. FotoHAVN will run preflight after saving." : "The new Event name, selected Camera, printing option, and Camera Tuning apply only to future Guest Cycles."}
        cancelLabel="Keep Editing" actionLabel={confirm === "discard" ? (isNew ? "Discard Draft" : "Discard Changes") : confirm === "start" ? "Save & Start Event" : "Save Changes"}
        destructive={confirm === "discard"} onCancel={() => setConfirm(null)} onConfirm={runConfirmed}
      />}
    </div>
  );
}

function Confirmation({ title, body, cancelLabel = "Cancel", actionLabel, destructive = false, onCancel, onConfirm }) {
  return <div className="dialog-layer"><section className="confirm-dialog" role="alertdialog" aria-modal="true"><h2>{title}</h2><p>{body}</p><footer>{onCancel && <button className="text-button" onClick={onCancel}>{cancelLabel}</button>}<button className={destructive ? "danger-button" : "primary-button"} onClick={onConfirm}>{actionLabel}</button></footer></section></div>;
}

function DeletionProgress({ event, onDone }) {
  useEffect(() => {
    const timer = setTimeout(onDone, 1800);
    return () => clearTimeout(timer);
  }, [onDone]);

  return <div className="dialog-layer deletion-layer"><section className="confirm-dialog deletion-progress" role="alertdialog" aria-modal="true" aria-busy="true" aria-labelledby="deleting-title" aria-describedby="deleting-copy"><CircleNotch className="busy-spinner" aria-hidden="true" /><h2 id="deleting-title">Deleting “{event.name}”…</h2><p id="deleting-copy">FotoHAVN is permanently deleting the Event directory, Guest Cycles, and photos. Keep the app open.</p></section></div>;
}

function Preflight({ event, onDone }) {
  const [step, setStep] = useState(0);
  useEffect(() => { const timer = setInterval(() => setStep((value) => Math.min(value + 1, 3)), 500); return () => clearInterval(timer); }, []);
  useEffect(() => { if (step === 3) { const timer = setTimeout(onDone, 700); return () => clearTimeout(timer); } }, [step, onDone]);
  const items = ["Camera connected", "Storage available", "No Printer selected"];
  return <div className="modal-layer"><section className="preflight"><span className="preflight-icon"><Check /></span><p className="eyebrow">Starting Event</p><h2>{event.name}</h2><p>Checking the booth before opening guest Start.</p><ul>{items.map((item, index) => <li key={item} className={index < step ? "complete" : index === step ? "checking" : ""}><span>{index < step ? <Check /> : index + 1}</span>{item}</li>)}</ul></section></div>;
}

function GuestExperience({ event }) {
  const url = `http://127.0.0.1:43174/?variant=A&embed=1&event=${encodeURIComponent(event.name)}`;
  return <iframe className="guest-frame" title={`Guest Photo Booth for ${event.name}`} src={url} />;
}

export function App() {
  const [variant, setVariant] = useState(getVariant);
  const [setup, setSetup] = useState({ event: savedEvents[0], isNew: false, expanded: true });
  const [dialog, setDialog] = useState(null);
  const [preflight, setPreflight] = useState(null);
  const [active, setActive] = useState(null);
  const [deleted, setDeleted] = useState([]);
  const [quarantined, setQuarantined] = useState(loadDeletionRecovery);
  const visibleEvents = useMemo(() => savedEvents.filter((event) => !deleted.includes(event.id)), [deleted]);

  useEffect(() => {
    window.localStorage.setItem(deletionRecoveryKey, JSON.stringify(quarantined));
  }, [quarantined]);

  useEffect(() => {
    const onMessage = (event) => {
      if (event.origin === "http://127.0.0.1:43174" && event.data?.type === "fotohavn-exit-event") {
        setDialog(null);
        setActive(null);
      }
    };
    window.addEventListener("message", onMessage);
    return () => window.removeEventListener("message", onMessage);
  }, []);

  const changeVariant = (key) => { const url = new URL(window.location); url.searchParams.set("variant", key); window.history.replaceState({}, "", url); setVariant(key); };
  const start = (event) => setDialog({ kind: "start", event });
  const beginPreflight = (event) => { setDialog(null); setSetup(null); setPreflight(event); };

  return <div className="prototype-stage">
    {active ? <GuestExperience event={active} /> : <Landing
      events={visibleEvents}
      quarantinedIds={quarantined}
      onNew={() => setSetup({ event: null, isNew: true, expanded: false })}
      onEdit={(event) => setSetup({ event, isNew: false, expanded: false })}
      onStart={start}
      onDelete={(event) => setDialog({ kind: "delete", event })}
      onRetry={(event) => setDialog({ kind: "deleting", event, retry: true })}
    />}

    {setup && <SetupModal variant={variant} {...setup} initialExpanded={setup.expanded} onCancel={() => setSetup(null)} onSave={() => setSetup(null)} onStart={beginPreflight} />}
    {preflight && <Preflight event={preflight} onDone={() => { setActive(preflight); setPreflight(null); }} />}

    {dialog?.kind === "start" && <Confirmation title={`Start “${dialog.event.name}”?`} body="FotoHAVN will check the camera and storage before opening the guest Start screen. No Printer requires no device check." actionLabel="Start Event" onCancel={() => setDialog(null)} onConfirm={() => beginPreflight(dialog.event)} />}
    {dialog?.kind === "delete" && <Confirmation title={`Delete “${dialog.event.name}”?`} body="This Event, all Guest Cycles, and all photos will be permanently deleted and cannot be recovered." actionLabel="Delete Event" destructive onCancel={() => setDialog(null)} onConfirm={() => setDialog({ kind: "deleting", event: dialog.event })} />}
    {dialog?.kind === "deleting" && <DeletionProgress event={dialog.event} onDone={() => {
      if (dialog.event.id === incompleteDeletionEventId) {
        setQuarantined((current) => current.includes(dialog.event.id) ? current : [...current, dialog.event.id]);
        setDialog({ kind: "deleteFailed", event: dialog.event, retry: dialog.retry });
      } else {
        setDeleted((current) => [...current, dialog.event.id]);
        setDialog({ kind: "deleted", event: dialog.event });
      }
    }} />}
    {dialog?.kind === "deleteFailed" && <Confirmation title={`Couldn’t finish deleting “${dialog.event.name}”`} body={`Not every file was removed, so this Event may no longer be usable.${dialog.retry ? " The retry also failed; nothing was restored." : ""}`} cancelLabel="Close" actionLabel="Retry Deletion" destructive onCancel={() => setDialog(null)} onConfirm={() => setDialog({ kind: "deleting", event: dialog.event, retry: true })} />}
    {dialog?.kind === "deleted" && <Confirmation title="Event deleted" body={`“${dialog.event.name}”, its Guest Cycles, and its photos were permanently deleted.`} actionLabel="Done" onConfirm={() => setDialog(null)} />}
    {dialog?.kind === "exit" && <Confirmation title={`Exit “${dialog.event.name}”?`} body="The Event will become inactive and FotoHAVN will return to Saved Events. Saved photos are not deleted." actionLabel="Exit Event" onCancel={() => setDialog(null)} onConfirm={() => { setDialog(null); setActive(null); }} />}
    {!active && <PrototypeSwitcher current={variant} onChange={changeVariant} />}
  </div>;
}
