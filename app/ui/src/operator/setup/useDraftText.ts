import { useEffect, useRef, useState } from "react";

/** How long a typed change waits for the next keystroke before it is sent: each change is one command. */
export const draftIdleMs = 300;

export interface DraftTextOptions {
  /** Idle time before a change is sent; 0 sends at once, for a control whose every change is complete (a date). */
  readonly delayMs?: number;
  /** Whether the text can be sent. Text that can't (a half-typed number) isn't sent, and on blur it reverts to the brain's value. */
  readonly isValid?: (text: string) => boolean;
}

export interface DraftText {
  readonly text: string;
  /** For the input's `onChange`. */
  readonly change: (next: string) => void;
  /** For the input's `onBlur`: sends a pending change now, or reverts text that can't be sent. */
  readonly blur: () => void;
}

/**
 * The text of one field while it is being typed. The brain owns the value; this holds only what is in progress. A
 * change is sent after a short idle, or at once on blur. A value the brain changes for a field that isn't being edited
 * (a draft opened for another Event, a reset) replaces the text; while the field is being edited, the brain's echoes of
 * earlier keystrokes are ignored, so they can't undo newer ones.
 */
export function useDraftText(value: string, commit: (text: string) => void, options: DraftTextOptions = {}): DraftText {
  const { delayMs = draftIdleMs, isValid = () => true } = options;
  const [text, setText] = useState(value);
  const [seen, setSeen] = useState(value);
  const [editing, setEditing] = useState(false);
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const unsent = useRef(false);
  const latest = useRef({ commit, isValid });

  useEffect(() => {
    latest.current = { commit, isValid };
  });

  useEffect(
    () => () => {
      if (timer.current !== null) {
        clearTimeout(timer.current);
      }
    },
    [],
  );

  if (seen !== value) {
    setSeen(value);
    if (!editing) {
      setText(value);
    }
  }

  function clearTimer(): void {
    if (timer.current !== null) {
      clearTimeout(timer.current);
      timer.current = null;
    }
  }

  function send(next: string): void {
    clearTimer();
    if (latest.current.isValid(next)) {
      unsent.current = false;
      latest.current.commit(next);
    }
  }

  return {
    text,
    change(next) {
      setText(next);
      setEditing(true);
      unsent.current = true;
      clearTimer();
      if (delayMs <= 0) {
        send(next);
        return;
      }
      timer.current = setTimeout(() => {
        send(next);
      }, delayMs);
    },
    blur() {
      setEditing(false);
      if (!unsent.current) {
        return;
      }
      if (isValid(text)) {
        send(text);
      } else {
        clearTimer();
        unsent.current = false;
        setText(value);
      }
    },
  };
}
