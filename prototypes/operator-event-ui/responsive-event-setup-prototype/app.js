// PROTOTYPE — three responsive Event setup structures, switchable via ?variant= and ?mode=.
const variants = {
  A: "Adaptive split",
  B: "Section rail",
  C: "Readiness canvas",
};

const modes = {
  standard: { label: "Standard", size: "1180 × 650", note: "≥ 1180 × 650" },
  compact: { label: "Compact", size: "900 × 600", note: "≥ 800 × 500" },
  stress: { label: "Stress", size: "760 × 460", note: "< 800 wide or 500 high" },
};

const states = {
  ready: "Ready — successful fields silent",
  checking: "Checking Camera",
  "camera-error": "Camera unavailable",
  "storage-low": "Insufficient storage",
  "name-required": "Event name required",
  saving: "Saving Event",
  starting: "Starting Event",
};

const params = new URLSearchParams(location.search);
let variant = variants[params.get("variant")] ? params.get("variant") : "A";
let mode = modes[params.get("mode")] ? params.get("mode") : "standard";
let state = states[params.get("state")] ? params.get("state") : "ready";
const app = document.querySelector("#app");

const previewUrl = "../dist/client/assets/wedding-camera-preview.png";

function field(label, value, helper, status = "ready") {
  return `<label class="field ${status}">
    <span class="field-label">${label}</span>
    <span class="control">${value}<span aria-hidden="true">⌄</span></span>
    ${helper ? `<small>${helper}</small>` : ""}
  </label>`;
}

function eventName() {
  const missing = state === "name-required";
  return `<label class="field ${missing ? "has-warning" : ""}">
    <span class="field-label">Event name</span>
    <span class="control input ${missing ? "placeholder" : ""}">${missing ? "Enter Event name" : "Luna &amp; Mateo Wedding"}</span>
    ${missing ? `<span class="field-message warning"><span aria-hidden="true">!</span>Enter an Event name to continue.</span>` : ""}
  </label>`;
}

function identity() {
  return `<section class="identity" aria-label="Full Event identity">
    <strong>Luna &amp; Mateo Wedding</strong>
    <span><i>Event ID</i><code>019fdb3f-3b84-7412-be30-7cc9123c31ac</code></span>
  </section>`;
}

function cameraFields() {
  const checking = state === "checking";
  const failed = state === "camera-error";
  return `<section class="field-group camera-fields">
    <label class="field ${failed ? "has-error" : ""}">
      <span class="field-label">Camera</span>
      <span class="control">${failed ? "FJ Camera 02 (unavailable)" : "Canon EOS R100 — Front Hall"}<span aria-hidden="true">⌄</span></span>
      ${checking ? `<span class="field-message checking"><span class="spinner" aria-hidden="true"></span>Checking Camera…</span>` : failed ? `<span class="field-message error"><span aria-hidden="true">×</span>Camera unavailable. Choose another Camera or try again.</span>` : ""}
    </label>
  </section>`;
}

function preview() {
  const checking = state === "checking";
  const failed = state === "camera-error";
  if (checking || failed) {
    return `<figure class="preview unavailable"><div>${checking ? `<span class="spinner" aria-hidden="true"></span><strong>Checking Camera…</strong>` : `<strong>Preview unavailable</strong><span>Choose another Camera or try again.</span>`}</div><figcaption><span>16:9 CAPTURE AREA</span></figcaption></figure>`;
  }
  return `<figure class="preview">
    <img src="${previewUrl}" alt="Crop-matched live preview from the selected Canon EOS R100" />
    <figcaption><span>LIVE · 16:9</span><span>Undistorted preview</span></figcaption>
  </figure>`;
}

function outputFields() {
  const lowStorage = state === "storage-low";
  return `<section class="field-group output-fields">
    ${field("Printer (optional)", "Not printing", "")}
    <div class="field storage-field ${lowStorage ? "has-error" : ""}">
      <span class="field-label">Storage</span>
      <span class="storage-path">C:\\Program Files\\FotoHAVN\\Events</span>
      <small>${lowStorage ? "480 MB free" : "120 GB free"}</small>
      ${lowStorage ? `<span class="field-message error"><span aria-hidden="true">×</span>Not enough space. Free up at least 1 GB to continue.</span>` : ""}
    </div>
  </section>`;
}

function readiness() {
  return `<section class="readiness" aria-label="Event readiness">
    <div class="section-heading"><div><span class="eyebrow">Readiness</span><h2>Ready to save or start</h2></div><strong>3 / 3</strong></div>
    <ul><li><span>✓</span><strong>Event name</strong><small>Ready</small></li><li><span>✓</span><strong>Camera</strong><small>Eligible</small></li><li><span>✓</span><strong>Storage</strong><small>74 GB free</small></li></ul>
    <p>Validation focuses and reveals the first unresolved requirement.</p>
  </section>`;
}

function header(subtitle) {
  return `<header class="setup-header"><div><span class="eyebrow">Event setup</span><h1>Edit Event</h1><p>${subtitle}</p></div><button class="close" aria-label="Close Event setup">×</button></header>`;
}

function footer() {
  const blocked = ["checking", "camera-error", "storage-low", "name-required"].includes(state);
  const busy = state === "saving" || state === "starting";
  return `<footer class="setup-footer"><button class="tertiary" ${busy ? "disabled" : ""}>Cancel</button><div>
    <button class="secondary" ${blocked || busy ? "disabled" : ""}>${state === "saving" ? `<span class="spinner" aria-hidden="true"></span>Saving Event…` : "Save &amp; Close"}</button>
    <button class="primary" ${blocked || busy ? "disabled" : ""}>${state === "starting" ? `<span class="spinner light" aria-hidden="true"></span>Starting Event…` : "Save &amp; Start Event"}</button>
  </div></footer>`;
}

function variantA() {
  return `<article class="setup variant-a">
    ${header("Name the Event and confirm its Camera, Printer, and storage.")}
    <div class="setup-scroll">
      <div class="a-grid">
        <div class="a-form">${identity()}${eventName()}${cameraFields()}${outputFields()}</div>
        <aside class="a-preview"><div class="preview-sticky"><div class="section-heading"><div><span class="eyebrow">Camera preview</span><h2>Live framing</h2></div></div>${preview()}</div></aside>
      </div>
    </div>
    ${footer()}
  </article>`;
}

function sectionNav() {
  return `<nav class="section-nav" aria-label="Event setup sections">
    <button class="active"><span>1</span><strong>Event</strong><small>Name and identity</small></button>
    <button><span>2</span><strong>Camera</strong><small>Eligible · ready</small></button>
    <button><span>3</span><strong>Output</strong><small>No Printer · storage ready</small></button>
    <div class="nav-readiness"><span>✓</span><div><strong>Ready to start</strong><small>All requirements resolved</small></div></div>
  </nav>`;
}

function variantB() {
  return `<article class="setup variant-b">
    ${header("Move through setup by section; readiness stays visible.")}
    <div class="setup-scroll b-layout">
      ${sectionNav()}
      <main class="b-workspace">
        <section class="workspace-title"><span class="eyebrow">Section 1 of 3</span><h2>Event details</h2><p>Confirm the Event identity before checking equipment.</p></section>
        ${identity()}${eventName()}${cameraFields()}${preview()}${outputFields()}${readiness()}
      </main>
    </div>
    ${footer()}
  </article>`;
}

function summaryCard(kind, title, detail, body) {
  return `<section class="summary-card ${kind}"><div class="summary-head"><span class="summary-icon">✓</span><div><span class="eyebrow">${title}</span><h2>${detail}</h2></div><button>Edit</button></div>${body}</section>`;
}

function variantC() {
  return `<article class="setup variant-c">
    ${header("Review the complete setup, then edit only what needs attention.")}
    <div class="setup-scroll c-layout">
      <div class="c-hero">${identity()}<div><span class="eyebrow">Event readiness</span><h2>Everything is ready</h2><p>Camera and storage checks passed. No Printer is selected.</p></div><strong class="score">3 / 3</strong></div>
      <div class="c-cards">
        ${summaryCard("event-card", "Event", "Luna & Mateo Wedding", `<p>Duplicate names are allowed; the full Event ID stays attached to consequential actions.</p>`)}
        ${summaryCard("camera-card", "Camera", "Canon EOS R100", `${preview()}<p>Eligible Camera · 6000 × 4000 JPEG · Front Hall</p>`)}
        ${summaryCard("output-card", "Output", "No Printer", `<dl><div><dt>Storage</dt><dd>Ready · 74 GB free</dd></div><div><dt>Minimum</dt><dd>1 GB</dd></div></dl>`)}
      </div>
      <div class="validation-note"><span>↳</span><p><strong>If validation fails</strong><br />The first unresolved card opens, receives focus, and scrolls fully into view.</p></div>
    </div>
    ${footer()}
  </article>`;
}

function switcher() {
  return `<div class="prototype-switcher" aria-label="Prototype controls">
    <button data-prev aria-label="Previous variant">←</button>
    <strong>${variant} — ${variants[variant]}</strong>
    <button data-next aria-label="Next variant">→</button>
    <span class="divider"></span>
    <label>Effective area <select data-mode>${Object.entries(modes).map(([key, item]) => `<option value="${key}" ${key === mode ? "selected" : ""}>${item.label} · ${item.size}</option>`).join("")}</select></label>
    ${variant === "A" ? `<label>State <select data-state>${Object.entries(states).map(([key, label]) => `<option value="${key}" ${key === state ? "selected" : ""}>${label}</option>`).join("")}</select></label>` : ""}
  </div>`;
}

function stateNote() {
  const item = modes[mode];
  return `<aside class="state-note"><strong>Prototype state</strong><span>${item.label}</span><small>${item.size} · ${item.note}</small><small>${variant === "A" ? states[state] : "Exploratory comparison"}</small><small>Sticky header + 80 px footer · no horizontal scroll</small></aside>`;
}

function render() {
  const body = variant === "A" ? variantA() : variant === "B" ? variantB() : variantC();
  app.innerHTML = `<main class="prototype-shell mode-${mode}"><div class="app-chrome"><span><b>F</b> FotoHAVN</span><strong>OPERATOR CONSOLE</strong></div><div class="scrim">${body}</div></main>${stateNote()}${switcher()}`;
  if (variant === "A" && ["checking", "camera-error", "storage-low", "name-required"].includes(state)) {
    requestAnimationFrame(() => {
      const target = state === "storage-low"
        ? document.querySelector(".variant-a .storage-field")
        : state === "name-required"
          ? document.querySelector(".variant-a .a-form > .field")
          : document.querySelector(".variant-a .camera-fields .field");
      target?.scrollIntoView({ block: "center" });
    });
  }
}

function setQuery(nextVariant, nextMode, nextState = state) {
  const url = new URL(location.href);
  url.searchParams.set("variant", nextVariant);
  url.searchParams.set("mode", nextMode);
  url.searchParams.set("state", nextState);
  history.replaceState({}, "", url);
  variant = nextVariant;
  mode = nextMode;
  state = nextState;
  render();
}

function cycle(direction) {
  const keys = Object.keys(variants);
  setQuery(keys[(keys.indexOf(variant) + direction + keys.length) % keys.length], mode, state);
}

document.addEventListener("click", event => {
  if (event.target.closest("[data-prev]")) cycle(-1);
  if (event.target.closest("[data-next]")) cycle(1);
});

document.addEventListener("change", event => {
  if (event.target.matches("[data-mode]")) setQuery(variant, event.target.value);
  if (event.target.matches("[data-state]")) setQuery(variant, mode, event.target.value);
});

document.addEventListener("keydown", event => {
  if (event.target.matches("input, textarea, select, [contenteditable]")) return;
  if (event.key === "ArrowLeft") cycle(-1);
  if (event.key === "ArrowRight") cycle(1);
  if (["1", "2", "3"].includes(event.key)) setQuery(variant, Object.keys(modes)[Number(event.key) - 1], state);
});

render();
