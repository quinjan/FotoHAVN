/**
 * The Template and Photographs steps' copy (plan section 7, Steps; app/DESIGN.md, Guest Screen copy). Owner: M8 UI-1.
 * The headings, supporting lines, and the Arrange hint are fixed by the plan; everything else follows `brand/voice.md`.
 */

export const templateCopy = {
  lead: "Make a little",
  /** The plan copies the website's approved line exactly, with a capital M. */
  turn: "Moment",
  supporting: "Choose your template.",
} as const;

export const templateActions = {
  continue: "CONTINUE TO PHOTOGRAPHS",
  /** The option list's accessible name; the heading beside it already says it, so it is not shown. */
  listLabel: "Template",
} as const;

/** The alt text of a Template's blank preview: it names the Template and nothing about faces or people. */
export function templatePreviewAlt(name: string): string {
  return `Blank ${name} Photo Strip`;
}

export const cameraCopy = {
  /** The view's heading, for assistive technology only: S1 shows no heading over the live preview. */
  lead: "Find your",
  turn: "light.",
  /** The accessible name of the whole live preview, the one control that starts the sequence; it begins with the visible prompt (WCAG 2.5.3). */
  start: "Tap to start taking photographs",
  /** The prompt over the preview's centre while the sequence can start. */
  tapToStart: "TAP TO START",
  /** Said in the prompt's place while the sequence can't start yet; tapping does nothing. */
  waiting: "One moment. The camera is getting ready.",
  /** Camera tab to Arrange tab, once a photograph exists. */
  review: "SEE MY PHOTOGRAPHS",
  /** While a Retake runs, the way back to the photograph that is already there. */
  keepOriginal: "KEEP ORIGINAL",
  previewLabel: "Live camera preview",
  /** Shown on the countdown's last second, so the group looks at the lens and not at the screen. Visual only. */
  lookAtCamera: "Look at the camera.",
} as const;

export const arrangeCopy = {
  lead: "Arrange your",
  turn: "moment.",
  hint: "Drag to rearrange · Tap a photograph to edit",
  retake: "RETAKE",
  move: "MOVE",
  cancelMove: "CANCEL MOVE",
  accept: "ACCEPT",
  tryAgain: "TRY AGAIN",
  keepOriginal: "KEEP ORIGINAL",
  /** Arrange tab to Camera tab while a window is still empty. */
  takeMore: "TAKE MORE PHOTOGRAPHS",
  continue: "CONTINUE TO FILTERS",
  /** With one Look offered the Filters step is skipped, so leaving Photographs finishes the strip. */
  finish: "FINISH MY STRIP",
  stripLabel: "Your Photo Strip",
  original: "Original",
  candidate: "New",
  emptyHint: "Take a photograph for each empty position to continue.",
  selected: (position: number) => `Photograph ${String(position)} is selected.`,
  moving: (position: number) => `Choose where photograph ${String(position)} goes.`,
  comparing: (position: number) => `Keep the new photograph for position ${String(position)}, or try again.`,
  /** The window buttons' names; the position is the strip's, counted from 1. */
  windowName: (position: number, total: number) => `Photograph ${String(position)} of ${String(total)}`,
  emptyWindowName: (position: number) => `Position ${String(position)} is empty.`,
  /** The visible label on every destination window while a Move waits (the Meta role sets it in capitals). */
  moveHere: "Move here",
  /** A destination begins with its visible label, so the accessible name contains the words on screen. */
  moveTarget: (position: number, occupied: boolean) =>
    occupied ? `Move here: swap with position ${String(position)}` : `Move here: position ${String(position)}`,
  moveSource: (position: number) => `Photograph ${String(position)}, moving`,
  /** Said once, politely, when a Retake's new photograph arrives or a photograph is placed. */
  announceCandidate: "Your new photograph is ready. Accept it, try again, or keep the original.",
  announceMoved: (position: number) => `Photograph placed in position ${String(position)}.`,
} as const;
