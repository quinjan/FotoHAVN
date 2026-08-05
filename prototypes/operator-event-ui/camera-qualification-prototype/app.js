// PROTOTYPE — throwaway UI for comparing three Scanning Available Cameras models.
const variants = {
  A: "Scan results list",
  B: "Inspection workspace",
  C: "Compact control",
};

const scenarios = {
  1: "Scanning Available Cameras",
  2: "Eligible Camera selected",
  3: "Mixed eligible & rejected",
  4: "No Eligible Camera",
  5: "Cancel on close",
  6: "Scanning saved Camera for activation",
  7: "Rescan selected Camera",
  8: "Capture output changed",
  9: "Activation returned: output changed",
  10: "Duplicate Camera names",
  11: "Keyboard-readable rejections",
  12: "Storage stops scanning",
};

const cameras = [
  { id: "canon", name: "Canon EOS R100 — USB Video Device (Front Hall Booth Position)", output: "6000 × 4000 JPEG", status: "eligible" },
  { id: "brio", name: "Logitech BRIO 4K Stream Edition", output: "3840 × 2160 JPEG", status: "eligible" },
  { id: "utility", name: "Canon EOS Webcam Utility", reason: "No Photo mode meets the minimum 1280 × 720 resolution.", status: "rejected" },
  { id: "obs", name: "OBS Virtual Camera — FotoHAVN Composite Output", reason: "Photo modes could not produce a valid JPEG Capture.", status: "rejected" },
  { id: "integrated", name: "Integrated Camera", reason: "Camera is in use by another app. Close the other app, then retry.", status: "rejected" },
  { id: "missing", name: "Saved Camera: USB Camera (2BC5:0403)", reason: "Saved Camera identity is missing or matches more than one device.", status: "rejected" },
  { id: "privacy", name: "USB Camera — Back Hall", reason: "Camera access was denied. Allow FotoHAVN camera access in Windows Settings.", status: "rejected" },
  { id: "preview", name: "Microsoft LifeCam Cinema", reason: "Live preview is below the minimum size or frame-rate requirement.", status: "rejected" },
  { id: "storage", name: "Sony Imaging Edge Virtual Camera", reason: "Temporary Capture storage could not write and clean up a probe file.", status: "rejected" },
  { id: "usb-front", name: "USB Camera", disambiguator: "Front hall USB port · 0BDA:5596", output: "1920 × 1080 JPEG", status: "eligible" },
  { id: "usb-back", name: "USB Camera", disambiguator: "Back hall USB port · 2BC5:0403", output: "2560 × 1440 JPEG", status: "eligible" },
];

let params = new URLSearchParams(location.search);
let variant = variants[params.get("variant")] ? params.get("variant") : "A";
let scenario = scenarios[params.get("scenario")] ? Number(params.get("scenario")) : 1;
let setupOpen = true;
let activationFailed = false;
let selectedId = scenario === 10 ? "usb-front" : "canon";
let cameraMenuOpen = false;
let cameraMenuAnchor = null;
let tuningOpen = false;

const app = document.querySelector("#app");
const previewUrl = "../dist/client/assets/wedding-camera-preview.png";

function icon(kind) {
  const icons = { check: "✓", reject: "!", spinner: "", camera: "▣", chevron: "⌄", refresh: "↻", close: "×", gear: "⚙" };
  return `<span class="icon icon-${kind}" aria-hidden="true">${icons[kind]}</span>`;
}

function statusPill(status, text) {
  return `<span class="status-pill ${status}">${status === "progress" ? icon("spinner") : icon(status === "eligible" ? "check" : "reject")}${text}</span>`;
}

function tuningTrigger(disabled) {
  return `<button class="tuning-trigger" data-tuning aria-label="Camera tuning" aria-expanded="${tuningOpen}" title="Camera tuning" ${disabled ? "disabled" : ""}>${icon("gear")}</button>`;
}

function collapsedTuningHint() {
  return `<div class="tuning-collapsed">${icon("gear")}<div><strong>Camera Tuning is collapsed</strong><span>Use the gear button to show the mirrored preview and supported controls.</span></div></div>`;
}

function preview() {
  return `<figure class="preview">
    <img src="${previewUrl}" alt="Crop-matched live preview from the selected Canon EOS R100" />
    <figcaption><span>LIVE · 3:2 CROP</span><span>Framing matches the Capture</span></figcaption>
  </figure>`;
}

function tuningControls(disabled = false, className = "") {
  const controls = [
    ["Brightness", -100, 100, 12, "+12"],
    ["Contrast", -100, 100, 8, "+8"],
    ["Exposure compensation", -20, 20, 3, "+0.3"],
    ["White balance", 2800, 7000, 5600, "5600K"],
  ];
  return `<section class="tuning-panel ${className} ${disabled ? "tuning-disabled" : ""}" aria-label="Camera tuning">
    <div class="tuning-head"><div><p class="eyebrow">Camera tuning</p><small>Remembered per Camera for this Event.</small></div><button ${disabled ? "disabled" : ""}>Reset</button></div>
    <div class="tuning-controls">${controls.map(([name, min, max, value, display]) => `<label><span>${name}</span><input type="range" min="${min}" max="${max}" value="${value}" ${disabled ? "disabled" : ""}/><output>${display}</output></label>`).join("")}</div>
    ${disabled ? `<p class="tuning-lock">Tuning is temporarily unavailable while Scanning Available Cameras runs.</p>` : ""}
  </section>`;
}

function rejectedRows(limit = 4) {
  return cameras.filter(c => c.status === "rejected" && c.id !== "storage").sort((a, b) => a.name.localeCompare(b.name)).slice(0, limit).map(c => `
    <div class="device-row rejected" aria-disabled="true">
      <span class="device-mark">${icon("reject")}</span>
      <span class="device-copy"><strong>${c.name}</strong><small>${c.reason}</small></span>
      <span class="disabled-label">Unavailable</span>
    </div>`).join("");
}

function progressRows() {
  return `
    <div class="device-row progress"><span class="device-mark">${icon("spinner")}</span><span class="device-copy"><strong>${cameras[0].name}</strong><small>Testing Photo mode 3 of 5 · validating JPEG…</small></span></div>
    <div class="device-row queued"><span class="device-mark">3</span><span class="device-copy"><strong>${cameras[1].name}</strong><small>Waiting to scan</small></span></div>
    <div class="device-row rejected" aria-disabled="true"><span class="device-mark">${icon("reject")}</span><span class="device-copy"><strong>${cameras[2].name}</strong><small>${cameras[2].reason}</small></span></div>`;
}

function commonState() {
  const rescan = scenario === 7;
  const outputChanged = scenario === 8 || scenario === 9;
  const duplicates = scenario === 10;
  const rejectionReview = scenario === 11;
  const storageBlocked = scenario === 12;
  const noEligible = scenario === 4 || activationFailed || storageBlocked;
  const scanning = scenario === 1 || scenario === 5 || (scenario === 6 && !activationFailed) || rescan;
  const mixed = scenario === 3 || duplicates || rejectionReview;
  const selected = scenario === 2 || mixed || rescan || outputChanged;
  return { noEligible, scanning, mixed, selected, rescan, outputChanged, duplicates, rejectionReview, storageBlocked };
}

function variantA() {
  const s = commonState();
  const heading = s.noEligible ? "No Eligible Camera found" : s.scanning ? (scenario === 6 ? "Scanning saved Camera before activation" : "Scanning Available Cameras…") : "Camera ready";
  const lead = s.noEligible
    ? (activationFailed ? "Event activation stopped because the saved Camera is now in use by another app." : "Event setup is blocked until a Camera passes preview and JPEG Capture checks.")
    : s.scanning ? "You can continue setup while FotoHAVN scans every detected Camera. Unvalidated Cameras cannot be selected."
    : "The best validated JPEG output is selected automatically.";
  const rows = s.scanning ? progressRows() : s.noEligible ? rejectedRows(7) : `
    <button class="device-row eligible selected" data-select="canon" aria-pressed="true"><span class="device-mark">${icon("check")}</span><span class="device-copy"><strong>${cameras[0].name}</strong><small>Capture output: ${cameras[0].output}</small></span><span class="selected-label">Selected</span></button>
    ${s.mixed ? `<button class="device-row eligible" data-select="brio"><span class="device-mark">${icon("check")}</span><span class="device-copy"><strong>${cameras[1].name}</strong><small>Capture output: ${cameras[1].output}</small></span></button>${rejectedRows(3)}` : ""}`;
  return `<section class="camera-panel variant-a">
    <div class="panel-head"><div><p class="eyebrow">Scanning Available Cameras</p><h3>${heading}</h3><p>${lead}</p></div><div class="panel-actions">${s.scanning ? statusPill("progress", "2 of 4 checked") : s.noEligible ? statusPill("rejected", "Setup blocked") : statusPill("eligible", "Eligible")}${tuningTrigger(!s.selected)}</div></div>
    ${s.scanning ? `<div class="progress-track"><span></span></div>` : ""}
    ${s.noEligible ? `<div class="action-alert">${icon("reject")}<div><strong>${activationFailed ? "Camera is in use by another app" : "Connect or free a Camera"}</strong><p>${activationFailed ? "Close the other app, then scan available Cameras again." : "Check camera access, close other camera apps, or connect a Camera with a usable Photo mode."}</p></div><button data-retry>Scan again</button></div>` : ""}
    <div class="a-grid"><div class="device-list" role="listbox" aria-label="Detected Cameras">${rows}</div><div class="selection-detail">${s.selected ? `<div class="output-line"><span>${icon("check")}</span><div><small>CAPTURE OUTPUT</small><strong>${selectedId === "brio" ? cameras[1].output : cameras[0].output}</strong><p>Chosen automatically from validated Photo modes.</p></div></div>${tuningOpen ? `<div class="a-expanded-tuning">${preview()}${tuningControls(false, "tuning-a")}</div>` : collapsedTuningHint()}` : `<div class="preview-placeholder">${icon(s.noEligible ? "camera" : "spinner")}<strong>${s.noEligible ? "Preview unavailable" : "Preview starts after Scanning Available Cameras"}</strong><span>${s.noEligible ? "No Camera can be selected." : "Temporary probes are deleted immediately."}</span></div>`}</div></div>
  </section>`;
}

function railItem(camera, state, meta) {
  return `<button class="rail-item ${state}" ${state === "rejected" || state === "queued" ? "disabled" : ""}><span>${state === "progress" ? icon("spinner") : state.includes("eligible") ? icon("check") : state === "rejected" ? icon("reject") : "·"}</span><span><strong>${camera.name}</strong><small>${meta}</small></span></button>`;
}

function variantB() {
  const s = commonState();
  const rail = s.scanning ? railItem(cameras[0], "progress", "Testing 3 of 5 modes") + railItem(cameras[1], "queued", "Waiting") + railItem(cameras[2], "rejected", "Below requirements")
    : s.noEligible ? cameras.slice(2).map(c => railItem(c, "rejected", c.reason.split(".")[0])).join("")
    : railItem(cameras[0], "eligible active", "6000 × 4000 JPEG") + (s.mixed ? railItem(cameras[1], "eligible", "3840 × 2160 JPEG") + cameras.slice(2,5).map(c => railItem(c, "rejected", c.reason.split(".")[0])).join("") : "");
  return `<section class="camera-panel variant-b">
    <aside class="camera-rail"><div class="rail-head"><div><p class="eyebrow">Detected Cameras</p><strong>${s.scanning ? "Scanning 2 of 4" : s.noEligible ? "0 Eligible Cameras" : s.mixed ? "2 of 5 eligible" : "1 Eligible Camera"}</strong></div><button class="icon-button" data-retry aria-label="Scan Available Cameras">${icon("refresh")}</button></div><div class="rail-list">${rail}</div></aside>
    <div class="inspection">
      <div class="inspection-head"><div>${statusPill(s.scanning ? "progress" : s.noEligible ? "rejected" : "eligible", s.scanning ? (scenario === 6 ? "Activation scan" : "Scanning") : s.noEligible ? "Cannot select" : "Selected")}</div><div class="inspection-actions"><p>${s.scanning ? "Testing actual JPEG Captures" : s.noEligible ? "Resolve the primary reason, then retry" : "Best validated output selected automatically"}</p>${tuningTrigger(!s.selected)}</div></div>
      ${s.noEligible ? `<div class="inspection-empty">${icon("camera")}<h3>${activationFailed ? "Activation returned to setup" : "No Camera can be used"}</h3><p>${activationFailed ? "The saved Camera is in use by another app. Close it, then scan again." : cameras[4].reason}</p><button class="primary" data-retry>Scan Available Cameras</button></div>`
      : s.scanning ? `<div class="mode-probe"><div class="probe-visual"><span class="focus-frame"></span>${icon("spinner")}</div><p class="eyebrow">${scenario === 6 ? "EVENT ACTIVATION" : "CANON EOS R100"}</p><h3>${scenario === 6 ? "Confirming the saved Camera is still ready" : "Producing a test Capture"}</h3><p>${scenario === 6 ? "Checking identity, preview, Photo modes, and temporary Capture storage before the Event starts." : "Mode 3 of 5 · 3840 × 2160 · Source format: Other"}</p><div class="probe-steps"><span class="done">Identity</span><span class="done">Preview</span><span class="current">JPEG Capture</span><span>Cleanup</span></div>${scenario === 6 ? `<button class="text-button" data-fail>Show activation failure</button>` : ""}</div>`
      : tuningOpen ? `<div class="inspection-selected">${preview()}<div class="selected-sidebar"><div class="spec-block"><p class="eyebrow">SELECTED CAMERA</p><h3>${cameras[0].name}</h3><dl><div><dt>Capture output</dt><dd>${cameras[0].output}</dd></div><div><dt>Framing</dt><dd>3:2 center crop</dd></div></dl></div>${tuningControls(false, "tuning-b")}</div></div>` : `<div class="collapsed-selection"><p class="eyebrow">SELECTED CAMERA</p><h3>${cameras[0].name}</h3><strong>Capture output: ${cameras[0].output}</strong>${collapsedTuningHint()}</div>`}
    </div>
  </section>`;
}

function compactOption(camera, state, detail) {
  const unavailable = state === "rejected" || state === "progress";
  const selectable = state === "selected" || state === "eligible";
  return `<button class="compact-option ${state}" role="option" aria-selected="${state === "selected"}" ${unavailable ? `aria-disabled="true"` : ""} ${selectable ? `data-camera-id="${camera.id}"` : ""}><span class="radio">${state === "selected" ? "●" : state === "eligible" ? "○" : state === "progress" ? icon("spinner") : icon("reject")}</span><span><strong>${camera.name}</strong>${camera.disambiguator ? `<span class="camera-disambiguator">${camera.disambiguator}</span>` : ""}<small>${detail}</small></span>${state === "selected" ? `<em>Selected</em>` : ""}</button>`;
}

function cameraOptions(state) {
  const rejected = cameras.filter(camera => camera.status === "rejected" && camera.id !== "storage").sort((a, b) => a.name.localeCompare(b.name));
  if (state.storageBlocked) {
    return `<div class="compact-empty">Camera results are unavailable until temporary Capture storage is writable.</div>`;
  }
  if (state.scanning) {
    return compactOption(cameras[0], "progress", state.rescan ? "Rechecking Photo modes" : "Testing Photo mode 3 of 5") +
      compactOption(cameras[1], "progress", "Waiting to scan") +
      compactOption(cameras[2], "rejected", cameras[2].reason);
  }
  if (state.noEligible) {
    return rejected.slice(0, 7).map(camera => compactOption(camera, "rejected", camera.reason)).join("");
  }
  if (state.duplicates) {
    return [cameras[9], cameras[10]].map(camera => compactOption(camera, camera.id === selectedId ? "selected" : "eligible", `Capture output: ${camera.output}`)).join("");
  }
  const eligible = [cameras[0], cameras[1]].sort((a, b) => a.name.localeCompare(b.name));
  const eligibleOptions = eligible.map(camera => compactOption(camera, camera.id === selectedId ? "selected" : "eligible", `Capture output: ${camera.output}`)).join("");
  const rejectedOptions = state.mixed ? rejected.slice(0, state.rejectionReview ? 6 : 3).map(camera => compactOption(camera, "rejected", camera.reason)).join("") : "";
  return eligibleOptions + rejectedOptions;
}

function cameraMenuPopover() {
  if (!cameraMenuOpen || !cameraMenuAnchor || variant !== "C") return "";
  const s = commonState();
  const style = `left:${cameraMenuAnchor.left}px;top:${cameraMenuAnchor.top}px;width:${cameraMenuAnchor.width}px`;
  const heading = s.storageBlocked ? "Scanning Available Cameras stopped" : s.scanning ? "Scanning Available Cameras" : s.noEligible ? (activationFailed ? "Event activation stopped" : "No Eligible Camera found") : "Choose an Eligible Camera";
  const detail = s.storageBlocked ? "Fix temporary Capture storage, then scan again." : s.scanning ? "Selection stays unavailable until the complete scan finishes." : s.noEligible ? (activationFailed ? "Camera is in use by another app. You are back in setup." : "Resolve a reason below, then scan again.") : "Output resolution is selected automatically.";
  return `<div class="select-popover portal-popover" id="camera-options" role="listbox" aria-label="Available Cameras" style="${style}"><div class="popover-status"><span>${s.scanning ? icon("spinner") : s.noEligible ? icon("reject") : icon("check")}</span><div><strong>${heading}</strong><small>${detail}</small></div>${scenario === 6 && !activationFailed ? `<button data-fail>Show failure</button>` : ""}</div><div class="compact-options">${cameraOptions(s)}</div></div>`;
}

function variantC() {
  const s = commonState();
  const selectedCamera = cameras.find(camera => camera.id === selectedId) || cameras[0];
  const selectedOutput = s.outputChanged ? "1280 × 960 JPEG" : selectedCamera.output;
  const label = s.rescan ? "Scanning Available Cameras…" : s.scanning ? (scenario === 6 ? "Scanning saved Camera before Event activation…" : "Scanning Available Cameras…") : s.storageBlocked ? "Scanning Available Cameras stopped" : s.noEligible ? "No Eligible Camera" : selectedCamera.disambiguator ? `${selectedCamera.name} — ${selectedCamera.disambiguator}` : selectedCamera.name;
  const changeMessage = scenario === 9
    ? `<div class="output-change" role="status"><strong>Event activation paused</strong><span>Capture output changed from 6000 × 4000 to 1280 × 960 JPEG. Review the change, then save and start.</span></div>`
    : scenario === 8 ? `<div class="output-change" role="status"><strong>Capture output changed</strong><span>6000 × 4000 → 1280 × 960 JPEG. The Camera configuration has unsaved changes.</span></div>` : "";
  return `<section class="camera-panel variant-c">
    <div class="compact-top"><div><div class="title-row"><h3>Camera</h3></div><p>FotoHAVN scans actual JPEG Captures and chooses the best output.</p></div><div class="compact-actions"><button class="text-button" data-retry ${s.scanning ? "disabled" : ""}>${icon("refresh")} Scan Available Cameras</button>${tuningTrigger(!s.selected || s.scanning)}</div></div>
    <label class="compact-label">Selected Camera</label>
    <button class="select-trigger ${s.noEligible ? "invalid" : ""}" data-camera-menu aria-haspopup="listbox" aria-controls="camera-options" aria-expanded="${cameraMenuOpen}" ${s.scanning || s.storageBlocked ? "disabled" : ""}><span>${label}</span>${icon("chevron")}</button>
    ${s.selected && tuningOpen && !s.scanning ? `<div class="c-tuning-region"><div class="c-scanned-layout"><div class="compact-summary">${preview()}<div class="preview-meta"><span>Capture output · ${selectedOutput}</span></div></div>${tuningControls(false, "tuning-c")}</div></div>` : ""}
    ${s.rescan ? `<div class="scan-state" role="status">${icon("spinner")}<div><strong>Scanning all detected Cameras</strong><span>Live preview and Camera tuning will return if the same exact Camera remains eligible.</span></div></div>` : ""}
    ${changeMessage}
    ${s.storageBlocked ? `<div class="storage-blocker" role="alert"><strong>Temporary Capture storage is unavailable</strong><span>Scanning Available Cameras could not finish. Fix executable-relative storage, then scan again.</span><button data-retry>Scan Available Cameras</button></div>` : ""}
    ${s.noEligible && !s.storageBlocked ? `<div class="inline-resolution"><strong>How to continue</strong><span>${activationFailed ? "Close the app using the Camera, then scan again." : "Check access, close other camera apps, or connect another Camera."}</span><button data-retry>Scan Available Cameras</button></div>` : ""}
  </section>`;
}

function modal() {
  const content = variant === "A" ? variantA() : variant === "B" ? variantB() : variantC();
  const s = commonState();
  const compactCamera = variant === "C" && !tuningOpen;
  return `<div class="scrim"><section class="modal ${compactCamera ? "compact-camera" : ""}" role="dialog" aria-modal="true" aria-labelledby="setup-title">
    <header class="modal-head"><div><p class="eyebrow">Event setup</p><h2 id="setup-title">Edit Event</h2><p>Update the Event name, Camera, or Printer before the Event starts.</p></div><button class="close-button" data-close aria-label="Close Event setup">${icon("close")}</button></header>
    <div class="setup-summary"><label>Event name<input value="Mika & Paolo's Wedding" /></label><label>Printer<select><option>No Printer</option></select></label></div>
    ${content}
    <footer class="modal-foot"><button data-close>Cancel</button><div><button class="secondary" ${s.noEligible || s.scanning ? "disabled" : ""}>Save & Close</button><button class="primary" ${s.noEligible || s.scanning ? "disabled" : ""}>${s.scanning && scenario === 6 ? `${icon("spinner")} Scanning Camera…` : "Save & Start Event"}</button></div></footer>
  </section>${cameraMenuPopover()}</div>`;
}

function underlay() {
  return `<div class="shell"><header><a><b>F</b> FotoHAVN</a><span>OPERATOR CONSOLE</span></header><main><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><p>Open one to adjust its setup, or start a Guest Cycle.</p><div class="event-grid"><article><strong>Mika & Paolo's Wedding</strong><small>Saved today, 9:18 PM</small></article><article><strong>Saturday Market</strong><small>Saved Jul 28, 2026, 11:06 AM</small></article><article><strong>Year-End Party</strong><small>Saved Jul 17, 2026, 9:18 PM</small></article></div></main></div>`;
}

function controls() {
  return `<aside class="prototype-controls" aria-label="Prototype controls"><button data-prev aria-label="Previous variant">←</button><strong>${variant} — ${variants[variant]}</strong><button data-next aria-label="Next variant">→</button><span class="divider"></span><label>Scenario<select data-scenario>${Object.entries(scenarios).map(([key, name]) => `<option value="${key}" ${scenario === Number(key) ? "selected" : ""}>${key}. ${name}</option>`).join("")}</select></label></aside>`;
}

function scenarioNote() {
  const hint = scenario === 5 ? "Press × or Escape to cancel Scanning Available Cameras." : scenario === 6 && !activationFailed ? "Open the Camera menu, then use “Show failure”." : scenario >= 10 ? "Use the scenario picker for states 10–12." : "Use the scenario picker or number keys.";
  return `<div class="scenario-note"><strong>Scenario ${scenario}</strong><span>${scenarios[scenario]}</span><small>${hint}</small></div>`;
}

function canceledReceipt() {
  return `<div class="cancel-receipt" role="status"><span>${icon("check")}</span><div><strong>Event setup closed</strong><p>Scanning Available Cameras was canceled. Temporary probe files were cleaned up.</p></div><button data-reopen>Reopen setup</button></div>`;
}

function render() {
  app.innerHTML = underlay() + (setupOpen ? modal() : canceledReceipt()) + scenarioNote() + controls();
  bind();
}

function navigate(nextVariant = variant, nextScenario = scenario) {
  variant = nextVariant;
  scenario = Number(nextScenario);
  setupOpen = true;
  activationFailed = false;
  cameraMenuOpen = false;
  cameraMenuAnchor = null;
  tuningOpen = false;
  selectedId = scenario === 10 ? "usb-front" : "canon";
  params.set("variant", variant);
  params.set("scenario", scenario);
  history.replaceState({}, "", `${location.pathname}?${params}`);
  render();
}

function cycle(direction) {
  const keys = Object.keys(variants);
  navigate(keys[(keys.indexOf(variant) + direction + keys.length) % keys.length], scenario);
}

function bind() {
  document.querySelector("[data-prev]")?.addEventListener("click", () => cycle(-1));
  document.querySelector("[data-next]")?.addEventListener("click", () => cycle(1));
  document.querySelector("[data-scenario]")?.addEventListener("change", e => navigate(variant, e.target.value));
  document.querySelectorAll("[data-close]").forEach(el => el.addEventListener("click", () => { setupOpen = false; render(); }));
  document.querySelector("[data-reopen]")?.addEventListener("click", () => { setupOpen = true; render(); });
  document.querySelectorAll("[data-retry]").forEach(el => el.addEventListener("click", () => navigate(variant, 1)));
  document.querySelectorAll("[data-fail]").forEach(el => el.addEventListener("click", () => { activationFailed = true; render(); }));
  document.querySelectorAll("[data-select]").forEach(el => el.addEventListener("click", () => { selectedId = el.dataset.select; tuningOpen = false; render(); }));
  document.querySelector("[data-camera-menu]")?.addEventListener("click", event => {
    if (!cameraMenuOpen) {
      const rect = event.currentTarget.getBoundingClientRect();
      cameraMenuAnchor = { left: Math.round(rect.left), top: Math.round(rect.bottom + 4), width: Math.round(rect.width) };
    }
    cameraMenuOpen = !cameraMenuOpen;
    render();
  });
  document.querySelector("[data-tuning]")?.addEventListener("click", () => { tuningOpen = !tuningOpen; render(); });
  document.querySelectorAll(".compact-option[data-camera-id]").forEach(el => el.addEventListener("click", () => { selectedId = el.dataset.cameraId; cameraMenuOpen = false; tuningOpen = false; render(); }));
  document.querySelector(".scrim")?.addEventListener("click", event => {
    if (cameraMenuOpen && !event.target.closest(".select-trigger, .select-popover")) { cameraMenuOpen = false; render(); }
  });
}

document.addEventListener("keydown", event => {
  const tag = event.target.tagName;
  if (["INPUT", "TEXTAREA", "SELECT"].includes(tag) || event.target.isContentEditable) return;
  if (event.key === "ArrowLeft") cycle(-1);
  if (event.key === "ArrowRight") cycle(1);
  if (/^[1-9]$/.test(event.key)) navigate(variant, Number(event.key));
  if (event.key === "Escape" && cameraMenuOpen) { cameraMenuOpen = false; render(); return; }
  if (event.key === "Escape" && tuningOpen) { tuningOpen = false; render(); return; }
  if (event.key === "Escape" && setupOpen) { setupOpen = false; render(); }
});

render();
