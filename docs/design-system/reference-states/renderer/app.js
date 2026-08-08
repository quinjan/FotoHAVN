(async () => {
  const canonical = await fetch('/docs/design-system/reference-states/canonical-presentation.json').then(response => {
    if (!response.ok) throw new Error(`Could not load canonical presentation data: ${response.status}`);
    return response.json();
  });
  const q = new URLSearchParams(location.search);
  const surface = q.get('surface') || 'saved-events';
  const state = q.get('state') || 'maximum-cards';
  const viewport = q.get('viewport') || `${innerWidth}x${innerHeight}`;
  const longName = canonical.longEventName;
  const eventName = state.includes('long') ? longName : canonical.eventName;
  const fullId = canonical.eventId;
  const media = '/design-qa/issue-28-countdown-implementation.jpg';
  const strip = '/design-qa/issue-20-photo-strip-reference.png';
  const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const button = (label, cls='', disabled=false, loading=false, action='') => `<button class="${cls}${loading?' loading':''}" ${disabled?'disabled':''} ${loading?'aria-busy="true"':''} ${action?`data-action="${action}"`:''}>${loading?`<span class="button-loader" aria-hidden="true"></span><span>${esc(label)}</span>`:esc(label)}</button>`;
  const iconButton = (label, glyph, action, cls='', disabled=false) => `<button class="icon-action ${cls}" aria-label="${esc(label)}" title="${esc(label)}" data-action="${action}" ${disabled?'disabled':''}><span class="mdl2" aria-hidden="true">&#x${glyph};</span></button>`;
  const callout = (kind, text) => `<div class="callout ${kind}"><strong>${kind === 'danger' ? 'Needs attention' : kind === 'warning' ? 'Check setup' : kind === 'success' ? 'Complete' : 'In progress'}</strong><span>${esc(text)}</span></div>`;
  const brand = () => `<div class="brand" aria-label="FotoHAVN"><img src="/docs/design-system/reference-states/renderer/assets/fotohavn-mark.png" alt=""><span>FotoHAVN</span></div>`;
  const header = (context='', action='') => `<header class="header">${brand()}<div class="header-context">${esc(context)}</div><div class="header-actions">${action}</div></header>`;
  const operatorHeader = () => `<header class="header operator-header">${brand()}<div class="operator-console">Operator console</div></header>`;
  const guestStartButton = () => `<button class="primary guest guest-start-action" data-action="start-guest-cycle"><span class="mdl2" aria-hidden="true">&#xE768;</span><span>Touch to start</span></button>`;
  const exitEventButton = ({holding=false,cancelled=false}={}) => `<button class="exit-event hold ${holding?'holding canonical-hold ':''}${cancelled?'focus':''}" aria-label="Hold to exit Event" data-hold-exit><span class="mdl2 exit-key" aria-hidden="true">&#xE8D7;</span><span class="exit-label">${holding?'Keep holding…':'Exit Event'}</span><span class="mdl2 exit-refresh" aria-hidden="true">&#xE72C;</span></button>`;
  const decisionIdentity = () => `<div class="decision-identity"><div class="event-id-label">EVENT</div><strong>${esc(eventName)}</strong><div class="event-id-label decision-id-label">EVENT ID</div><div class="full-id">${fullId}</div></div>`;
  const camera = (overlays='') => `<div class="camera"><img class="camera-source" src="${media}" alt=""><div class="media-label">LIVE · MIRRORED</div>${overlays}</div>`;

  function savedEvents() {
    const count = state === 'maximum-cards' ? 6 : 3;
    const cards = Array.from({length:count}, (_,i) => {
      const target = i === 0;
      const cls = target && state === 'card-hover' ? 'hover' : target && state === 'card-focus' ? 'focus' : target && state === 'unavailable' ? 'unavailable' : '';
      const busy = target && state === 'busy';
      const unavailable = target && state === 'unavailable';
      const deletion = target && state === 'deletion-incomplete';
      const fixture = canonical.savedEvents[i];
      const name = state.includes('long') && i === 0 ? longName : fixture.eventName;
      const eventId = fixture.compactEventId;
      const cardStatus = busy ? '<div class="card-progress"><span class="button-loader" aria-hidden="true"></span><span>Opening Event…</span></div>' : unavailable ? '<div class="card-message warning">Camera unavailable</div>' : deletion ? '<div class="card-message danger">Deletion did not finish</div>' : '';
      const startBlocked = busy || unavailable || deletion;
      return `<article class="event-card ${cls}">
        <button class="card-start-hit" aria-label="Start ${esc(name)}" data-action="start-event" ${startBlocked?'disabled':''}></button>
        <div class="card-body"><div class="card-name">${esc(name)}</div><div class="event-id-label">EVENT ID</div><div class="event-id">${eventId}</div><div class="card-saved">${esc(fixture.savedMetadata)}</div></div>
        <div class="card-bottom">${cardStatus}<div class="card-actions">${iconButton('Edit Event','E70F','edit-event','',busy)}${iconButton('Delete Event','E74D','delete-event','delete',busy)}</div></div>
      </article>`;
    }).join('');
    const top = deletionBanner(state);
    return `<section class="screen">${operatorHeader()}<div class="content"><div class="page-head"><h1>Saved Events</h1><button class="primary" data-action="new-event">New Event</button></div>${top}<div class="cards" style="margin-top:${top?'18px':'0'}">${cards}</div></div></section>`;
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
    const eventIdField = edit ? `<div class="field"><label>Event ID</label><div class="control event-id-control">${fullId}</div></div>` : '';
    const identityNameCamera = `${eventIdField}<div class="field"><label for="event-name">Event name</label><input id="event-name" class="control" value="${missingName?'':esc(eventName)}" placeholder="" readonly>${nameStatus}${dirtyMark}</div><div class="field"><label for="camera-select">Camera</label><select id="camera-select" class="control"><option>${cameraSelected?'Logitech BRIO':'Select Camera'}</option></select>${camStatus}</div>`;
    const printerStorage = `<div class="field"><label for="printer-select">Printer <span class="optional">(optional)</span></label><select id="printer-select" class="control"><option>Not printing</option></select></div><div class="field"><label>Storage</label><div class="storage-path">C:\\Program Files\\FotoHAVN\\Events</div><div class="storage-free">${storageBad?'480 MB':'120 GB'} free</div>${storageStatus}</div>`;
    return `<section class="screen setup"><div class="setup-shell"><div class="setup-title"><div class="eyebrow">EVENT SETUP</div><h1>${edit?'Edit Event':'New Event'}</h1><p class="setup-intro">${edit?'Review this Event and update its Camera or Printer.':'Name the Event and choose its Camera and Printer.'}</p></div><div class="setup-main"><div class="setup-form setup-form-top">${identityNameCamera}</div><div class="preview-panel"><div class="preview">${previewBody}<div class="capture-area-label">3:2 Capture area</div></div></div><div class="setup-form setup-form-bottom">${printerStorage}</div></div><footer class="setup-footer">${button('Cancel','cancel')}${button(success?'Saved':'Save & Close','',!valid||state==='saving'||success)}${button(state==='saving'?'Saving & starting…':'Save & Start Event','primary',!valid||state==='saving'||success,state==='saving')}</footer></div></section>`;
  }

  function guestStart() {
    const holding = state === 'exit-holding' || state === 'long-event-name-and-exit-hold';
    const cancelled = state === 'exit-hold-cancelled';
    const action = exitEventButton({holding,cancelled:cancelled || state==='exit-hold-idle'});
    const confirmation = state === 'exit-confirmation-open' ? dialog('exit-idle') : '';
    return `<section class="screen">${header('',action)}<div class="guest-stage content"><div class="eyebrow guest-event-name">${esc(eventName)}</div><h1>Let’s take some photos.</h1><p class="lead">Four Captures. A quick countdown before each one.</p>${guestStartButton()}<p class="guest-note">Photos stay with this Event.</p></div>${confirmation}</section>`;
  }

  function guestUnavailable() {
    const cameraProblem = state.startsWith('camera');
    const retry = state.includes('retry') || state === 'retrying' || state === 'retry-failed';
    const failed = state === 'retry-failed';
    const action = state === 'retrying' ? button('Checking Camera…','primary',true,true) : retry ? button('Retry','primary') : button('Exit Event','destructive');
    const cause = failed ? 'The Camera is still unavailable.' : cameraProblem ? 'The Camera is not ready.' : 'Photos cannot be saved right now.';
    const guidance = failed ? 'Check the Camera connection before trying again.' : retry ? 'The operator can check the setup and Retry.' : 'The operator must exit the Event and update setup.';
    return `<section class="screen">${header('',exitEventButton())}<div class="assistance"><div class="assist-panel unavailable-assistance"><div class="eyebrow">Operator Assistance</div><h1>Please call the operator</h1><p>${esc(cause)}</p><p class="assist-guidance">${esc(guidance)}</p><div class="assist-action">${action}</div></div></div></section>`;
  }

  function capture() {
    const n = Number((state.match(/capture-(\d)/)||[])[1] || (state.includes('countdown')?4:2));
    const countdownNumber = state === 'capture-4-countdown' ? '3' : (state.match(/countdown-(\d)/)||[])[1];
    let overlay = countdownNumber ? `<div class="countdown"><div><small>GET READY</small>${countdownNumber}</div></div>` : state === 'flash' ? '<div class="flash"></div>' : state === 'photo-saved' ? '<div class="saved-badge">Photo saved</div>' : '';
    if (state.includes('failure')) return operatorAssistance(state==='camera-failure'?'camera-3-preserved':'storage-3-preserved');
    const steps = [1,2,3,4].map(x=>`<span class="step ${x<n?'done':x===n?'active':''}">${x}</span>`).join('');
    return `<section class="screen">${header('')}<div class="capture-stage"><div class="capture-meta"><div class="eyebrow">Photo ${n} of 4</div><div class="steps">${steps}</div></div>${camera(overlay)}</div></section>`;
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
    return `<section class="screen">${header('',exitEventButton())}<div class="assistance"><div class="assist-panel"><div class="eyebrow">Operator Assistance</div><h1>${recovered?'Ready to continue':'Please call the operator'}</h1><p>${recovered?'The Camera is ready. Your saved photos are still here.':cause}</p><div class="preserved">${steps}</div><p><strong>${preserved} of 4 photos saved.</strong> ${failed?'Retry did not work. Check the Camera connection before trying again.':'Saved photos will not be lost.'}</p><div class="assist-action">${recovered?button('Continue','primary guest'):exitOnly?button('Exit Event','destructive'):button(retrying?'Retrying…':'Retry','primary guest',retrying,retrying)}</div></div></div></section>`;
  }

  function stripFrame(cls='') { return `<div class="strip-frame ${cls}"><img src="${strip}" alt="Photo strip preview">${cls==='preparing'?'<div class="strip-loader">Preparing…</div>':''}</div>`; }
  function photoStrip() {
    if (state === 'failed') return operatorAssistance('photo-strip-4-preserved');
    const preparing = state.startsWith('preparing');
    const returning = state === 'returning' || state === 'preparing-or-returning';
    const five = state === 'visible-5-seconds';
    const seconds = five?5:returning?1:10;
    return `<section class="screen">${header('')}<div class="photo-stage"><div class="photo-copy"><div class="eyebrow">${preparing?'All four photos saved':'Photo Strip ready'}</div><h1>${preparing?'Preparing your Photo Strip…':returning?'Ready for the next guest':'Here’s your Photo Strip'}</h1><p class="muted">${preparing?'Your saved photos are being arranged.':returning?'Returning to start now.':`Returning to start in ${seconds} seconds.`}</p><div class="progress-line" style="--progress:${preparing?'38%':returning?'100%':five?'50%':'12%'}"></div></div>${stripFrame(preparing?'preparing':returning?'returning':'')}</div></section>`;
  }

  function acknowledgementDialog({ intent='success', title, message, action='Continue' }) {
    const glyph = intent === 'information' ? 'E946' : 'E930';
    return `<div class="scrim"><section class="dialog acknowledgement ${intent}" role="dialog" aria-modal="true" aria-labelledby="acknowledgement-title" aria-describedby="acknowledgement-message"><div class="dialog-body"><span class="mdl2 acknowledgement-icon" aria-hidden="true">&#x${glyph};</span><h1 id="acknowledgement-title">${esc(title)}</h1><p id="acknowledgement-message" class="muted">${esc(message)}</p></div><div class="dialog-actions"><button class="primary" data-action="acknowledge" autofocus>${esc(action)}</button></div></section></div>`;
  }

  function decisionIcon(glyph, destructive=false) {
    return `<span class="decision-icon ${destructive?'danger':''}" aria-hidden="true"><span class="mdl2 decision-icon-ring">&#xF138;</span><span class="mdl2 decision-icon-glyph">&#x${glyph};</span></span>`;
  }

  function dialog(requested=state) {
    const s = requested === 'destructive-busy-with-long-event-id' ? 'delete-busy' : requested;
    if (s === 'success-destination') return acknowledgementDialog({ title:'Event saved', message:'Your changes have been saved.' });
    const type = s.split('-')[0];
    const busy = s.includes('busy');
    const failed = s.includes('failed') || s === 'retry';
    const titles = {start:'Start this Event?',save:'Save changes?',discard:'Discard changes?',exit:'Exit this Event?',delete:'Delete this Event?'};
    const verbs = {start:'Start Event',save:'Save changes',discard:'Discard changes',exit:'Exit Event',delete:'Delete Event'};
    const busyVerbs = {start:'Starting Event…',exit:'Exiting Event…',delete:'Deleting Event…'};
    const glyphs = {start:'E768',save:'E74E',discard:'E74D',exit:'F3B1',delete:'E74D'};
    const destructive = ['discard','exit','delete'].includes(type);
    const showEventIdentity = !['discard','exit'].includes(type);
    const status = failed ? callout('danger',`${type==='delete'?'Deletion':'The action'} did not finish. Review the Event and try again.`) : '';
    const title = titles[type] || 'Confirm this action';
    const consequence = type==='delete' ? 'This permanently removes the Event and its saved photos.' : type==='exit' ? 'The current Guest Cycle will end and FotoHAVN will return to Saved Events.' : type==='discard' ? 'Unsaved changes will be lost.' : '';
    const cancelAction = surface === 'guest-start' ? 'cancel-exit' : 'cancel-dialog';
    const confirmAction = failed && ['start','delete'].includes(type) ? `retry-${type}` : failed ? '' : `confirm-${type}`;
    return `<div class="scrim"><section class="dialog decision ${destructive?'danger':''}" role="dialog" aria-modal="true" aria-labelledby="decision-title" ${consequence?'aria-describedby="decision-consequence"':''}><div class="dialog-body">${decisionIcon(glyphs[type]||'E8AD',destructive)}<h1 id="decision-title">${esc(title)}</h1>${consequence?`<p id="decision-consequence" class="muted">${esc(consequence)}</p>`:''}${showEventIdentity?decisionIdentity():''}${status}</div><div class="dialog-actions">${button(type==='exit'?'Keep event active':'Cancel',busy?'':'focus',busy,false,cancelAction)}${button(failed?'Retry':(busy?busyVerbs[type]||'Working…':verbs[type]||'Continue'),destructive?'destructive':'primary',busy,busy,confirmAction)}</div></section></div>`;
  }
  function confirmation() { return `<section class="screen">${operatorHeader()}<div class="content"><div class="page-head"><h1>Saved Events</h1>${button('New Event','primary')}</div><div class="cards"><article class="event-card"><div class="card-name">${esc(eventName)}</div><div class="event-id-label">EVENT ID</div><div class="event-id">${esc(canonical.savedEvents[0].compactEventId)}</div></article></div></div>${dialog()}</section>`; }

  const renderers = {'saved-events':savedEvents,'event-setup':setup,'guest-start':guestStart,'guest-start-unavailable':guestUnavailable,capture, 'operator-assistance':operatorAssistance,'photo-strip':photoStrip,confirmation};
  document.getElementById('app').innerHTML = (renderers[surface] || savedEvents)() + `<div class="sr-only">Target annotation: ${esc(surface)}.${esc(state)}, ${esc(viewport)}, design-v1.0.0 at 1a20bc6. Full accessibility annotation is registered in the same-name YAML sidecar.</div>`;
  document.querySelectorAll('[data-action]').forEach(control => control.addEventListener('click', () => {
    const destinations = {
      'start-event': ['confirmation','start-idle'],
      'edit-event': ['event-setup','edit-clean'],
      'delete-event': ['confirmation','delete-idle'],
      'new-event': ['event-setup','new-empty'],
      'acknowledge': ['saved-events','card-idle'],
      'cancel-dialog': ['saved-events','card-idle'],
      'cancel-exit': ['guest-start','ready'],
      'confirm-start': ['guest-start','ready'],
      'confirm-save': ['confirmation','success-destination'],
      'confirm-discard': ['saved-events','card-idle'],
      'confirm-exit': ['saved-events','card-idle'],
      'confirm-delete': ['saved-events','card-idle'],
      'retry-start': ['confirmation','start-busy'],
      'retry-delete': ['confirmation','delete-busy'],
      'start-guest-cycle': ['capture','capture-1']
    };
    const destination = destinations[control.dataset.action];
    if (!destination) return;
    const next = new URLSearchParams({surface:destination[0],state:destination[1],viewport});
    location.search = next.toString();
  }));
  document.querySelectorAll('[data-hold-exit]').forEach(control => {
    let holdTimer;
    const label = control.querySelector('.exit-label');
    const startHold = event => {
      if (event.type === 'keydown' && !['Enter',' '].includes(event.key)) return;
      if (event.type === 'keydown') event.preventDefault();
      if (control.classList.contains('active-hold')) return;
      clearTimeout(holdTimer);
      control.classList.remove('canonical-hold');
      control.classList.add('holding','active-hold');
      label.textContent = 'Keep holding…';
      control.setAttribute('aria-busy','true');
      holdTimer = setTimeout(() => {
        const next = new URLSearchParams({surface:'guest-start',state:'exit-confirmation-open',viewport});
        location.search = next.toString();
      },1500);
    };
    const cancelHold = event => {
      if (event.type === 'keyup' && !['Enter',' '].includes(event.key)) return;
      clearTimeout(holdTimer);
      control.classList.remove('holding','active-hold');
      label.textContent = 'Exit Event';
      control.removeAttribute('aria-busy');
    };
    control.addEventListener('pointerdown', startHold);
    control.addEventListener('pointerup', cancelHold);
    control.addEventListener('pointercancel', cancelHold);
    control.addEventListener('pointerleave', cancelHold);
    control.addEventListener('keydown', startHold);
    control.addEventListener('keyup', cancelHold);
    control.addEventListener('blur', cancelHold);
  });
  document.documentElement.dataset.ready = 'true';
})();
