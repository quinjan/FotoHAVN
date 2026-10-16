/**
 * The Filters, Keepsake, and Operator Assistance copy, and the cycle chrome's (plan section 7, Steps and Failures; app/DESIGN.md, Guest
 * Screen copy). Owner: M8 UI-2. The headings and supporting lines are fixed by the plan; everything else follows `brand/voice.md`.
 */

export const filtersCopy = {
  lead: "Find the",
  turn: "feeling.",
  supporting: "Choose a filter for your photographs.",
} as const;

export const finishStripLabel = "FINISH MY STRIP";

/** Shown while the brain composes a Look's strip preview or the final Photo Strip; announced once when it starts. */
export const composingLine = "Preparing your strip.";

/** Q25: a failed composition keeps the previous preview visible, with this line and `TRY AGAIN`. */
export const compositionFailedLine = "Your strip couldn't be prepared. Try again.";
export const retryCompositionLabel = "TRY AGAIN";

/** The Look list's accessible name; the heading above already says what to do, so the legend stays hidden. */
export const looksLabel = "Filters";

/** The strip preview's alternative text: it describes the object, never the people in it. */
export const stripPreviewAlt = "Preview of your Photo Strip";

export const keepsakeCopy = {
  lead: "A little moment.",
  turn: "Yours.",
  supporting: "Keep the moment close.",
} as const;

export const keepsakeStripAlt = "Your Photo Strip";

export const assistanceCopy = {
  lead: "A little",
  turn: "interruption.",
  supporting: "Please ask the FOTOHAVN team for help.",
} as const;

export const retryLabel = "RETRY";

/** While `RetryAssistance` is being checked: the one line beside the unavailable `RETRY`. It matches the operator's "Trying again…". */
export const retryingLine = "Trying again…";

/** When `RetryAssistance` itself doesn't go through (the brain isn't reachable). */
export const retryNotSentLine = "That didn't go through. Try again.";

/** The step progress's accessible name. */
export const stepsLabel = "Steps";