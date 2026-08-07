(() => {
  const q = new URLSearchParams(location.search);
  const surface = q.get('surface') || 'saved-events';
  const state = q.get('state') || 'maximum-cards';
  const viewport = q.get('viewport') || `${innerWidth}x${innerHeight}`;
  const longName = 'Mika & Paolo’s extraordinarily long wedding celebration';
  const eventName = state.includes('long') ? longName : 'Mika & Paolo’s wedding';
  const fullId = '0198a7d2-5bc1-7f45-8e90-3f7a2f91c4e8';
  const media = '/design-qa/issue-28-countdown-implementation.jpg';
  const strip = '/design-qa/issue-20-photo-strip-reference.png';
  const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const button = (label, cls='', disabled=false) => `<button class="${cls}" ${disabled?'disabled':''}>${esc(label)}</button>`;
  const callout = (kind, text) => `<div class="callout ${kind}"><strong>${kind === 'danger' ? 'Needs attention' : kind === 'warning' ? 'Check setup' : kind === 'success' ? 'Complete' : 'In progress'}</strong><span>${esc(text)}</span></div>`;
  const header = (context='', action='') => `<header class="header"><div class="brand">FotoHAVN</div><div class="header-context">${esc(context)}</div><div class="header-actions">${action}</div></header>`;
  const identity = () => `<div class="identity"><div class="event-id-label">EVENT</div><strong>${esc(eventName)}</strong><div class="event-id-label" style="margin-top:10px">EVENT ID</div><div class="full-id">${fullId}</div></div>`;
  const camera = (overlays='') => `<div class="camera"><img class="camera-source" src="${media}" alt=""><div class="media-label">LIVE · MIRRORED</div>${overlays}</div>`;

  function savedEvents() {
    const count = state === 'maximum-cards' ? 6 : 3;
    const cards = Array.from({length:count}, (_,i) => {
      const target = i === 0;
      const cls = target && state === 'card-hover' ? 'hover' : target && state === 'card-focus' ? 'focus' : target && state === 'unavailable' ? 'unavailable' : '';
      const busy = target && state === 'busy';
      const unavailable = target && state === 'unavailable';
      const deletion = target && state === 'deletion-incomplete';
      return `<article class="event-card ${cls}">
        <div><div class="card-name">${esc(i ? ['Summer reunion','Graduation portraits','Studio open day','Town fiesta','Family weekend'][i-1] : eventName)}</div><div class="event-id-label">EVENT ID</div><div class="event-id">${i?'03B1 · '+String(7710+i).padStart(4,'0'):'7A2F · 91C4'}</div><div class="muted" style="font-size:12px;margin-top:7px">Saved ${i+1} day${i?'s':''} ago</div></div>
        <div class="card-status">${unavailable?'Camera unavailable — edit setup to continue.':deletion?'Deletion did not finish. The Event is still saved.':busy?'Opening Event…':'Ready to start'}</div>
        <div class="card-actions">${button(busy?'Opening…':unavailable?'Edit setup':'Start','start primary',busy)}${button('Edit','mini',busy)}${button('Delete','mini',busy)}</div>
      </article>`;
    }).join('');
    const top = state === 'new-event' ? callout('info','New Event setup opens without changing existing Events.') : deletionBanner(state);
    return `<section class="screen">${header('Operator')}<div class="content"><div class="page-head"><div><div class="eyebrow">Operator</div><h1>Saved Events</h1></div>${button('New Event','primary')}</div>${top}<div class="cards" style="margin-top:${top?'18px':'0'}">${cards}</div></div></section>`;
  }
  function deletionBanner(s){ return s==='deletion-incomplete'?callout('danger','Deletion did not finish. Review the Event and try again.'):''; }

  function setup() {
    const edit = state.startsWith('edit');
    const missingName = state === 'new-empty';
    const missingCamera = state === 'new-empty' || state === 'actions-disabled';
    const cameraSelected = !missingCamera;
    let camStatus = missingCamera ? `<div class="inline-status warning">Select a Camera to continue.</div>` : '';
    if (state === 'camera-checking') camStatus = `<div class="inline-status info">Checking Camera…</div>`;
    if (state === 'camera-error-and-insufficient-storage') camStatus = `<div class="inline-status danger">Camera check failed. Choose another Camera or Retry.</div>`;
    if (state === 'camera-unavailable') camStatus = `<div class="inline-status danger">This Camera is no longer available. Choose another Camera.</div>`;
    if (state === 'camera-access-denied') camStatus = `<div class="inline-status danger">Windows blocked Camera access. Allow access, then Retry.</div>`;
    if (state === 'camera-in-use') camStatus = `<div class="inline-status danger">Another app is using this Camera. Close it, then Retry.</div>`;
    if (state === 'camera-disconnected') camStatus = `<div class="inline-status danger">The Camera disconnected. Reconnect it or choose another Camera.</div>`;
    const storageBad = state === 'storage-insufficient' || state === 'camera-error-and-insufficient-storage';
    const storageUnavailable = state === 'storage-unavailable';
    const saveError = state === 'save-error';
    const valid = !missingName && !missingCamera && !camStatus.includes('danger') && !storageBad && !storageUnavailable && !saveError;
    const nameStatus = missingName ? '<div class="inline-status warning">Enter an Event name to continue.</div>' : '';
    const storageStatus = storageBad ? '<div class="inline-status danger">Not enough space. Free up at least 1 GB to continue.</div>' : storageUnavailable ? '<div class="inline-status danger">Storage is unavailable. Restore access before saving.</div>' : saveError ? '<div class="inline-status danger">The Event could not be saved here. Check storage access and try again.</div>' : '';
    const dirtyMark = state === 'edit-dirty' ? '<div class="inline-status info">Unsaved changes</div>' : '';
    const previewUnavailable = missingCamera || camStatus.includes('danger') || state === 'camera-checking';
    const previewMessage = missingCamera ? 'Select a Camera to start the preview.' : state === 'camera-checking' ? 'Checking Camera…' : 'Camera preview unavailable.';
    const previewBody = previewUnavailable ? `<div class="preview-empty">${esc(previewMessage)}</div>` : `<img class="camera-source" src="${media}" alt=""><div class="guide"></div><div class="media-label">${state==='camera-ready'?'LIVE · MIRRORED':'PREVIEW'}</div>`;
    const success = state === 'save-success';
    return `<section class="screen setup"><div class="setup-shell"><div class="setup-title"><div class="eyebrow">EVENT SETUP</div><h1>${edit?'Edit Event':'New Event'}</h1><p class="setup-intro">${edit?'Review this Event and update its Camera or Printer.':'Name the Event and choose its Camera and Printer.'}</p></div><div class="setup-main"><div class="setup-form">${edit?identity():''}<div class="field"><label for="event-name">Event name</label><input id="event-name" class="control" value="${missingName?'':esc(eventName)}" placeholder="" readonly>${nameStatus}${dirtyMark}</div><div class="field"><label for="camera-select">Camera</label><select id="camera-select" class="control"><option>${cameraSelected?'Logitech BRIO':'Select Camera'}</option></select>${camStatus}</div><div class="field"><label for="printer-select">Printer <span class="optional">(optional)</span></label><select id="printer-select" class="control"><option>Not printing</option></select></div><div class="field"><label>Storage</label><div class="storage-path">C:\\Program Files\\FotoHAVN\\Events</div><div class="storage-free">${storageBad?'480 MB':'120 GB'} free</div>${storageStatus}</div></div><div class="preview-panel"><div class="preview">${previewBody}<div class="capture-area-label">3:2 Capture area</div></div></div></div><footer class="setup-footer">${button('Cancel','cancel')}${button(success?'Saved':'Save & Close','',!valid||state==='saving'||success)}${button(state==='saving'?'Saving & starting…':'Save & Start Event','primary',!valid||state==='saving'||success)}</footer></div></section>`;
  }

  function guestStart() {
    const holding = state === 'exit-holding';
    const cancelled = state === 'exit-hold-cancelled';
    const action = button(holding?'Keep holding…':'Exit event',`hold ${(cancelled || state==='exit-hold-idle')?'focus':''}`);
    const confirmation = state === 'exit-confirmation-open' ? dialog('exit-idle') : '';
    return `<section class="screen">${header(eventName,action)}<div class="guest-stage content"><div class="eyebrow">${esc(eventName)}</div><h1>Let’s take some photos</h1><p class="lead">Four photos. A quick countdown before each one.</p>${button('Touch to start','primary guest')}<p class="guest-note">Photos stay with this Event.</p></div>${confirmation}</section>`;
  }

  function guestUnavailable() {
    const cameraProblem = state.startsWith('camera');
    const retry = state.includes('retry') || state === 'retrying' || state === 'retry-failed';
    const failed = state === 'retry-failed';
    const action = state === 'retrying' ? button('Checking Camera…','primary',true) : retry ? button('Retry','primary') : button('Exit event','destructive');
    return `<section class="screen">${header(eventName,button('Exit event'))}<div class="guest-stage content"><div class="unavailable-stage"><div class="eyebrow">Photo session unavailable</div><h1 style="margin:8px 0 12px">${cameraProblem?'Camera needs attention':'Storage needs attention'}</h1>${callout(failed?'danger':'warning',failed?'The Camera is still unavailable. Ask the operator to check the setup.':cameraProblem?'The Camera is not ready. The operator can retry or update setup.':'Photos cannot be saved right now. The operator must restore storage access.')}<div class="actions">${action}</div></div></div></section>`;
  }

  function capture() {
    const n = Number((state.match(/capture-(\d)/)||[])[1] || (state.includes('countdown')?4:2));
    const countdownNumber = state === 'capture-4-countdown' ? '3' : (state.match(/countdown-(\d)/)||[])[1];
    let overlay = countdownNumber ? `<div class="countdown"><div><small>GET READY</small>${countdownNumber}</div></div>` : state === 'flash' ? '<div class="flash"></div>' : state === 'photo-saved' ? '<div class="saved-badge">Photo saved</div>' : '';
    if (state.includes('failure')) return operatorAssistance(state==='camera-failure'?'camera-3-preserved':'storage-3-preserved');
    const steps = [1,2,3,4].map(x=>`<span class="step ${x<n?'done':x===n?'active':''}">${x}</span>`).join('');
    return `<section class="screen">${header(eventName)}<div class="capture-stage"><div class="capture-meta"><div class="eyebrow">Photo ${n} of 4</div><div class="steps">${steps}</div></div>${camera(overlay)}</div></section>`;
  }

  function operatorAssistance(forced) {
    const s = forced || state;
    const preserved = Number((s.match(/-(\d)-preserved/)||[])[1] || (s.startsWith('photo-strip')?4:3));
    const cause = s.startsWith('storage') ? 'The last photo could not be saved.' : s.startsWith('photo-strip') ? 'The Photo Strip could not be prepared.' : 'The Camera stopped responding.';
    const failed = s === 'retry-failed';
    const retrying = s === 'retrying';
    const recovered = s === 'recovered';
    const exitOnly = s === 'exit-only';
    const steps = [1,2,3,4].map(x=>`<span class="${x<=preserved?'done':''}">${x}</span>`).join('');
    return `<section class="screen">${header(eventName,button('Exit event'))}<div class="assistance"><div class="assist-panel"><div class="eyebrow">Operator Assistance</div><h1>${recovered?'Ready to continue':'Please call the operator'}</h1><p>${recovered?'The Camera is ready. Your saved photos are still here.':cause}</p><div class="preserved">${steps}</div><p><strong>${preserved} of 4 photos saved.</strong> ${failed?'Retry did not work. Check the Camera connection before trying again.':'Saved photos will not be lost.'}</p><div style="margin-top:22px">${recovered?button('Continue','primary guest'):exitOnly?button('Exit event','destructive'):button(retrying?'Retrying…':'Retry','primary guest',retrying)}</div></div></div></section>`;
  }

  function stripFrame(cls='') { return `<div class="strip-frame ${cls}"><img src="${strip}" alt="Photo strip preview">${cls==='preparing'?'<div class="strip-loader">Preparing…</div>':''}</div>`; }
  function photoStrip() {
    if (state === 'failed') return operatorAssistance('photo-strip-4-preserved');
    const preparing = state.startsWith('preparing');
    const returning = state === 'returning' || state === 'preparing-or-returning';
    const five = state === 'visible-5-seconds';
    const seconds = five?5:returning?1:10;
    return `<section class="screen">${header(eventName)}<div class="photo-stage"><div class="photo-copy"><div class="eyebrow">${preparing?'All four photos saved':'Photo Strip ready'}</div><h1>${preparing?'Preparing your Photo Strip…':returning?'Ready for the next guest':'Here’s your Photo Strip'}</h1><p class="muted">${preparing?'Your saved photos are being arranged.':returning?'Returning to start now.':`Returning to start in ${seconds} seconds.`}</p><div class="progress-line" style="--progress:${preparing?'38%':returning?'100%':five?'50%':'12%'}"></div></div>${stripFrame(preparing?'preparing':returning?'returning':'')}</div></section>`;
  }

  function dialog(requested=state) {
    const s = requested === 'destructive-busy-with-long-event-id' ? 'delete-busy' : requested;
    const type = s.split('-')[0];
    const busy = s.includes('busy');
    const failed = s.includes('failed') || s === 'retry';
    const titles = {start:'Start this Event?',save:'Save changes?',discard:'Discard changes?',exit:'Exit this Event?',delete:'Delete this Event?'};
    const verbs = {start:'Start Event',save:'Save changes',discard:'Discard changes',exit:'Exit Event',delete:'Delete Event'};
    const busyVerbs = {start:'Starting Event…',exit:'Exiting Event…',delete:'Deleting Event…'};
    const destructive = ['discard','exit','delete'].includes(type);
    const status = failed ? callout('danger',`${type==='delete'?'Deletion':'The action'} did not finish. Review the Event and try again.`) : '';
    const title = s === 'success-destination' ? 'Event saved' : (titles[type] || 'Confirm this action');
    return `<div class="scrim"><section class="dialog" role="dialog" aria-modal="true"><div class="dialog-body"><div class="eyebrow">Confirmation</div><h1>${esc(title)}</h1><p class="muted">${type==='delete'?'This permanently removes the Event and its saved photos.':type==='exit'?'The current Guest Cycle will end and FotoHAVN will return to Saved Events.':'Review the Event before continuing.'}</p>${identity()}${status}</div><div class="dialog-actions">${button(type==='exit'?'Keep event active':'Cancel',failed?'':'focus',busy)}${button(failed?'Retry':(busy?busyVerbs[type]||'Working…':verbs[type]||'Continue'),destructive?'destructive':'primary',busy)}</div></section></div>`;
  }
  function confirmation() { return `<section class="screen">${header('Operator')}<div class="content"><div class="page-head"><h1>Saved Events</h1>${button('New Event','primary')}</div><div class="cards"><article class="event-card"><div class="card-name">${esc(eventName)}</div><div class="event-id-label">EVENT ID</div><div class="event-id">7A2F · 91C4</div></article></div></div>${dialog()}</section>`; }

  const renderers = {'saved-events':savedEvents,'event-setup':setup,'guest-start':guestStart,'guest-start-unavailable':guestUnavailable,capture, 'operator-assistance':operatorAssistance,'photo-strip':photoStrip,confirmation};
  document.getElementById('app').innerHTML = (renderers[surface] || savedEvents)() + `<div class="sr-only">Target annotation: ${esc(surface)}.${esc(state)}, ${esc(viewport)}, design-v1.0.0 at 1a20bc6. Full accessibility annotation is registered in the same-name YAML sidecar.</div>`;
  document.documentElement.dataset.ready = 'true';
})();
