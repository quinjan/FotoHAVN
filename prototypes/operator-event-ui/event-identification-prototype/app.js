// PROTOTYPE — throwaway UI. Three duplicate-Event identity contracts on one route.
const variants = {
  A: "Event ID",
  B: "Duplicate-aware",
  C: "Identity band",
};

const events = [
  { id: "summer-am", uuid: "01989c3a-61d2-7000-8000-00007a2f91c4", displayId: "7A2F · 91C4", name: "Summer Social", created: "Created Aug 5, 2026, 9:15 AM", saved: "Saved today, 4:42 PM", camera: "Canon EOS R100 · Front hall" },
  { id: "summer-pm", uuid: "01989c3a-cc82-7000-8000-0000c8064d3b", displayId: "C806 · 4D3B", name: "Summer Social", created: "Created Aug 5, 2026, 8:30 PM", saved: "Saved today, 9:18 PM", camera: "Canon EOS R100 · Garden booth" },
  { id: "market", uuid: "01983aa1-e3f1-7000-8000-0000d91eb4f0", displayId: "D91E · B4F0", name: "Saturday Market", created: "Created Jul 28, 2026, 7:05 AM", saved: "Saved Jul 28, 2026, 11:06 AM", camera: "Logitech BRIO · Market booth" },
];

let params = new URLSearchParams(location.search);
let variant = variants[params.get("variant")] ? params.get("variant") : "A";
let flow = params.get("flow") || "cards";
let selectedId = params.get("event") || "summer-pm";
let viewport = params.get("viewport") || "canonical";
let typedName = "";
let acknowledged = false;
const app = document.querySelector("#app");

const selected = () => events.find(item => item.id === selectedId) || events[1];
const duplicate = item => events.filter(candidate => candidate.name === item.name).length > 1;

function identity(item, context = "default") {
  if (variant === "A") {
    const compact = context === "card";
    return `<div class="identity identity-a ${context}"><strong>${item.name}</strong><span class="event-id ${compact ? "compact" : "full"}"><i>Event ID</i><code>${compact ? item.displayId : item.uuid}</code></span></div>`;
  }
  if (variant === "B") return `<div class="identity identity-b ${context}"><strong>${item.name}</strong>${duplicate(item) ? `<span><i>Same-name Event</i>${item.created}</span>` : ""}</div>`;
  return `<div class="identity identity-c ${context}"><strong>${item.name}</strong><span>${item.created}</span></div>`;
}

function shell(content) {
  return `<div class="frame ${viewport}">
    <header><a><b>F</b><span>FotoHAVN</span></a><span>OPERATOR CONSOLE</span></header>
    ${content}
  </div>`;
}

function cards() {
  return shell(`<main class="page"><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><p class="lede">Open one to adjust its setup, or start a Guest Cycle.</p>
    <section class="event-grid variant-${variant.toLowerCase()}">
      <article class="new-event"><span>＋</span><strong>New Event</strong><small>Set up a new booth run</small></article>
      ${events.map(item => `<article class="event-card ${duplicate(item) ? "duplicate" : ""}">
        <button class="trash" data-open="delete" data-event="${item.id}" aria-label="Delete ${item.name}">Delete</button>
        ${identity(item, "card")}
        ${variant === "C" ? `<p class="saved">${item.saved}</p>` : `<small class="saved">${item.saved}</small>`}
        <div class="card-actions"><button data-open="edit" data-event="${item.id}">Edit</button><button class="primary" data-open="start" data-event="${item.id}">Start</button></div>
      </article>`).join("")}
    </section>
  </main>`);
}

function edit() {
  const item = selected();
  return shell(`<main class="page"><button class="back" data-flow="cards">← Saved Events</button>
    <section class="edit-surface">
      <div class="edit-head"><div><p class="eyebrow">Edit Event</p>${identity(item, "header")}</div><button data-flow="cards" aria-label="Close">×</button></div>
      <div class="edit-body"><label>Event name<input value="${item.name}" /></label><label>Camera<input value="${item.camera}" /></label><div class="preview">LIVE CAMERA PREVIEW</div></div>
      <footer><span>${variant === "A" ? item.saved : "Changes stay with this exact Event."}</span><button data-flow="cards">Cancel</button><button class="primary" data-open="start">Save & Start</button></footer>
    </section>
  </main>`);
}

function modal(kind) {
  const item = selected();
  const isDelete = kind === "delete";
  const canDelete = variant === "A" || (variant === "B" && typedName === item.name) || (variant === "C" && acknowledged);
  return cards() + `<div class="scrim"><section class="dialog ${isDelete ? "danger" : ""}" role="dialog" aria-modal="true">
    <p class="eyebrow">${isDelete ? "Permanent deletion" : "Start Event"}</p>
    <h2>${isDelete ? "Delete this Event?" : "Start this Event?"}</h2>
    ${identity(item, "dialog-identity")}
    <p>${isDelete ? "The Event, all Guest Cycles, and all photos will be permanently deleted and cannot be recovered." : "FotoHAVN will confirm the Camera bound to this Event and storage before starting."}</p>
    ${isDelete && variant === "B" ? `<label class="safeguard">Type <strong>${item.name}</strong> to confirm<input data-confirm-name value="${typedName}" autocomplete="off" /></label>` : ""}
    ${isDelete && variant === "C" ? `<label class="check"><input data-ack type="checkbox" ${acknowledged ? "checked" : ""} /> I checked the name and creation time above.</label>` : ""}
    <footer><button data-flow="cards">Cancel</button><button class="${isDelete ? "destructive" : "primary"}" data-confirm="${kind}" ${isDelete && !canDelete ? "disabled" : ""}>${isDelete ? "Delete Event" : "Start Event"}</button></footer>
  </section></div>`;
}

function progress() {
  const item = selected();
  return cards() + `<div class="scrim"><section class="dialog progress" role="status"><span class="spinner"></span><p class="eyebrow">Deleting Event</p>${identity(item, "dialog-identity")}<p>Removing Guest Cycles and photos…</p><button data-flow="deleted">Show result</button></section></div>`;
}

function result() {
  const item = selected();
  return shell(`<main class="page result"><span class="result-icon">✓</span><p class="eyebrow">Deletion complete</p><h1>Event deleted</h1>${identity(item, "result-identity")}<p>Its Guest Cycles and photos were permanently removed.</p><button class="primary" data-flow="cards">Return to Saved Events</button></main>`);
}

function controls() {
  return `<aside class="prototype-controls"><button data-cycle="-1" aria-label="Previous variant">←</button><strong>${variant} — ${variants[variant]}</strong><button data-cycle="1" aria-label="Next variant">→</button><span></span><label>View<select data-viewport><option value="canonical" ${viewport === "canonical" ? "selected" : ""}>1280 × 720</option><option value="narrow" ${viewport === "narrow" ? "selected" : ""}>Narrow / 150%</option></select></label><button data-reset>Reset flow</button></aside>`;
}

function render() {
  const views = { cards, edit, start: () => modal("start"), delete: () => modal("delete"), deleting: progress, deleted: result };
  app.innerHTML = (views[flow] || cards)() + controls();
  bind();
}

function navigate(next = {}) {
  variant = next.variant || variant;
  flow = next.flow || flow;
  selectedId = next.event || selectedId;
  viewport = next.viewport || viewport;
  params.set("variant", variant); params.set("flow", flow); params.set("event", selectedId); params.set("viewport", viewport);
  history.replaceState({}, "", `${location.pathname}?${params}`);
  render();
}

function cycle(direction) {
  const keys = Object.keys(variants);
  typedName = ""; acknowledged = false;
  navigate({ variant: keys[(keys.indexOf(variant) + direction + keys.length) % keys.length] });
}

function bind() {
  document.querySelectorAll("[data-cycle]").forEach(button => button.addEventListener("click", () => cycle(Number(button.dataset.cycle))));
  document.querySelectorAll("[data-flow]").forEach(button => button.addEventListener("click", () => navigate({ flow: button.dataset.flow })));
  document.querySelectorAll("[data-open]").forEach(button => button.addEventListener("click", () => { typedName = ""; acknowledged = false; navigate({ flow: button.dataset.open, event: button.dataset.event }); }));
  document.querySelector("[data-confirm='start']")?.addEventListener("click", () => navigate({ flow: "edit" }));
  document.querySelector("[data-confirm='delete']")?.addEventListener("click", () => navigate({ flow: "deleting" }));
  document.querySelector("[data-confirm-name]")?.addEventListener("input", event => { typedName = event.target.value; render(); document.querySelector("[data-confirm-name]")?.focus(); });
  document.querySelector("[data-ack]")?.addEventListener("change", event => { acknowledged = event.target.checked; render(); });
  document.querySelector("[data-viewport]")?.addEventListener("change", event => navigate({ viewport: event.target.value }));
  document.querySelector("[data-reset]")?.addEventListener("click", () => { typedName = ""; acknowledged = false; navigate({ flow: "cards", event: "summer-pm" }); });
}

document.addEventListener("keydown", event => {
  if (["INPUT", "TEXTAREA", "SELECT"].includes(event.target.tagName) || event.target.isContentEditable) return;
  if (event.key === "ArrowLeft") cycle(-1);
  if (event.key === "ArrowRight") cycle(1);
  if (event.key === "Escape") navigate({ flow: "cards" });
});

render();
