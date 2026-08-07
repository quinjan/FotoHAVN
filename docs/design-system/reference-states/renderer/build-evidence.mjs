import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const targets = join(root, 'targets');
const contract = {
  version: '1.0.0',
  tag: 'design-v1.0.0',
  commit: '1a20bc6f46f8d724683f8c4b47c359379fc7371b',
};
const surfaces = [
  { id:'saved-events', name:'Saved Events', audience:'operator', states:['new-event','card-idle','card-hover','card-focus','unavailable','busy','deletion-incomplete','maximum-cards'], risk:'maximum-cards', heading:'Saved Events', order:['heading','New Event','Event cards','Start, Edit, and Delete actions'] },
  { id:'event-setup', name:'Event setup', audience:'operator', states:['new-empty','new-valid','edit-clean','edit-dirty','camera-checking','camera-ready','camera-unavailable','camera-access-denied','camera-in-use','camera-disconnected','storage-insufficient','storage-unavailable','actions-disabled','saving','save-error','save-success'], risk:'camera-error-and-insufficient-storage', heading:'Set up Event', order:['heading','Event identity','Event name','Camera and status','Camera preview','Printer','Storage','footer actions'] },
  { id:'guest-start', name:'Guest Start', audience:'guest', states:['ready','exit-hold-idle','exit-holding','exit-hold-cancelled','exit-confirmation-open'], risk:'long-event-name-and-exit-hold', heading:'Let’s take some photos.', order:['FotoHAVN brand header','Event name','heading','instructions','Touch to start','Exit Event'] },
  { id:'guest-start-unavailable', name:'Guest Start unavailable', audience:'guest', states:['camera-retry','camera-exit-only','storage-retry','storage-exit-only','retrying','retry-failed'], risk:'longest-recovery-copy', heading:'Please call the operator', order:['FotoHAVN brand header','Operator Assistance label','heading','reason','Retry or Exit Event'] },
  { id:'capture', name:'Capture', audience:'guest', states:['capture-1','capture-2','capture-3','capture-4','countdown-3','countdown-2','countdown-1','flash','photo-saved','camera-failure','storage-failure'], risk:'capture-4-countdown', heading:'Photo capture', order:['FotoHAVN brand header','Capture progress','Camera preview','countdown or saved status'] },
  { id:'operator-assistance', name:'Operator Assistance', audience:'guest', states:['camera-0-preserved','camera-3-preserved','storage-3-preserved','photo-strip-4-preserved','retrying','recovered','retry-failed','exit-only'], risk:'retry-failed-with-3-preserved', heading:'Please call the operator', order:['FotoHAVN brand header','Operator Assistance heading','cause','preserved progress','recovery action','Exit Event'] },
  { id:'photo-strip', name:'Photo Strip', audience:'guest', states:['preparing','visible-10-seconds','visible-5-seconds','returning','failed'], risk:'preparing-or-returning', heading:'Here’s your Photo Strip', order:['FotoHAVN brand header','heading','Photo Strip preview','return status and progress'] },
  { id:'confirmation', name:'Confirmation', audience:'operator', states:['start-idle','start-busy','start-failed','save-idle','discard-idle','exit-idle','exit-busy','delete-idle','delete-busy','delete-failed','retry','success-destination'], risk:'destructive-busy-with-long-event-id', heading:'Confirmation', order:['semantic icon','dialog heading','consequence when present','Event identity when relevant','status when present','safe action','confirming action'] },
];
const viewports = [
  { id:'standard', size:'1280x720', mode:'standard' },
  { id:'compact-aspect', size:'1024x768', mode:'compact' },
  { id:'scale-125-equivalent', size:'1024x576', mode:'compact' },
  { id:'scale-150-equivalent', size:'853x480', mode:'compact' },
  { id:'scale-200-stress-equivalent', size:'640x360', mode:'stress' },
];
const quote = value => JSON.stringify(value);
const sha256 = path => createHash('sha256').update(readFileSync(path)).digest('hex');
const frames = [];
for (const surface of surfaces) {
  for (const state of surface.states) frames.push({ surface, state, viewport:viewports[0], coverage:'canonical' });
  for (const viewport of viewports.slice(1)) frames.push({ surface, state:surface.risk, viewport, coverage:'responsive-risk' });
}
for (const frame of frames) {
  const folder = join(targets, frame.surface.id);
  mkdirSync(folder, { recursive:true });
  const basename = `${frame.state}--${frame.viewport.size}`;
  const png = join(folder, `${basename}.png`);
  const annotation = join(folder, `${basename}.yaml`);
  const id = `${frame.surface.id}.${frame.state}.${frame.viewport.id}`;
  const acknowledgement = frame.surface.id === 'confirmation' && frame.state === 'success-destination';
  const confirmationState = frame.state === 'destructive-busy-with-long-event-id' ? 'delete-busy' : frame.state;
  const confirmationType = confirmationState.split('-')[0];
  const confirmationTitles = {start:'Start this Event?',save:'Save changes?',discard:'Discard changes?',exit:'Exit this Event?',delete:'Delete this Event?',retry:'Confirm this action'};
  const headingText = acknowledgement ? 'Event saved' : frame.surface.id === 'confirmation' ? (confirmationTitles[confirmationType] || frame.surface.heading) : frame.surface.id === 'event-setup' ? (frame.state.startsWith('edit-') ? 'Edit Event' : 'New Event') : frame.surface.heading;
  const readingOrder = acknowledgement ? ['success status','dialog heading','message','primary action'] : frame.surface.order;
  const active = /(busy|saving|checking|retrying|countdown|returning|preparing|holding)/.test(frame.state);
  const blocking = /(failure|failed|unavailable|access-denied|in-use|disconnected|insufficient|exit-only|incomplete)/.test(frame.state);
  const lines = [
    `id: ${quote(id)}`,
    `contractVersion: ${quote(contract.version)}`,
    `contractTag: ${quote(contract.tag)}`,
    `contractCommit: ${quote(contract.commit)}`,
    `surface: ${quote(frame.surface.name)}`,
    `state: ${quote(frame.state)}`,
    `coverage: ${quote(frame.coverage)}`,
    `viewport: ${quote(frame.viewport.size)}`,
    `effectiveMode: ${quote(frame.viewport.mode)}`,
    `audience: ${quote(frame.surface.audience)}`,
    `heading:`,
    `  text: ${quote(headingText)}`,
    `  level: 1`,
    `automation:`,
    `  name: ${quote(headingText)}`,
    `  role: ${quote(frame.surface.id === 'confirmation' ? 'dialog' : 'window')}`,
    `  description: ${quote(`${frame.surface.name}, ${frame.state}`)}`,
    `  state: ${quote(active ? 'busy' : blocking ? 'unavailable' : 'ready')}`,
    `readingOrder:`,
    ...readingOrder.map(value => `  - ${quote(value)}`),
    `focus:`,
    `  initial: ${quote(acknowledgement ? 'primary action' : frame.surface.id === 'confirmation' ? 'safe action' : frame.surface.audience === 'guest' ? 'primary guest action when present' : 'page heading')}`,
    `  order: ${quote(readingOrder.join(' -> '))}`,
    `  returnTarget: ${quote(frame.surface.id === 'confirmation' ? 'invoking control' : 'not applicable')}`,
    `  reflowBehavior: ${quote('Focus follows visible task order and remains revealed without horizontal scrolling.')}`,
    `keyboard:`,
    `  - ${quote('Tab and Shift+Tab follow the visible task order.')}`,
    `  - ${quote('Enter and Space activate native buttons on release.')}`,
    `touch:`,
    `  minimumTarget: ${quote(frame.surface.audience === 'guest' ? '64x64 prominent; 48x48 safety action' : '48x48')}`,
    `  activation: ${quote('release')}`,
    `  gesture: ${quote(frame.surface.id === 'guest-start' && frame.state.includes('hold') ? 'continuous 1.5-second hold for Exit event only' : 'none')}`,
    `announcements:`,
    `  - text: ${quote(acknowledgement ? 'Event saved. Your changes have been saved.' : blocking ? `${frame.surface.name} needs attention.` : active ? `${frame.surface.name} in progress.` : `${frame.surface.name} ready.`)}`,
    `    trigger: ${quote('semantic state transition')}`,
    `    priority: ${quote(blocking ? 'assertive' : 'polite')}`,
    `    deduplicate: ${quote('semantic-transition')}`,
    `contrast:`,
    `  - foreground: ${quote('color.text.primary')}`,
    `    background: ${quote('color.surface.panel')}`,
    `    ratio: ${quote('17.77:1')}`,
    `  - foreground: ${quote('color.focus.ring')}`,
    `    background: ${quote('color.surface.canvas')}`,
    `    ratio: ${quote('5.69:1')}`,
    `highContrast: ${quote('Use system colors; preserve boundaries, focus, text, shape, and state meaning.')}`,
    `reducedMotion: ${quote('Resolve nonessential transitions instantly; never delay failure or completion.')}`,
    `stress200: ${quote(frame.viewport.mode === 'stress' ? 'Shown in this target; no clipping, horizontal scrolling, or guest-stage vertical scrolling.' : 'Verify against the registered 640x360 responsive-risk target for this surface.')}`,
    `winuiPattern: ${quote(frame.surface.id === 'confirmation' ? 'ContentDialog; Button; TextBlock' : 'Page; native WinUI controls; UI Automation patterns per component contract')}`,
    `verification: ${quote(['keyboard','narrator','touch','visual','high-contrast','reduced-motion'])}`,
    `evidence: ${quote(`targets/${frame.surface.id}/${basename}.png`)}`,
  ];
  writeFileSync(annotation, `${lines.join('\n')}\n`);
  frames[frames.indexOf(frame)] = {
    id,
    surface:frame.surface.id,
    state:frame.state,
    coverage:frame.coverage,
    viewport:frame.viewport.size,
    viewportId:frame.viewport.id,
    mode:frame.viewport.mode,
    target:`targets/${frame.surface.id}/${basename}.png`,
    annotation:`targets/${frame.surface.id}/${basename}.yaml`,
    renderer:`renderer/index.html?surface=${frame.surface.id}&state=${frame.state}&viewport=${frame.viewport.size}`,
    sha256:existsSync(png) ? sha256(png) : null,
  };
}
const registry = {
  schemaVersion:1,
  contract,
  renderer:'renderer/',
  frameCount:frames.length,
  canonicalCount:frames.filter(frame => frame.coverage === 'canonical').length,
  responsiveRiskCount:frames.filter(frame => frame.coverage === 'responsive-risk').length,
  complete:frames.every(frame => frame.sha256),
  intentionalPixelAliases:[
    { ids:['guest-start.exit-hold-idle.standard','guest-start.exit-hold-cancelled.standard'], rationale:'A cancelled hold returns cleanly to the exact idle visual state; only the transition history differs.' },
    { ids:['capture.camera-failure.standard','operator-assistance.camera-3-preserved.standard'], rationale:'A Camera failure routes immediately to the canonical Operator Assistance state with three preserved Captures.' },
    { ids:['capture.storage-failure.standard','operator-assistance.storage-3-preserved.standard'], rationale:'A storage failure routes immediately to the canonical Operator Assistance state with three preserved Captures.' },
    { ids:['operator-assistance.photo-strip-4-preserved.standard','photo-strip.failed.standard'], rationale:'Photo Strip failure routes immediately to the canonical Operator Assistance state with all four Captures preserved.' },
  ],
  frames,
};
writeFileSync(join(root,'registry.json'), `${JSON.stringify(registry,null,2)}\n`);
writeFileSync(join(root,'capture-plan.json'), `${JSON.stringify(frames.map(({id,surface,state,viewport,target,renderer})=>({id,surface,state,viewport,target,renderer})),null,2)}\n`);
console.log(JSON.stringify({frames:frames.length, complete:registry.complete, missing:frames.filter(frame=>!frame.sha256).length}));
