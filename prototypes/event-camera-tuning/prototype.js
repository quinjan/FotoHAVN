// THROWAWAY PROTOTYPE — visual exploration for FotoHAVN issue #18.
// Three centered-modal Camera Tuning variants, switchable via ?variant=.
const variants = {
  A: { name: "Camera section", note: "Selected: Camera and its gear-triggered tuning share one section" },
  B: { name: "Focused tuning step", note: "Advanced mode temporarily replaces the basic Event form" },
  C: { name: "Tuning dialog", note: "Advanced controls open in a focused layer above Event setup" },
};

const cameras = {
  "Integrated Camera": {
    status: "Ready · 1920 × 1080 · 30 fps",
    controls: ["brightness", "contrast", "saturation", "exposure"],
    defaults: { brightness: 52, contrast: 48, saturation: 50, exposure: 44, temperature: 50, focus: 50, autoExposure: true, autoFocus: true },
  },
  "Logitech BRIO": {
    status: "Ready · 3840 × 2160 · 30 fps",
    controls: ["brightness", "contrast", "saturation", "temperature", "exposure", "focus"],
    defaults: { brightness: 58, contrast: 54, saturation: 62, exposure: 38, temperature: 46, focus: 64, autoExposure: false, autoFocus: false },
  },
  "USB Camera": {
    status: "Ready · 1280 × 720 · 30 fps",
    controls: ["brightness", "contrast", "exposure"],
    defaults: { brightness: 47, contrast: 52, saturation: 50, exposure: 57, temperature: 50, focus: 50, autoExposure: false, autoFocus: true },
  },
};

const labels = {
  brightness: "Brightness", contrast: "Contrast", saturation: "Saturation",
  temperature: "White balance", exposure: "Exposure", focus: "Focus",
};

const params = new URLSearchParams(location.search);
const state = {
  variant: Object.hasOwn(variants, params.get("variant")) ? params.get("variant") : "A",
  mode: params.get("mode") === "edit" ? "edit" : "create",
  advanced: params.get("advanced") === "open",
  name: params.get("mode") === "edit" ? "Mika & Paolo's Wedding" : "",
  camera: params.get("mode") === "edit" ? "Logitech BRIO" : "Integrated Camera",
  tuning: Object.fromEntries(Object.entries(cameras).map(([name, camera]) => [name, structuredClone(camera.defaults)])),
  confirmation: null,
  saved: null,
};
state.saved = snapshot();
if (state.mode === "edit" && ["discard", "save", "save-start"].includes(params.get("dialog"))) {
  state.tuning[state.camera].brightness += 4;
  state.confirmation = params.get("dialog") === "discard"
    ? { type: "discard" }
    : { type: "save", action: params.get("dialog") };
}
if (state.mode === "edit" && !state.confirmation) {
  if (params.get("dirty") === "name") state.name += "s";
  if (params.get("dirty") === "camera") state.camera = "Integrated Camera";
  if (params.get("dirty") === "tuning") state.tuning[state.camera].brightness += 4;
}

const app = document.querySelector("#app");
const switcher = document.querySelector("#switcher");

function snapshot() {
  return JSON.stringify({ name: state.name, camera: state.camera, tuning: state.tuning });
}
function esc(value) {
  return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll('"', "&quot;");
}
function dirty() { return snapshot() !== state.saved; }
function savedConfiguration() { return JSON.parse(state.saved); }
function nameIsDirty() { return state.name !== savedConfiguration().name; }
function selectedCameraIsDirty() { return state.camera !== savedConfiguration().camera; }
function tuningControlIsDirty(key, camera = state.camera) {
  const current = state.tuning[camera];
  const saved = savedConfiguration().tuning[camera];
  if (current[key] !== saved[key]) return true;
  if (key === "exposure") return current.autoExposure !== saved.autoExposure;
  if (key === "focus") return current.autoFocus !== saved.autoFocus;
  return false;
}
function cameraTuningIsDirty(camera = state.camera) {
  return JSON.stringify(state.tuning[camera]) !== JSON.stringify(savedConfiguration().tuning[camera]);
}
function anyCameraTuningIsDirty() { return Object.keys(cameras).some(cameraTuningIsDirty); }
function cameraSectionIsDirty() { return anyCameraTuningIsDirty(); }
function cameraData() { return cameras[state.camera]; }
function currentTuning() { return state.tuning[state.camera]; }
function cameraIsCustomized() { return JSON.stringify(currentTuning()) !== JSON.stringify(cameraData().defaults); }
function previewFilter() {
  const tuning = currentTuning();
  return `brightness(${.55 + tuning.brightness / 112}) contrast(${.62 + tuning.contrast / 130}) saturate(${.55 + tuning.saturation / 90})`;
}
function modeCopy() {
  return state.mode === "edit"
    ? { eyebrow: "Event setup", title: "Edit Event", intro: "Update the Event name or Camera. Camera Tuning is available when you need it." }
    : { eyebrow: "New Event", title: "Set up your Event", intro: "Name the Event and choose one Camera. The fixed printer is ready to go." };
}
function icon(name) {
  const paths = {
    camera: '<path d="M4 8h3l1.5-2h7L17 8h3v10H4V8Z"/><circle cx="12" cy="13" r="3.5"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.83 2.83-.06-.06a1.7 1.7 0 0 0-1.88-.34A1.7 1.7 0 0 0 14 20.93V21h-4v-.08a1.7 1.7 0 0 0-1.06-1.52 1.7 1.7 0 0 0-1.88.34L7 19.8l-2.83-2.83.06-.06A1.7 1.7 0 0 0 4.57 15 1.7 1.7 0 0 0 3 14v-4a1.7 1.7 0 0 0 1.6-1.06 1.7 1.7 0 0 0-.34-1.88L4.2 7 7 4.2l.06.06A1.7 1.7 0 0 0 9 4.57 1.7 1.7 0 0 0 10 3h4a1.7 1.7 0 0 0 1.06 1.6 1.7 1.7 0 0 0 1.88-.34L17 4.2 19.8 7l-.06.06A1.7 1.7 0 0 0 19.43 9 1.7 1.7 0 0 0 21 10v4a1.7 1.7 0 0 0-1.6 1Z"/>',
    reset: '<path d="M4 12a8 8 0 1 0 2.3-5.7L4 8.6"/><path d="M4 4v4.6h4.6"/>',
    check: '<path d="m5 12 4 4L19 6"/>',
  };
  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]}</svg>`;
}

function shell() {
  return `<div class="shell"><header class="topbar"><div class="brand"><span class="brand-mark">F</span><span>FotoHAVN</span></div><span class="prototype-tag">Camera Tuning prototype</span></header><section class="landing"><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><div class="ghost-grid"><span></span><span></span><span></span></div></section></div>`;
}

function eventField(compact = false) {
  return `<label class="field ${compact ? "field-compact" : ""} ${nameIsDirty() ? "is-dirty" : ""}"><span>Event name</span><input data-field="name" value="${esc(state.name)}" placeholder="e.g. Mika & Paolo's Wedding"><small>Names do not need to be unique.</small></label>`;
}

function hardwareFields(compact = false) {
  return `<div class="hardware ${compact ? "hardware-compact" : ""}">
    <label class="field ${selectedCameraIsDirty() ? "is-dirty" : ""}"><span>Camera</span><select data-field="camera">${Object.keys(cameras).map(name => `<option ${name === state.camera ? "selected" : ""}>${name}</option>`).join("")}</select><small>Exactly one Camera is used when this Event is saved or started.</small></label>
    <label class="field"><span>Printer</span><input value="DNP DS-RX1HS" readonly><small>Fixed for the first field test.</small></label>
  </div>`;
}

function printerField() {
  return `<label class="field"><span>Printer</span><input value="DNP DS-RX1HS" readonly><small>Fixed for the first field test.</small></label>`;
}

function cameraSectionA() {
  return `<section class="camera-section-a ${state.advanced ? "is-tuning" : ""} ${cameraSectionIsDirty() ? "is-dirty" : ""}">
    <div class="camera-section-heading"><div><h2>Camera</h2><p>Choose the Camera used when this Event is saved or started.</p></div><div class="tuning-gear-wrap">
      <button class="tuning-gear ${cameraIsCustomized() ? "is-customized" : ""} ${anyCameraTuningIsDirty() ? "has-unsaved-tuning" : ""}" data-action="advanced" aria-label="Camera tuning" aria-expanded="${state.advanced}" title="Camera tuning">${icon("settings")}</button>
      <span class="gear-tooltip" role="tooltip">Camera tuning</span>
    </div></div>
    <label class="field camera-select-a ${selectedCameraIsDirty() ? "is-dirty" : ""}"><span>Selected Camera</span><select data-field="camera">${Object.keys(cameras).map(name => `<option ${name === state.camera ? "selected" : ""}>${name}</option>`).join("")}</select><small>${cameraData().status}${cameraIsCustomized() ? " · Customized tuning" : ""}</small></label>
    ${state.advanced ? `<div class="a-workspace">${preview("standard")}${tuningPanel("panel")}</div>` : ""}
  </section>`;
}

function advancedButton(label = "Show Camera Tuning", treatment = "inline") {
  const status = state.advanced ? "Hide" : cameraIsCustomized() ? "Customized" : "Advanced";
  return `<button class="advanced-button advanced-${treatment}" data-action="advanced" aria-expanded="${state.advanced}">
    <span class="advanced-icon">${icon("camera")}</span>
    <span><strong>${label}</strong><small>Live preview and controls supported by ${esc(state.camera)}</small></span>
    <span class="advanced-state">${status} <i>${state.advanced ? "−" : "+"}</i></span>
  </button>`;
}

function preview(size = "standard") {
  return `<section class="preview preview-${size}" aria-label="Simulated live Camera preview">
    <div class="preview-image" style="filter:${previewFilter()}">
      <div class="studio-light"></div><div class="subject"><span class="head"></span><span class="body"></span></div><div class="plant"><i></i><i></i><i></i></div>
      <div class="focus-frame"></div>
    </div>
    <div class="preview-bar"><span class="live"><i></i> LIVE</span><span>${esc(state.camera)}</span><span class="camera-ready">${icon("check")} ${cameraData().status}</span></div>
  </section>`;
}

function slider(key) {
  const value = currentTuning()[key];
  const automatic = key === "exposure" ? currentTuning().autoExposure : key === "focus" ? currentTuning().autoFocus : false;
  return `<div class="tuning-row ${automatic ? "is-auto" : ""} ${tuningControlIsDirty(key) ? "is-dirty" : ""}">
    <div class="tuning-label"><label for="tuning-${key}">${labels[key]}</label><output>${automatic ? "Auto" : value}</output></div>
    <input id="tuning-${key}" data-tuning="${key}" type="range" min="0" max="100" value="${value}" ${automatic ? "disabled" : ""}>
    ${["exposure", "focus"].includes(key) ? `<label class="auto-toggle"><input data-auto="${key}" type="checkbox" ${automatic ? "checked" : ""}><span>Auto</span></label>` : ""}
  </div>`;
}

function tuningPanel(style = "panel") {
  return `<section class="tuning tuning-${style}">
    <div class="section-title"><div><p class="eyebrow">Camera Tuning</p><h2>Tune ${esc(state.camera)}</h2><p>Only controls supported by this Camera are shown. Your tuning is remembered when you switch Cameras.</p></div><button class="reset" data-action="reset">${icon("reset")} Reset</button></div>
    <div class="tuning-controls">${cameraData().controls.map(slider).join("")}</div>
  </section>`;
}

function footer() {
  return `<footer class="setup-footer"><button class="btn btn-text" data-action="cancel">Cancel</button><div class="footer-actions"><button class="btn btn-secondary" data-action="save">Save & Close</button><button class="btn btn-primary" data-action="save-start">Save & Start Event</button></div></footer>`;
}

function confirmationModal() {
  if (!state.confirmation) return "";
  if (state.confirmation.type === "discard") {
    return `<div class="confirmation-backdrop"><section class="confirmation-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirmation-title" aria-describedby="confirmation-copy">
      <div class="confirmation-mark">!</div><h1 id="confirmation-title">Discard changes?</h1>
      <p id="confirmation-copy">This restores the last-saved Event name, selected Camera, and Camera Tuning.</p>
      <div class="confirmation-actions"><button class="btn btn-secondary" data-action="dismiss-confirmation">Keep Editing</button><button class="btn btn-danger" data-action="discard-changes">Discard Changes</button></div>
    </section></div>`;
  }
  const startsEvent = state.confirmation.action === "save-start";
  return `<div class="confirmation-backdrop"><section class="confirmation-dialog" role="alertdialog" aria-modal="true" aria-labelledby="confirmation-title" aria-describedby="confirmation-copy">
    <div class="confirmation-mark save-mark">${icon("check")}</div><h1 id="confirmation-title">${startsEvent ? `Save changes and start “${esc(state.name.trim())}”?` : `Save changes to “${esc(state.name.trim())}”?`}</h1>
    <p id="confirmation-copy">The updated Event name, selected Camera, and Camera Tuning apply only to future Guest Cycles. Existing Guest Cycles and artifacts remain unchanged.${startsEvent ? " FotoHAVN will run preflight after saving." : ""}</p>
    <div class="confirmation-actions"><button class="btn btn-secondary" data-action="dismiss-confirmation">Keep Editing</button><button class="btn btn-primary" data-action="confirm-save">${startsEvent ? "Save & Start Event" : "Save Changes"}</button></div>
  </section></div>`;
}

function header() {
  const copy = modeCopy();
  return `<header class="setup-header"><div><p class="eyebrow">${copy.eyebrow}</p><h1 id="setup-title">${copy.title}</h1><p>${copy.intro}</p></div><button class="close" data-action="cancel" aria-label="Close Event setup">×</button></header>`;
}

function basicFields(className = "basic-fields") {
  return `<div class="${className}">${eventField(true)}${hardwareFields(true)}</div>`;
}

function variantA() {
  return `<div class="modal-backdrop"><section class="setup-modal variant-a ${state.advanced ? "advanced-open" : ""}" role="dialog" aria-modal="true" aria-labelledby="setup-title">${header()}<div class="a-primary-details">${eventField(true)}${printerField()}</div>${cameraSectionA()}${footer()}</section></div>`;
}

function variantB() {
  const content = state.advanced
    ? `<div class="focus-toolbar"><button class="back-button" data-action="advanced">← Event details</button><div><p class="eyebrow">Advanced</p><h2>Preview and tune ${esc(state.camera)}</h2></div></div><div class="b-camera">${preview("wide")}${tuningPanel("wide")}</div>`
    : `${header()}<div class="b-details"><div><span class="step-number">1</span><h2>Event details</h2></div><div class="b-fields">${eventField()}${hardwareFields()}</div></div><div class="b-advanced"><span class="step-number muted-step">2</span>${advancedButton("Open Camera Tuning", "step")}</div>`;
  return `<div class="modal-backdrop"><section class="setup-modal variant-b ${state.advanced ? "advanced-open" : ""}" role="dialog" aria-modal="true" aria-labelledby="setup-title">${content}${footer()}</section></div>`;
}

function variantC() {
  return `<div class="modal-backdrop"><section class="setup-modal variant-c" role="dialog" aria-modal="true" aria-labelledby="setup-title">${header()}${basicFields("c-fields")}<div class="c-advanced">${advancedButton("Open Camera Tuning…", "dialog")}</div>${footer()}</section>${state.advanced ? `<div class="nested-backdrop"><section class="tuning-dialog" role="dialog" aria-modal="true" aria-labelledby="tuning-dialog-title"><header><div><p class="eyebrow">Advanced Camera Tuning</p><h1 id="tuning-dialog-title">Preview ${esc(state.camera)}</h1><p>Adjust this Camera against its live view. These settings belong to this Event.</p></div><button class="close" data-action="advanced" aria-label="Close Camera Tuning">×</button></header><div class="dialog-workspace">${preview("large")}${tuningPanel("inspector")}</div><footer><button class="btn btn-primary" data-action="advanced">Done</button></footer></section></div>` : ""}</div>`;
}

function renderSwitcher() {
  const keys = Object.keys(variants);
  const index = keys.indexOf(state.variant) + 1;
  switcher.innerHTML = `<div class="switcher"><button data-cycle="-1" aria-label="Previous variant">←</button><div class="switch-copy"><strong>${state.variant} — ${variants[state.variant].name} (${index}/3)</strong><small>${variants[state.variant].note}</small><span>mode=${state.mode} · advanced=${state.advanced ? "open" : "closed"} · camera=${state.camera} · dirty=${dirty()} · stored tuning=${Object.keys(state.tuning).length} Cameras</span></div><button data-action="mode">${state.mode === "create" ? "Edit" : "New"}</button><button data-cycle="1" aria-label="Next variant">→</button></div>`;
}

function render() {
  app.innerHTML = shell() + (state.variant === "A" ? variantA() : state.variant === "B" ? variantB() : variantC());
  if (state.confirmation) app.insertAdjacentHTML("beforeend", confirmationModal());
  renderSwitcher();
}

function refreshDirtyState() {
  document.querySelector("[data-field='name']")?.closest(".field")?.classList.toggle("is-dirty", nameIsDirty());
  document.querySelector("[data-field='camera']")?.closest(".field")?.classList.toggle("is-dirty", selectedCameraIsDirty());
  document.querySelector(".camera-section-a")?.classList.toggle("is-dirty", cameraSectionIsDirty());
  document.querySelector(".tuning-gear")?.classList.toggle("has-unsaved-tuning", anyCameraTuningIsDirty());
  document.querySelectorAll("[data-tuning]").forEach(control => control.closest(".tuning-row")?.classList.toggle("is-dirty", tuningControlIsDirty(control.dataset.tuning)));
  renderSwitcher();
}

function updateUrl() {
  const url = new URL(location.href);
  url.searchParams.set("variant", state.variant);
  url.searchParams.set("mode", state.mode);
  if (state.advanced) url.searchParams.set("advanced", "open");
  else url.searchParams.delete("advanced");
  history.replaceState({}, "", url);
}

function cycle(direction) {
  const keys = Object.keys(variants);
  state.variant = keys[(keys.indexOf(state.variant) + direction + keys.length) % keys.length];
  state.advanced = false;
  updateUrl();
  render();
}

function switchMode() {
  state.mode = state.mode === "create" ? "edit" : "create";
  state.name = state.mode === "edit" ? "Mika & Paolo's Wedding" : "";
  state.camera = state.mode === "edit" ? "Logitech BRIO" : "Integrated Camera";
  state.advanced = false;
  state.confirmation = null;
  state.tuning = Object.fromEntries(Object.entries(cameras).map(([name, camera]) => [name, structuredClone(camera.defaults)]));
  state.saved = snapshot();
  updateUrl();
  render();
}

function restoreSavedConfiguration() {
  const saved = JSON.parse(state.saved);
  state.name = saved.name;
  state.camera = saved.camera;
  state.tuning = saved.tuning;
  state.advanced = false;
  state.confirmation = null;
  updateUrl();
}

function commitSave(action) {
  state.name = state.name.trim();
  state.saved = snapshot();
  state.confirmation = null;
  render();
  toast(action === "save" ? "Event saved. Returning to Saved Events…" : "Event saved. Starting preflight…");
}

function toast(message) {
  document.querySelector(".toast")?.remove();
  document.body.insertAdjacentHTML("beforeend", `<div class="toast" role="status">${esc(message)}</div>`);
  setTimeout(() => document.querySelector(".toast")?.remove(), 2400);
}

document.addEventListener("input", event => {
  if (event.target.matches("[data-field='name']")) {
    state.name = event.target.value;
    return refreshDirtyState();
  }
  if (!event.target.matches("[data-tuning]")) return;
  const key = event.target.dataset.tuning;
  currentTuning()[key] = Number(event.target.value);
  event.target.closest(".tuning-row")?.querySelector("output")?.replaceChildren(String(currentTuning()[key]));
  document.querySelector(".preview-image").style.filter = previewFilter();
  refreshDirtyState();
});

document.addEventListener("change", event => {
  if (event.target.matches("[data-field='camera']")) state.camera = event.target.value;
  if (event.target.matches("[data-auto]")) {
    const key = event.target.dataset.auto === "exposure" ? "autoExposure" : "autoFocus";
    currentTuning()[key] = event.target.checked;
  }
  render();
});

document.addEventListener("click", event => {
  const cycleButton = event.target.closest("[data-cycle]");
  if (cycleButton) return cycle(Number(cycleButton.dataset.cycle));
  const action = event.target.closest("[data-action]");
  if (!action) return;
  if (action.dataset.action === "mode") return switchMode();
  if (action.dataset.action === "dismiss-confirmation") {
    state.confirmation = null;
    return render();
  }
  if (action.dataset.action === "discard-changes") {
    restoreSavedConfiguration();
    render();
    return toast("Changes discarded. Returning to Saved Events…");
  }
  if (action.dataset.action === "confirm-save") return commitSave(state.confirmation.action);
  if (action.dataset.action === "advanced") {
    state.advanced = !state.advanced;
    updateUrl();
    return render();
  }
  if (action.dataset.action === "reset") {
    state.tuning[state.camera] = structuredClone(cameras[state.camera].defaults);
    render(); return toast(`${state.camera} tuning reset.`);
  }
  if (action.dataset.action === "cancel") {
    if (state.mode === "edit" && dirty()) {
      state.confirmation = { type: "discard" };
      return render();
    }
    return toast(state.mode === "edit" ? "No changes. Returning to Saved Events…" : dirty() ? "New Event draft would require discard confirmation." : "Setup closes immediately.");
  }
  if (["save", "save-start"].includes(action.dataset.action)) {
    if (!state.name.trim()) { document.querySelector("[data-field='name']")?.focus(); return toast("Enter an Event name first."); }
    if (state.mode === "edit" && dirty()) {
      state.confirmation = { type: "save", action: action.dataset.action };
      return render();
    }
    return commitSave(action.dataset.action);
  }
});

document.addEventListener("keydown", event => {
  if (event.key === "Escape" && state.confirmation) {
    state.confirmation = null;
    return render();
  }
  if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
  if (event.target.matches("input, textarea, select, [contenteditable]")) return;
  cycle(event.key === "ArrowRight" ? 1 : -1);
});

render();
