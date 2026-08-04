// THROWAWAY PROTOTYPE — visual exploration for FotoHAVN issue #18.
const variants = {
  A: "Luma deck",
  B: "Operator runway",
  C: "Studio ledger",
};

const initialEvents = [
  { name: "Mika & Paolo's Wedding", saved: "Today, 4:42 PM" },
  { name: "Acme Year-End Party", saved: "Yesterday, 9:18 PM" },
  { name: "Saturday Market", saved: "Jul 28, 2026, 11:06 AM" },
  { name: "Saturday Market", saved: "Jul 19, 2026, 8:31 PM" },
];

const initialParams = new URLSearchParams(location.search);
const state = {
  variant: validVariant(initialParams.get("variant")),
  view: initialParams.get("view") === "setup" ? "setup" : "landing",
  events: structuredClone(initialEvents),
  draft: { name: "", camera: "Integrated Camera" },
  confirmEvent: null,
};

const app = document.querySelector("#app");
const switcher = document.querySelector("#switcher");

function validVariant(value) { return Object.hasOwn(variants, value) ? value : "A"; }
function esc(value) { return value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll('"', "&quot;"); }
function icon(name) {
  const paths = {
    plus: '<path d="M12 5v14M5 12h14"/>',
    play: '<path d="m9 7 8 5-8 5V7Z"/>',
    folder: '<path d="M4 7h6l2 2h8v9H4V7Z"/>',
    settings: '<path d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.83 2.83-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.03 1.56V21h-4v-.08A1.7 1.7 0 0 0 8.94 19.4a1.7 1.7 0 0 0-1.88.34l-.06.06-2.83-2.83.06-.06A1.7 1.7 0 0 0 4.57 15 1.7 1.7 0 0 0 3 14H3v-4h.08A1.7 1.7 0 0 0 4.6 8.94a1.7 1.7 0 0 0-.34-1.88L4.2 7l2.83-2.83.06.06A1.7 1.7 0 0 0 9 4.57 1.7 1.7 0 0 0 10 3h4v.08A1.7 1.7 0 0 0 15.06 4.6a1.7 1.7 0 0 0 1.88-.34L17 4.2 19.83 7l-.06.06A1.7 1.7 0 0 0 19.43 9 1.7 1.7 0 0 0 21 10h.08v4H21a1.7 1.7 0 0 0-1.6 1Z"/>',
  };
  return `<svg class="icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]}</svg>`;
}

function chrome(className) {
  return `<div class="shell ${className}">
    <header class="topbar">
      <div class="brand"><span class="brand-mark">F</span><span>FotoHAVN</span></div>
      <span class="prototype-tag">Operator console · prototype</span>
    </header>
    <div class="page" id="page"></div>
  </div>`;
}

function landingA() {
  return `${chrome("variant-a")}`;
}

function contentA() {
  return `<section class="a-heading">
      <div><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><p class="lede">Open one to adjust its setup, or start it when the booth is ready.</p></div>
    </section>
    <section class="event-grid-a" aria-label="Saved Events">
      <button class="event-card-a new-tile-a" data-action="new">
        <span class="new-circle">+</span><h2>New Event</h2><span>Set up a new booth run</span>
      </button>
      ${state.events.map(event => eventCardA(event)).join("")}
    </section>`;
}

function eventCardA(event) {
  const name = esc(event.name);
  return `<article class="event-card-a event-launch-card">
    <button class="card-launch-target" data-action="confirm-start" data-name="${name}" aria-label="Start ${name}">
      <span class="card-copy"><h2>${name}</h2><span class="saved-at">Saved ${event.saved}</span></span>
      <span class="hover-play" aria-hidden="true">${icon("play")}</span>
    </button>
    <button class="settings-button" data-action="open" data-name="${name}" aria-label="Open ${name} settings" title="Event settings">${icon("settings")}</button>
  </article>`;
}

function contentB() {
  return `<section class="b-heading">
      <div><p class="eyebrow">Operator home</p><h1>Saved Events</h1><p class="lede">Most recently saved first. Start is always one click away.</p></div>
      <button class="btn btn-lime btn-large" data-action="new">${icon("plus")} New Event</button>
    </section>
    <section class="b-list" aria-label="Saved Events">
      ${state.events.map((event, index) => `<article class="event-row-b"><div><span class="number">${String(index + 1).padStart(2, "0")}</span><h2>${esc(event.name)}</h2></div><p class="saved-at">Saved ${event.saved}</p>${actions(event, "row-actions")}</article>`).join("")}
    </section>`;
}

function contentC() {
  return `<section class="c-heading">
      <div><p class="eyebrow">Your booth runs</p><h1>Events, ready when you are.</h1></div>
      <button class="btn btn-primary btn-large" data-action="new">${icon("plus")} Create New Event</button>
    </section>
    <section class="event-grid-c" aria-label="Saved Events">
      ${state.events.map(event => `<article class="event-card-c"><div><h2>${esc(event.name)}</h2></div><div class="c-bottom"><p class="saved-at">Saved ${event.saved}</p>${actions(event, "c-actions")}</div></article>`).join("")}
    </section>`;
}

function actions(event, className) {
  const name = esc(event.name);
  return `<div class="${className}"><button class="btn btn-secondary" data-action="open" data-name="${name}">${icon("folder")} Open</button><button class="btn btn-primary" data-action="start" data-name="${name}">${icon("play")} Start Event</button></div>`;
}

function setupForm() {
  return `<div class="form-grid">
      <div class="field"><label for="event-name">Event name</label><input id="event-name" data-field="name" value="${esc(state.draft.name)}" placeholder="e.g. Mika & Paolo's Wedding" autofocus><span class="field-note">Names do not need to be unique.</span></div>
      <div class="field"><label for="camera">Camera</label><select id="camera" data-field="camera"><option>Integrated Camera</option><option>Logitech BRIO</option><option>USB Camera</option></select><span class="field-note">Choose a Windows camera that FotoHAVN can preview and capture from.</span></div>
      <div class="field"><label for="printer">Printer</label><input id="printer" value="DNP DS-RX1HS" readonly><span class="field-note">Fixed for the first field test.</span></div>
    </div>
    <p class="draft-note">This Event is a draft. Nothing is added to Saved Events until you save.</p>`;
}

function setupActions(treatment = "default") {
  const isModalFooter = treatment === "modal-footer";
  return `<div class="setup-actions${isModalFooter ? " setup-actions-modal" : ""}"><button class="btn ${isModalFooter ? "btn-tertiary" : "btn-quiet"}" data-action="cancel">Cancel</button><div class="commit-actions"><button class="btn btn-secondary" data-action="save">Save & Close</button><button class="btn btn-primary" data-action="save-start">${isModalFooter ? "" : icon("play")} Save & Start Event</button></div></div>`;
}

function setupA() {
  return `<div class="modal-backdrop"><section class="setup-modal" role="dialog" aria-modal="true" aria-labelledby="setup-heading"><div class="setup-title"><div><p class="eyebrow">New Event</p><h1 id="setup-heading">Set up your Event</h1><p class="lede">Name it and confirm the booth hardware.</p></div><button class="close-x" data-action="cancel" aria-label="Close">×</button></div>${setupForm()}${setupActions("modal-footer")}</section></div>`;
}

function startConfirmationA() {
  const name = esc(state.confirmEvent);
  return `<div class="modal-backdrop"><section class="confirm-modal" role="alertdialog" aria-modal="true" aria-labelledby="confirm-heading" aria-describedby="confirm-copy">
    <h1 id="confirm-heading">Start “${name}”?</h1>
    <p class="lede" id="confirm-copy">FotoHAVN will check the camera, printer, and storage before opening the guest Start screen.</p>
    <div class="confirm-actions"><button class="btn btn-quiet" data-action="cancel-confirm">Cancel</button><button class="btn btn-primary btn-large" data-action="start-confirmed">Start Event</button></div>
  </section></div>`;
}

function setupB() {
  return `<section class="setup-panel"><span class="step">New Event · Setup</span><h1>Ready the booth</h1><p class="lede">This short setup creates the saved Event only when you commit it.</p>${setupForm()}${setupActions()}</section>`;
}

function setupC() {
  return `<div class="sheet-backdrop"><section class="setup-sheet" role="dialog" aria-modal="true" aria-labelledby="setup-heading"><button class="close-x" data-action="cancel" aria-label="Close">×</button><p class="eyebrow" style="margin-top:32px">Create New Event</p><h1 id="setup-heading">Give the day a home.</h1><p class="lede">FotoHAVN will keep every Guest Cycle from this booth run together.</p>${setupForm()}${setupActions()}</section></div>`;
}

function render() {
  app.innerHTML = state.variant === "A" ? landingA() : chrome(`variant-${state.variant.toLowerCase()}`);
  const page = document.querySelector("#page");
  page.innerHTML = state.variant === "A" ? contentA() : state.variant === "B" ? contentB() : contentC();
  if (state.view === "setup") {
    if (state.variant === "A") app.insertAdjacentHTML("beforeend", setupA());
    if (state.variant === "B") page.innerHTML = setupB();
    if (state.variant === "C") app.insertAdjacentHTML("beforeend", setupC());
    requestAnimationFrame(() => document.querySelector("#event-name")?.focus());
  }
  if (state.variant === "A" && state.confirmEvent) app.insertAdjacentHTML("beforeend", startConfirmationA());
  renderSwitcher();
}

function renderSwitcher() {
  const index = Object.keys(variants).indexOf(state.variant) + 1;
  switcher.innerHTML = `<div class="switcher"><button data-cycle="-1" aria-label="Previous variant">←</button><div class="switch-label"><strong>${state.variant} — ${variants[state.variant]} (${index}/3)</strong><small>view=${state.view} · events=${state.events.length} · active=none · ← → to compare</small></div><button data-cycle="1" aria-label="Next variant">→</button></div>`;
}

function cycle(direction) {
  const keys = Object.keys(variants);
  const next = (keys.indexOf(state.variant) + direction + keys.length) % keys.length;
  state.variant = keys[next];
  state.confirmEvent = null;
  const url = new URL(location.href);
  url.searchParams.set("variant", state.variant);
  history.replaceState({}, "", url);
  render();
}

function setView(view) {
  state.view = view;
  const url = new URL(location.href);
  if (view === "setup") url.searchParams.set("view", "setup");
  else url.searchParams.delete("view");
  history.replaceState({}, "", url);
  render();
}

function toast(message) {
  document.querySelector(".toast")?.remove();
  document.body.insertAdjacentHTML("beforeend", `<div class="toast" role="status">${esc(message)}</div>`);
  setTimeout(() => document.querySelector(".toast")?.remove(), 2200);
}

document.addEventListener("click", event => {
  const cycleButton = event.target.closest("[data-cycle]");
  if (cycleButton) return cycle(Number(cycleButton.dataset.cycle));
  const action = event.target.closest("[data-action]");
  if (!action) return;
  if (action.dataset.action === "new") {
    state.draft = { name: "", camera: "Integrated Camera" };
    return setView("setup");
  }
  if (action.dataset.action === "cancel") return setView("landing");
  if (action.dataset.action === "confirm-start") {
    state.confirmEvent = action.dataset.name;
    return render();
  }
  if (action.dataset.action === "cancel-confirm") {
    state.confirmEvent = null;
    return render();
  }
  if (action.dataset.action === "start-confirmed") {
    const eventName = state.confirmEvent;
    state.confirmEvent = null;
    render();
    return toast(`Preflighting ${eventName}…`);
  }
  if (action.dataset.action === "open") return toast(`Opening ${action.dataset.name} setup…`);
  if (action.dataset.action === "start") return toast(`Preflighting ${action.dataset.name}…`);
  if (["save", "save-start"].includes(action.dataset.action)) {
    if (!state.draft.name.trim()) { document.querySelector("#event-name")?.focus(); return toast("Enter an Event name first."); }
    state.events.unshift({ name: state.draft.name.trim(), saved: "Just now" });
    const shouldStart = action.dataset.action === "save-start";
    setView("landing");
    return toast(shouldStart ? `Saved. Preflighting ${state.draft.name.trim()}…` : "Event saved.");
  }
});

document.addEventListener("input", event => {
  if (event.target.matches("[data-field='name']")) state.draft.name = event.target.value;
  if (event.target.matches("[data-field='camera']")) state.draft.camera = event.target.value;
});

document.addEventListener("keydown", event => {
  if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
  if (event.target.matches("input, textarea, select, [contenteditable]")) return;
  cycle(event.key === "ArrowRight" ? 1 : -1);
});

render();
