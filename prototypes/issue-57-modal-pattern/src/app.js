import "./styles.css";

// Three modal standards, switchable via ?variant=, mounted over current operator surfaces.
const app = document.querySelector("#app");
const variants = {
  A: "Calm footer",
  B: "Decision split",
  C: "Guided stack",
};
const scenarios = {
  start: "Start Event",
  exit: "Exit Event",
  delete: "Delete Event",
  discard: "Discard setup changes",
  error: "Recoverable error",
};
const event = {
  name: "Mika & Paolo's Wedding",
  id: "019fdb2f-26bb-7811-bff5-9f788d8e7b99",
  compactId: "8E7B · 7B99",
};

const params = new URLSearchParams(location.search);
let variant = variants[params.get("variant")] ? params.get("variant") : "A";
let scenario = scenarios[params.get("scenario")] ? params.get("scenario") : "delete";
let phase = scenario === "error" ? "error" : "idle";
let modalOpen = true;
let toast = "";
let destination = null;
let setupDirty = scenario === "discard";
let returnFocusSelector = null;

const icon = (name) => {
  const paths = {
    play: '<path d="M8 5v14l11-7z"/>',
    power: '<path d="M12 2v10M5.6 5.6a9 9 0 1 0 12.8 0"/>',
    trash: '<path d="M4 7h16M9 7V4h6v3m-8 0 1 14h8l1-14M10 11v6m4-6v6"/>',
    edit: '<path d="m4 16-1 5 5-1L19 9l-4-4L4 16Zm9-9 4 4"/>',
    warning: '<path d="M12 3 2 21h20L12 3Zm0 6v5m0 3v1"/>',
    close: '<path d="m6 6 12 12M18 6 6 18"/>',
    camera: '<rect x="3" y="6" width="18" height="13" rx="2"/><path d="m8 6 2-3h4l2 3m-7 6a3 3 0 1 0 6 0 3 3 0 0 0-6 0Z"/>',
    spinner: '<circle cx="12" cy="12" r="8" opacity=".22"/><path d="M12 4a8 8 0 0 1 8 8"/>',
    check: '<path d="m4 12 5 5L20 6"/>',
  };
  return `<svg class="icon ${name === "spinner" ? "spin" : ""}" aria-hidden="true" viewBox="0 0 24 24">${paths[name]}</svg>`;
};

function identityPanel() {
  return `<div class="identity-panel" aria-label="Event identity">
    <strong>${event.name}</strong>
    <span>EVENT ID</span>
    <code>${event.id}</code>
  </div>`;
}

function shellHeader(active = false) {
  return `<header class="app-header">
    <div class="brand"><span>F</span><strong>FotoHAVN</strong></div>
    ${active ? '<button class="header-action" data-open-modal="exit">Exit Event</button>' : '<span class="console-label">OPERATOR CONSOLE</span>'}
  </header>`;
}

function savedEvents() {
  return `<div class="app-shell modal-background">${shellHeader()}
    <main class="saved-events">
      <p class="eyebrow">SAVED EVENTS</p><h1 tabindex="-1">Choose an Event</h1>
      <p>Open an Event to adjust its setup, or start the booth.</p>
      <div class="event-grid">
        <button class="new-event"><span>+</span><strong>New Event</strong></button>
        <article class="event-card">
          <div class="card-tools"><button aria-label="Edit ${event.name}" data-open-setup>${icon("edit")}</button><button aria-label="Delete ${event.name}" data-open-modal="delete">${icon("trash")}</button></div>
          <div><h2>${event.name}</h2><span class="compact-id">EVENT ID&nbsp;&nbsp;${event.compactId}</span><small>Saved today, 9:18 PM</small></div>
          <p>${icon("check")} Camera ready · Not printing · Storage ready</p>
          <button class="event-start" data-open-modal="start">${icon("play")} Start Event</button>
        </article>
      </div>
      ${toast ? `<div class="toast" role="status" tabindex="-1">${icon("check")} ${toast}</div>` : ""}
    </main></div>`;
}

function activeEvent() {
  return `<div class="app-shell modal-background">${shellHeader(true)}
    <main class="guest-start"><span class="eyebrow">${event.name.toUpperCase()}</span><h1 tabindex="-1">Ready when you are.</h1><p>We’ll take four photos, with a short countdown before each one.</p><button>Touch to start</button>${toast ? `<span class="sr-only" role="status">${toast}</span>` : ""}</main>
  </div>`;
}

function setupScreen() {
  return `<div class="app-shell modal-background">${shellHeader()}
    <div class="setup-scrim"><section class="setup-dialog" aria-labelledby="setup-title">
      <header><p class="eyebrow">EVENT SETUP</p><h1 id="setup-title" tabindex="-1">Edit Event</h1><p>Update the Event name, Camera, or Printer.</p></header>
      <div class="setup-content">
        <div class="setup-form">
          <label>Event name<input value="${event.name}" data-dirty-input /></label>
          <label>Camera<select data-dirty-input><option>FJ Camera 01 (3:2)</option></select></label>
          <label>Printer <span>(optional)</span><select data-dirty-input><option>Not printing</option></select></label>
          <div class="storage"><strong>Storage</strong><span>C:\\Program Files\\FotoHAVN\\Events</span><small>120 GB free</small></div>
        </div>
        <div class="live-preview"><h2>Live preview</h2><div>${icon("camera")}<span>3:2 Capture area</span></div></div>
      </div>
      <footer><button class="tertiary" data-cancel-setup>Cancel</button><div><button class="secondary">Save & Close</button><button class="primary">Save & Start Event</button></div></footer>
    </section></div></div>`;
}

function scenarioContent() {
  const data = {
    start: { icon: "play", eyebrow: "START EVENT", title: "Start this Event?", body: "FotoHAVN will check the selected Camera and storage before opening the booth.", safe: "Not Yet", action: "Start Event", busy: "Starting Event…", success: "Event started.", destructive: false, identity: true },
    exit: { icon: "power", eyebrow: "EXIT EVENT", title: "Exit this Event?", body: "The booth will stop accepting Guest Cycles and return to Saved Events.", safe: "Keep Event Active", action: "Exit Event", busy: "Exiting Event…", success: "Event exited.", destructive: true, identity: true },
    delete: { icon: "trash", eyebrow: "PERMANENTLY DELETE", title: "Delete this Event?", body: "This permanently deletes the Event and its saved Captures. This cannot be undone.", safe: "Keep Event", action: "Delete Event", busy: "Deleting Event…", success: "Event deleted.", destructive: true, identity: true },
    discard: { icon: "warning", eyebrow: "UNSAVED CHANGES", title: "Discard setup changes?", body: "Your changes to the Event name, Camera, or Printer will not be saved.", safe: "Continue Editing", action: "Discard Changes", busy: "Discarding…", success: "Changes discarded.", destructive: true, identity: true },
    error: { icon: "warning", eyebrow: "START EVENT", title: "Camera check failed", body: "The selected Camera is being used by another app. Close that app, then retry without losing setup.", safe: "Back to Setup", action: "Retry", busy: "Checking Camera…", success: "Camera ready. Event started.", destructive: false, identity: true },
  };
  return data[scenario];
}

function statusCallout() {
  if (phase !== "error") return "";
  return `<div class="status-callout" role="alert">${icon("warning")}<div><strong>Could not complete the action</strong><span>${scenario === "delete" ? "Some Event files are still in use. Close other apps, then retry." : "The Camera is still unavailable. Close the other app, then retry."}</span></div></div>`;
}

function actionButtons(data, stacked = false) {
  const disabled = phase === "busy" ? "disabled" : "";
  const actionClass = data.destructive ? "danger" : "primary";
  return `<div class="modal-actions ${stacked ? "stacked" : ""}">
    <button class="secondary" data-safe data-autofocus ${disabled}>${data.safe}</button>
    <button class="${actionClass}" data-confirm ${disabled}>${phase === "busy" ? icon("spinner") + data.busy : phase === "error" ? "Retry" : data.action}</button>
  </div>`;
}

function modal() {
  const data = scenarioContent();
  const closeDisabled = phase === "busy" ? "disabled" : "";
  const sharedHead = `<button class="modal-close" data-safe aria-label="Close dialog" ${closeDisabled}>${icon("close")}</button>`;
  if (variant === "A") {
    return `<div class="modal-layer"><section class="modal variant-a" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      ${sharedHead}<div class="modal-icon ${data.destructive ? "danger-soft" : "neutral-soft"}">${icon(data.icon)}</div>
      <p class="eyebrow">${data.eyebrow}</p><h2 id="modal-title">${data.title}</h2><p class="modal-body">${data.body}</p>
      ${data.identity ? identityPanel() : ""}${statusCallout()}
      <footer>${actionButtons(data)}</footer>
    </section></div>`;
  }
  if (variant === "B") {
    return `<div class="modal-layer"><section class="modal variant-b" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      ${sharedHead}<div class="decision-copy"><div class="modal-icon ${data.destructive ? "danger-soft" : "neutral-soft"}">${icon(data.icon)}</div><p class="eyebrow">${data.eyebrow}</p><h2 id="modal-title">${data.title}</h2><p class="modal-body">${data.body}</p>${data.identity ? identityPanel() : ""}${statusCallout()}</div>
      <aside class="decision-rail"><span class="rail-label">YOUR DECISION</span><p>${data.destructive ? "The safe choice preserves the Event and its current state." : "You can return without changing the current state."}</p>${actionButtons(data, true)}</aside>
    </section></div>`;
  }
  return `<div class="modal-layer"><section class="modal variant-c" role="dialog" aria-modal="true" aria-labelledby="modal-title">
    ${sharedHead}<header><div class="modal-icon ${data.destructive ? "danger-soft" : "neutral-soft"}">${icon(data.icon)}</div><div><p class="eyebrow">${data.eyebrow}</p><h2 id="modal-title">${data.title}</h2></div></header>
    <ol class="review-steps"><li><span>1</span><div><strong>Review the Event</strong>${data.identity ? identityPanel() : ""}</div></li><li><span>2</span><div><strong>Understand what happens</strong><p>${data.body}</p></div></li></ol>${statusCallout()}
    <footer><span class="rail-label">CHOOSE AN ACTION</span>${actionButtons(data, true)}</footer>
  </section></div>`;
}

function prototypeSwitcher() {
  if (!import.meta.env.DEV) return "";
  return `<aside class="prototype-switcher" aria-label="Prototype controls">
    <button data-prev aria-label="Previous variant">←</button><strong>${variant} — ${variants[variant]}</strong><button data-next aria-label="Next variant">→</button>
    <span></span><label>Scenario<select data-scenario>${Object.entries(scenarios).map(([key, label]) => `<option value="${key}" ${scenario === key ? "selected" : ""}>${label}</option>`).join("")}</select></label>
    <button class="simulate" data-simulate ${phase === "busy" ? "disabled" : ""}>Simulate failure</button>
  </aside>`;
}

function render() {
  const background = destination === "active" ? activeEvent() : destination === "saved" ? savedEvents() : scenario === "discard" ? setupScreen() : scenario === "exit" ? activeEvent() : savedEvents();
  app.innerHTML = background + (modalOpen ? modal() : "") + prototypeSwitcher();
  bind();
  if (modalOpen) activateModal();
  if (toast) document.querySelector(".toast")?.focus();
}

function updateUrl() {
  params.set("variant", variant); params.set("scenario", scenario);
  history.replaceState({}, "", `${location.pathname}?${params}`);
}

function navigate(nextVariant = variant, nextScenario = scenario) {
  variant = nextVariant; scenario = nextScenario; phase = nextScenario === "error" ? "error" : "idle"; modalOpen = true; toast = ""; destination = null; setupDirty = nextScenario === "discard";
  updateUrl(); render();
}

function cycle(direction) {
  const keys = Object.keys(variants);
  navigate(keys[(keys.indexOf(variant) + direction + keys.length) % keys.length], scenario);
}

function dismiss() {
  if (phase === "busy") return;
  modalOpen = false; phase = "idle"; render();
  requestAnimationFrame(() => (returnFocusSelector ? document.querySelector(returnFocusSelector) : document.querySelector("h1"))?.focus());
}

function activateModal() {
  const layer = document.querySelector(".modal-layer");
  const background = document.querySelector(".modal-background");
  background?.setAttribute("inert", "");
  background?.setAttribute("aria-hidden", "true");
  const focusables = () => [...layer.querySelectorAll('button:not(:disabled), select:not(:disabled), [tabindex]:not([tabindex="-1"])')];
  const dialogKeyHandler = (e) => {
    if (e.key === "Escape" && phase !== "busy") { e.preventDefault(); dismiss(); return; }
    if (!["INPUT", "TEXTAREA", "SELECT"].includes(e.target.tagName) && !e.target.isContentEditable && (e.key === "ArrowLeft" || e.key === "ArrowRight")) {
      e.preventDefault(); cycle(e.key === "ArrowLeft" ? -1 : 1); return;
    }
    if (e.key !== "Tab") return;
    const items = focusables(); if (!items.length) return;
    e.preventDefault();
    const current = items.indexOf(document.activeElement);
    items[(current + (e.shiftKey ? -1 : 1) + items.length) % items.length].focus();
  };
  layer.addEventListener("keydown", dialogKeyHandler);
  requestAnimationFrame(() => layer.querySelector("[data-autofocus]")?.focus());
}

async function confirmAction() {
  if (phase === "busy") return;
  phase = "busy"; render();
  await new Promise((resolve) => setTimeout(resolve, 900));
  if (params.get("fail") === "1") {
    params.delete("fail"); phase = "error"; render(); return;
  }
  toast = scenarioContent().success; modalOpen = false; phase = "idle";
  destination = scenario === "start" || scenario === "error" ? "active" : "saved";
  if (scenario === "discard") setupDirty = false;
  render();
}

function bind() {
  document.querySelector("[data-prev]")?.addEventListener("click", () => cycle(-1));
  document.querySelector("[data-next]")?.addEventListener("click", () => cycle(1));
  document.querySelector("[data-scenario]")?.addEventListener("change", (e) => navigate(variant, e.target.value));
  document.querySelector("[data-simulate]")?.addEventListener("click", () => { params.set("fail", "1"); confirmAction(); });
  document.querySelectorAll("[data-safe]").forEach((el) => el.addEventListener("click", dismiss));
  document.querySelector("[data-confirm]")?.addEventListener("click", confirmAction);
  document.querySelectorAll("[data-open-modal]").forEach((el) => el.addEventListener("click", (e) => { returnFocusSelector = `[data-open-modal="${e.currentTarget.dataset.openModal}"]`; navigate(variant, e.currentTarget.dataset.openModal); }));
  document.querySelector("[data-open-setup]")?.addEventListener("click", () => navigate(variant, "discard"));
  document.querySelector("[data-cancel-setup]")?.addEventListener("click", () => { returnFocusSelector = "[data-cancel-setup]"; modalOpen = setupDirty; render(); });
  document.querySelectorAll("[data-dirty-input]").forEach((el) => el.addEventListener("input", () => { setupDirty = true; }));
}

document.addEventListener("keydown", (e) => {
  if (modalOpen || ["INPUT", "TEXTAREA", "SELECT"].includes(e.target.tagName) || e.target.isContentEditable) return;
  if (e.key === "ArrowLeft") cycle(-1);
  if (e.key === "ArrowRight") cycle(1);
});

render();
