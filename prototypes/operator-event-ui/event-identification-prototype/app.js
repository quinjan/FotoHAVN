// PROTOTYPE — throwaway UI. Three full Event-ID presentations in consequential flows.
const variants = {
  A: "Identity panel",
  B: "Sentence-led",
  C: "Verification rows",
};

const events = [
  { id: "summer-am", uuid: "01989c3a-61d2-7000-8000-00007a2f91c4", displayId: "7A2F · 91C4", name: "Summer Social", saved: "Saved today, 4:42 PM", camera: "Canon EOS R100 · Front hall" },
  { id: "summer-pm", uuid: "01989c3a-cc82-7000-8000-0000c8064d3b", displayId: "C806 · 4D3B", name: "Summer Social", saved: "Saved today, 9:18 PM", camera: "Canon EOS R100 · Garden booth" },
  { id: "market", uuid: "01983aa1-e3f1-7000-8000-0000d91eb4f0", displayId: "D91E · B4F0", name: "Saturday Market", saved: "Saved Jul 28, 2026, 11:06 AM", camera: "Logitech BRIO · Market booth" },
];

const flows = {
  cards: "Saved Events",
  edit: "Edit",
  start: "Start confirmation",
  starting: "Starting",
  startFailed: "Could not start",
  delete: "Delete confirmation",
  deleting: "Deleting",
  deleteFailed: "Deletion incomplete",
  deleted: "Deletion complete",
};

let params = new URLSearchParams(location.search);
let variant = variants[params.get("variant")] ? params.get("variant") : "A";
let flow = flows[params.get("flow")] ? params.get("flow") : "start";
let selectedId = params.get("event") || "summer-pm";
let viewport = params.get("viewport") || "canonical";
const app = document.querySelector("#app");

const selected = () => events.find(item => item.id === selectedId) || events[1];

function compactIdentity(item) {
  return `<div class="compact-identity"><strong>${item.name}</strong><span><i>Event ID</i><code>${item.displayId}</code></span></div>`;
}

function fullIdentity(item, context = "default") {
  if (variant === "A") {
    return `<section class="full-identity identity-panel ${context}" aria-label="Full Event identity"><strong>${item.name}</strong><span><i>Event ID</i><code>${item.uuid}</code></span></section>`;
  }
  if (variant === "B") {
    return `<section class="full-identity sentence-led ${context}" aria-label="Full Event identity"><p><strong>${item.name}</strong> is the Event ending in <code>${item.displayId}</code>.</p><span><i>Full Event ID</i><code>${item.uuid}</code></span></section>`;
  }
  return `<dl class="full-identity verification-rows ${context}" aria-label="Full Event identity"><div><dt>Event</dt><dd>${item.name}</dd></div><div><dt>Full Event ID</dt><dd><code>${item.uuid}</code></dd></div></dl>`;
}

function shell(content) {
  return `<div class="frame ${viewport}"><header><a><b>F</b><span>FotoHAVN</span></a><span>OPERATOR CONSOLE</span></header>${content}</div>`;
}

function cards() {
  return shell(`<main class="page"><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><p class="lede">Open one to adjust its setup, or start a Guest Cycle.</p>
    <section class="event-grid">
      <article class="new-event"><span>＋</span><strong>New Event</strong><small>Set up a new booth run</small></article>
      ${events.map(item => `<article class="event-card">
        <button class="trash" data-open="delete" data-event="${item.id}" aria-label="Delete ${item.name}, Event ID ending in ${item.displayId.replace(" · ", ", ")}">Delete</button>
        ${compactIdentity(item)}<small class="saved">${item.saved}</small>
        <div class="card-actions"><button data-open="edit" data-event="${item.id}">Edit</button><button class="primary" data-open="start" data-event="${item.id}">Start</button></div>
      </article>`).join("")}
    </section>
  </main>`);
}

function edit() {
  const item = selected();
  return shell(`<main class="page"><button class="back" data-flow="cards">← Saved Events</button>
    <section class="edit-surface">
      <div class="edit-head"><div><p class="eyebrow">Edit Event</p><h1>${item.name}</h1></div><button data-flow="cards" aria-label="Close">×</button></div>
      <div class="edit-identity">${fullIdentity(item, "edit-context")}</div>
      <div class="edit-body"><label>Event name<input value="${item.name}" /></label><label>Camera<input value="${item.camera}" /></label><div class="preview">LIVE CAMERA PREVIEW</div></div>
      <footer><span>${item.saved}</span><button data-flow="cards">Cancel</button><button class="primary">Save changes</button></footer>
    </section>
  </main>`);
}

function confirmation(kind) {
  const item = selected();
  const isDelete = kind === "delete";
  return cards() + `<div class="scrim ${viewport}"><section class="dialog ${isDelete ? "danger" : ""}" role="dialog" aria-modal="true" aria-labelledby="dialog-title">
    <p class="eyebrow">${isDelete ? "Permanent deletion" : "Start Event"}</p>
    <h2 id="dialog-title">${isDelete ? "Delete this Event?" : "Start this Event?"}</h2>
    ${fullIdentity(item, "dialog-identity")}
    <p>${isDelete ? "The Event, all Guest Cycles, and all photos will be permanently deleted and cannot be recovered." : "FotoHAVN will confirm the Camera bound to this Event and storage before starting."}</p>
    <footer><button data-flow="cards">Cancel</button><button class="${isDelete ? "destructive" : "primary"}" data-confirm="${kind}">${isDelete ? "Delete Event" : "Start Event"}</button></footer>
  </section></div>`;
}

function working(kind) {
  const item = selected();
  const deleting = kind === "deleting";
  return cards() + `<div class="scrim ${viewport}"><section class="dialog progress" role="status" aria-live="polite"><span class="spinner" aria-hidden="true"></span><p class="eyebrow">${deleting ? "Deleting Event" : "Starting Event"}</p><h2>${deleting ? "Removing Event data…" : "Checking booth readiness…"}</h2>${fullIdentity(item, "dialog-identity")}<p>${deleting ? "Guest Cycles and photos are being permanently removed." : "FotoHAVN is checking the bound Camera and storage."}</p><button data-flow="${deleting ? "deleteFailed" : "startFailed"}">Show failure state</button></section></div>`;
}

function failed(kind) {
  const item = selected();
  const deleting = kind === "deleteFailed";
  return cards() + `<div class="scrim ${viewport}"><section class="dialog failure" role="alertdialog" aria-modal="true"><p class="eyebrow">${deleting ? "Deletion incomplete" : "Could not start"}</p><h2>${deleting ? "Some Event data remains" : "This Event did not start"}</h2>${fullIdentity(item, "dialog-identity")}<div class="status-callout"><strong>${deleting ? "Storage is unavailable." : "The bound Camera is unavailable."}</strong><span>${deleting ? "The Event is still saved. Retry when storage is available." : "Reconnect the Camera, then retry this exact Event."}</span></div><footer><button data-flow="cards">Return to Events</button><button class="primary" data-flow="${deleting ? "deleting" : "starting"}">Retry</button></footer></section></div>`;
}

function deleted() {
  const item = selected();
  return shell(`<main class="page result"><span class="result-icon">✓</span><p class="eyebrow">Deletion complete</p><h1>Event deleted</h1>${fullIdentity(item, "result-identity")}<p>Its Guest Cycles and photos were permanently removed.</p><button class="primary" data-flow="cards">Return to Saved Events</button></main>`);
}

function controls() {
  return `<aside class="prototype-controls"><button data-cycle="-1" aria-label="Previous variant">←</button><strong>${variant} — ${variants[variant]}</strong><button data-cycle="1" aria-label="Next variant">→</button><span></span><label>State<select data-flow-select>${Object.entries(flows).map(([key, label]) => `<option value="${key}" ${flow === key ? "selected" : ""}>${label}</option>`).join("")}</select></label><label>View<select data-viewport><option value="canonical" ${viewport === "canonical" ? "selected" : ""}>1280 × 720</option><option value="tablet" ${viewport === "tablet" ? "selected" : ""}>1024 × 768</option><option value="scale125" ${viewport === "scale125" ? "selected" : ""}>125%</option><option value="scale150" ${viewport === "scale150" ? "selected" : ""}>150%</option><option value="stress200" ${viewport === "stress200" ? "selected" : ""}>200% stress</option></select></label></aside>`;
}

function render() {
  const views = { cards, edit, start: () => confirmation("start"), starting: () => working("starting"), startFailed: () => failed("startFailed"), delete: () => confirmation("delete"), deleting: () => working("deleting"), deleteFailed: () => failed("deleteFailed"), deleted };
  app.innerHTML = views[flow]() + controls();
  bind();
}

function navigate(next = {}) {
  variant = next.variant || variant; flow = next.flow || flow; selectedId = next.event || selectedId; viewport = next.viewport || viewport;
  params.set("variant", variant); params.set("flow", flow); params.set("event", selectedId); params.set("viewport", viewport);
  history.replaceState({}, "", `${location.pathname}?${params}`); render();
}

function cycle(direction) {
  const keys = Object.keys(variants);
  navigate({ variant: keys[(keys.indexOf(variant) + direction + keys.length) % keys.length] });
}

function bind() {
  document.querySelectorAll("[data-cycle]").forEach(button => button.addEventListener("click", () => cycle(Number(button.dataset.cycle))));
  document.querySelectorAll("[data-flow]").forEach(button => button.addEventListener("click", () => navigate({ flow: button.dataset.flow })));
  document.querySelectorAll("[data-open]").forEach(button => button.addEventListener("click", () => navigate({ flow: button.dataset.open, event: button.dataset.event })));
  document.querySelector("[data-confirm='start']")?.addEventListener("click", () => navigate({ flow: "starting" }));
  document.querySelector("[data-confirm='delete']")?.addEventListener("click", () => navigate({ flow: "deleting" }));
  document.querySelector("[data-flow-select]")?.addEventListener("change", event => navigate({ flow: event.target.value }));
  document.querySelector("[data-viewport]")?.addEventListener("change", event => navigate({ viewport: event.target.value }));
}

document.addEventListener("keydown", event => {
  if (["INPUT", "TEXTAREA", "SELECT"].includes(event.target.tagName) || event.target.isContentEditable) return;
  if (event.key === "ArrowLeft") cycle(-1);
  if (event.key === "ArrowRight") cycle(1);
  if (event.key === "Escape") navigate({ flow: "cards" });
});

render();
