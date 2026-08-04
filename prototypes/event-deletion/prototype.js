// THROWAWAY PROTOTYPE — deletion entry patterns (A-D) and incomplete-deletion recovery patterns (E-G) for FotoHAVN issue #18.
const variants = {
  A: { name: "Direct delete", note: "Fastest: always-visible trash icon on each card" },
  B: { name: "Overflow menu", note: "Balanced: Delete Event is one intentional tap deeper" },
  C: { name: "Setup danger zone", note: "Safest: deletion lives only inside Event setup" },
  D: { name: "Hover delete", note: "Like A: trash icon appears only on card hover or keyboard focus" },
  E: { name: "Recovery dialog", note: "Failure interrupts once; the quarantined Event remains behind it" },
  F: { name: "Recovery card", note: "The affected card becomes the complete recovery surface" },
  G: { name: "Recovery notice", note: "A page-level notice explains the problem; the card shows quarantine" },
};

const initialEvents = [
  { id: 1, name: "Mika & Paolo's Wedding", saved: "Today, 4:42 PM", camera: "Canon EOS R100" },
  { id: 2, name: "Acme Year-End Party", saved: "Yesterday, 9:18 PM", camera: "Canon EOS R100" },
  { id: 3, name: "Saturday Market", saved: "Jul 28, 2026, 11:06 AM", camera: "Integrated Camera" },
  { id: 4, name: "Saturday Market", saved: "Jul 19, 2026, 8:31 PM", camera: "Canon EOS R100" },
];

const params = new URLSearchParams(location.search);
const state = {
  variant: Object.hasOwn(variants, params.get("variant")) ? params.get("variant") : "A",
  events: structuredClone(initialEvents),
  menuId: null,
  setupId: null,
  deleteId: null,
  deletingEvent: null,
  deletedName: null,
  failureDialogOpen: params.get("variant") === "E",
  retryCount: 0,
};

const app = document.querySelector("#app");
const switcher = document.querySelector("#switcher");
const esc = value => value.replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll('"', "&quot;");
const currentEvent = id => state.events.find(event => event.id === id);

function icon(name) {
  const paths = {
    play: '<path d="m9 7 8 5-8 5V7Z"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l-2.8 2.8a1.7 1.7 0 0 0-1.88-.34A1.7 1.7 0 0 0 14 20.92V21h-4v-.08A1.7 1.7 0 0 0 8.94 19.4a1.7 1.7 0 0 0-1.88.34l-2.8-2.8A1.7 1.7 0 0 0 4.6 15 1.7 1.7 0 0 0 3 14v-4a1.7 1.7 0 0 0 1.6-1 1.7 1.7 0 0 0-.34-1.94l2.8-2.8A1.7 1.7 0 0 0 9 4.6 1.7 1.7 0 0 0 10 3h4a1.7 1.7 0 0 0 1 1.6 1.7 1.7 0 0 0 1.94-.34l2.8 2.8A1.7 1.7 0 0 0 19.4 9a1.7 1.7 0 0 0 1.6 1v4a1.7 1.7 0 0 0-1.6 1Z"/>',
    trash: '<path d="M4 7h16M9 7V4h6v3M7 7l1 13h8l1-13M10 11v5M14 11v5"/>',
    more: '<circle cx="5" cy="12" r="1" fill="currentColor" stroke="none"/><circle cx="12" cy="12" r="1" fill="currentColor" stroke="none"/><circle cx="19" cy="12" r="1" fill="currentColor" stroke="none"/>',
    warning: '<path d="M12 3 2.8 20h18.4L12 3Z"/><path d="M12 9v5M12 17h.01"/>',
    check: '<path d="m5 12 4 4L19 6"/>',
  };
  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${paths[name]}</svg>`;
}

function card(event) {
  const name = esc(event.name);
  if (event.deletionIncomplete) return incompleteCard(event);
  let topAction = "";
  if (["A", "D"].includes(state.variant)) topAction = `<button class="icon-button delete-direct" data-action="ask-delete" data-id="${event.id}" aria-label="Delete ${name}" title="Delete Event">${icon("trash")}</button>`;
  if (state.variant === "B") topAction = `<button class="icon-button more" data-action="toggle-menu" data-id="${event.id}" aria-label="More actions for ${name}" aria-expanded="${state.menuId === event.id}">${icon("more")}</button>${state.menuId === event.id ? `<div class="menu"><button data-action="ask-delete" data-id="${event.id}">Delete Event…</button></div>` : ""}`;
  return `<article class="card">
    <button class="launch" data-action="start" data-id="${event.id}" aria-label="Start ${name}"><span class="card-copy"><h2>${name}</h2><span class="saved-at">Saved ${event.saved}</span></span><span class="hover-play">${icon("play")}</span></button>
    ${topAction}
    <button class="icon-button settings" data-action="setup" data-id="${event.id}" aria-label="Open ${name} settings" title="Event settings">${icon("settings")}</button>
  </article>`;
}

function incompleteCard(event) {
  const name = esc(event.name);
  if (state.variant === "F") return `<article class="card recovery-card" aria-labelledby="recovery-${event.id}">
    <div class="recovery-card-mark">${icon("warning")}</div>
    <div class="recovery-card-copy"><span class="status-label">Deletion incomplete</span><h2 id="recovery-${event.id}">${name}</h2><p>Some files could not be removed. This Event may no longer be usable.</p></div>
    <button class="btn btn-danger" data-action="retry-delete" data-id="${event.id}">Retry Deletion</button>
  </article>`;

  return `<article class="card incomplete-card" aria-labelledby="incomplete-${event.id}">
    <div class="incomplete-copy"><span class="status-label">${icon("warning")} Deletion incomplete</span><h2 id="incomplete-${event.id}">${name}</h2><p>This Event is unavailable because some of its files may already be gone.</p></div>
    ${state.variant === "E" ? `<button class="btn btn-danger retry-card-action" data-action="show-failure" data-id="${event.id}">Retry Deletion</button>` : ""}
  </article>`;
}

function setupModal(event) {
  return `<div class="modal-backdrop"><section class="setup-modal" role="dialog" aria-modal="true" aria-labelledby="setup-title">
    <div class="setup-title"><div><p class="eyebrow">Event setup</p><h1 id="setup-title">${esc(event.name)}</h1><p class="lede">Review the saved booth configuration.</p></div><button class="close" data-action="close-setup" aria-label="Close">×</button></div>
    <div class="setup-summary"><div class="setup-row"><span>Camera</span><strong>${esc(event.camera)}</strong></div><div class="setup-row"><span>Printer</span><strong>DNP DS-RX1HS</strong></div></div>
    ${state.variant === "C" ? `<section class="danger-zone"><h2>Delete this Event</h2><p>Permanently remove this Event, every Guest Cycle, and all photos. This action requires confirmation.</p><button class="btn" data-action="ask-delete" data-id="${event.id}">${icon("trash")} Delete Event…</button></section>` : ""}
  </section></div>`;
}

function deleteDialog(event) {
  return `<div class="modal-backdrop"><section class="dialog" role="alertdialog" aria-modal="true" aria-labelledby="delete-title" aria-describedby="delete-copy">
    <div class="dialog-mark">${icon("warning")}</div>
    <h1 id="delete-title">Delete “${esc(event.name)}”?</h1>
    <p class="lede" id="delete-copy">This permanently deletes the Event, every Guest Cycle, and all photos. It cannot be recovered.</p>
    <div class="actions"><button class="btn btn-cancel" data-action="cancel-delete">Cancel</button><button class="btn btn-danger" data-action="confirm-delete">Delete Event</button></div>
  </section></div>`;
}

function deletingDialog(event) {
  return `<div class="modal-backdrop"><section class="dialog busy-dialog" role="alertdialog" aria-modal="true" aria-labelledby="deleting-title" aria-describedby="deleting-copy" aria-busy="true">
    <div class="spinner" aria-hidden="true"></div>
    <h1 id="deleting-title">Deleting “${esc(event.name)}”…</h1>
    <p class="lede" id="deleting-copy">FotoHAVN is permanently deleting the Event directory, Guest Cycles, and photos. Keep the app open.</p>
  </section></div>`;
}

function deletedDialog(name) {
  return `<div class="modal-backdrop"><section class="dialog" role="alertdialog" aria-modal="true" aria-labelledby="deleted-title" aria-describedby="deleted-copy">
    <div class="dialog-mark success">${icon("check")}</div>
    <h1 id="deleted-title">Event deleted</h1>
    <p class="lede" id="deleted-copy">“${esc(name)}” and all of its Guest Cycles and photos were permanently deleted.</p>
    <div class="actions"><button class="btn btn-primary" data-action="close-success">Done</button></div>
  </section></div>`;
}

function failedDeletionDialog(event) {
  return `<div class="modal-backdrop"><section class="dialog failure-dialog" role="alertdialog" aria-modal="true" aria-labelledby="failure-title" aria-describedby="failure-copy">
    <div class="dialog-mark">${icon("warning")}</div>
    <p class="status-label">Deletion incomplete</p>
    <h1 id="failure-title">Couldn’t finish deleting “${esc(event.name)}”</h1>
    <p class="lede" id="failure-copy">Not every file was removed, so this Event may no longer be usable. You can safely retry deletion; FotoHAVN will only remove what remains.</p>
    ${state.retryCount ? `<p class="attempt-note">Retry ${state.retryCount} also failed. The Event remains unavailable.</p>` : ""}
    <div class="actions"><button class="btn btn-cancel" data-action="close-failure">Close</button><button class="btn btn-danger" data-action="retry-delete" data-id="${event.id}">Retry Deletion</button></div>
  </section></div>`;
}

function recoveryNotice(event) {
  return `<section class="recovery-notice" role="status" aria-labelledby="recovery-notice-title">
    <div class="recovery-notice-mark">${icon("warning")}</div>
    <div><p class="status-label">Action needed</p><h2 id="recovery-notice-title">Deletion incomplete for “${esc(event.name)}”</h2><p>Some files may already be gone. The Event is unavailable until deletion finishes.</p></div>
    <button class="btn btn-danger" data-action="retry-delete" data-id="${event.id}">Retry Deletion</button>
  </section>`;
}

function render() {
  const failedEvent = state.events.find(event => event.deletionIncomplete);
  app.innerHTML = `<div class="shell variant-${state.variant.toLowerCase()}"><header class="topbar"><div class="brand"><span class="brand-mark">F</span><span>FotoHAVN</span></div><span class="prototype-tag">Deletion prototype</span></header><div class="page"><section class="heading"><p class="eyebrow">Saved Events</p><h1>Choose an Event</h1><p class="lede">Open one to adjust its setup, or start it when the booth is ready.</p></section>${state.variant === "G" && failedEvent ? recoveryNotice(failedEvent) : ""}<section class="event-grid" aria-label="Saved Events"><button class="new-card"><span class="new-circle">+</span><h2>New Event</h2><span>Set up a new booth run</span></button>${state.events.map(card).join("")}</section></div></div>`;
  if (state.setupId && currentEvent(state.setupId)) app.insertAdjacentHTML("beforeend", setupModal(currentEvent(state.setupId)));
  if (state.deleteId && currentEvent(state.deleteId)) app.insertAdjacentHTML("beforeend", deleteDialog(currentEvent(state.deleteId)));
  if (state.deletingEvent) app.insertAdjacentHTML("beforeend", deletingDialog(state.deletingEvent));
  if (state.deletedName) app.insertAdjacentHTML("beforeend", deletedDialog(state.deletedName));
  if (state.variant === "E" && state.failureDialogOpen && failedEvent && !state.deletingEvent) app.insertAdjacentHTML("beforeend", failedDeletionDialog(failedEvent));
  const index = Object.keys(variants).indexOf(state.variant) + 1;
  switcher.innerHTML = `<div class="switcher"><button data-cycle="-1" aria-label="Previous variant">←</button><div class="switch-label"><strong>${state.variant} — ${variants[state.variant].name} (${index}/${Object.keys(variants).length})</strong><small>${variants[state.variant].note} · events=${state.events.length}</small></div><button data-cycle="1" aria-label="Next variant">→</button></div>`;
}

function cycle(direction) {
  if (state.deletingEvent) return;
  const keys = Object.keys(variants);
  state.variant = keys[(keys.indexOf(state.variant) + direction + keys.length) % keys.length];
  state.menuId = state.setupId = state.deleteId = null;
  state.deletedName = null;
  state.events = structuredClone(initialEvents);
  if (["E", "F", "G"].includes(state.variant)) state.events[0].deletionIncomplete = true;
  state.failureDialogOpen = state.variant === "E";
  state.retryCount = 0;
  const url = new URL(location.href);
  url.searchParams.set("variant", state.variant);
  history.replaceState({}, "", url);
  render();
}

function toast(message) {
  document.querySelector(".toast")?.remove();
  document.body.insertAdjacentHTML("beforeend", `<div class="toast" role="status">${esc(message)}</div>`);
  setTimeout(() => document.querySelector(".toast")?.remove(), 2400);
}

document.addEventListener("click", event => {
  const cycleButton = event.target.closest("[data-cycle]");
  if (cycleButton) return cycle(Number(cycleButton.dataset.cycle));
  const action = event.target.closest("[data-action]");
  if (!action) return;
  if (state.deletingEvent) return;
  const id = Number(action.dataset.id);
  if (action.dataset.action === "toggle-menu") { state.menuId = state.menuId === id ? null : id; return render(); }
  if (action.dataset.action === "setup") { state.setupId = id; state.menuId = null; return render(); }
  if (action.dataset.action === "close-setup") { state.setupId = null; return render(); }
  if (action.dataset.action === "ask-delete") { state.deleteId = id; state.menuId = null; return render(); }
  if (action.dataset.action === "cancel-delete") { state.deleteId = null; return render(); }
  if (action.dataset.action === "close-success") { state.deletedName = null; return render(); }
  if (action.dataset.action === "show-failure") { state.failureDialogOpen = true; return render(); }
  if (action.dataset.action === "close-failure") { state.failureDialogOpen = false; return render(); }
  if (action.dataset.action === "retry-delete") {
    const doomed = currentEvent(id);
    state.deletingEvent = structuredClone(doomed);
    state.failureDialogOpen = false;
    render();
    return setTimeout(() => {
      state.deletingEvent = null;
      state.retryCount += 1;
      state.failureDialogOpen = state.variant === "E";
      render();
      if (state.variant !== "E") toast("Retry failed. The Event remains unavailable; nothing was restored.");
    }, 1800);
  }
  if (action.dataset.action === "confirm-delete") {
    const doomed = currentEvent(state.deleteId);
    state.deletingEvent = structuredClone(doomed);
    state.deleteId = null;
    render();
    return setTimeout(() => {
      state.events = state.events.filter(item => item.id !== doomed.id);
      state.setupId = null;
      state.deletingEvent = null;
      state.deletedName = doomed.name;
      render();
    }, 1800);
  }
  if (action.dataset.action === "start") return toast(`Would open Start “${currentEvent(id).name}”? confirmation.`);
});

document.addEventListener("keydown", event => {
  if (state.deletingEvent) return;
  if (event.key === "Escape" && state.deleteId) { state.deleteId = null; return render(); }
  if (event.key === "Escape" && state.setupId) { state.setupId = null; return render(); }
  if (!["ArrowLeft", "ArrowRight"].includes(event.key)) return;
  if (event.target.matches("input, textarea, select, [contenteditable]")) return;
  cycle(event.key === "ArrowRight" ? 1 : -1);
});

if (["E", "F", "G"].includes(state.variant)) state.events[0].deletionIncomplete = true;
render();
