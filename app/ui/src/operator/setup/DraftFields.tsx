import { DateField } from "../../components/DateField/index.ts";
import { TextArea } from "../../components/TextArea/index.ts";
import { TextField } from "../../components/TextField/index.ts";
import type { PhaseTimerOption } from "../../protocol/generated.ts";
import { timerFieldId, timerIsOff, timerNames, timerOffCopy, timerRange, timerRunsOut } from "./setupCopy.ts";
import { useDraftText } from "./useDraftText.ts";
import styles from "./EventSetup.module.css";

interface DraftFieldProps {
  readonly id: string;
  readonly label: string;
  /** The brain's value for the field. */
  readonly value: string;
  readonly error: string | undefined;
  readonly required?: boolean;
  readonly maxLength?: number;
  readonly onCommit: (value: string) => void;
}

/** A single-line text field whose typing is debounced into one command per pause, and one on blur. */
export function DraftTextField({ id, label, value, error, required = false, maxLength, onCommit }: DraftFieldProps) {
  const draft = useDraftText(value, onCommit);
  return (
    <TextField
      id={id}
      label={label}
      value={draft.text}
      error={error}
      required={required}
      maxLength={maxLength}
      autoComplete="off"
      onChange={(event) => {
        draft.change(event.target.value);
      }}
      onBlur={draft.blur}
    />
  );
}

/** The Event's notes: the same debounce as the other text fields, in a text area. */
export function DraftTextArea({ id, label, value, error, maxLength, onCommit }: DraftFieldProps) {
  const draft = useDraftText(value, onCommit);
  return (
    <TextArea
      id={id}
      label={label}
      value={draft.text}
      error={error}
      maxLength={maxLength}
      onChange={(event) => {
        draft.change(event.target.value);
      }}
      onBlur={draft.blur}
    />
  );
}

/** The Event's date: the native date input, sent whole as soon as the operator picks or clears it. */
export function DraftDateField({ id, label, value, error, required = false, onCommit }: DraftFieldProps) {
  const draft = useDraftText(value, onCommit, { delayMs: 0 });
  return (
    <DateField
      id={id}
      label={label}
      value={draft.text}
      error={error}
      required={required}
      onChange={(event) => {
        draft.change(event.target.value);
      }}
      onBlur={draft.blur}
    />
  );
}

const wholeSeconds = /^\d+$/;

export interface TimerFieldProps {
  readonly timer: PhaseTimerOption;
  readonly error: string | undefined;
  readonly onCommit: (phase: PhaseTimerOption["phase"], seconds: number) => void;
}

/**
 * One Phase Timer: seconds typed as digits, with the range under it, "Off" in words where 0 turns it off, and what
 * happens when the time runs out (or when it is off). A half-typed value stays visible while typing and reverts on blur.
 */
export function TimerField({ timer, error, onCommit }: TimerFieldProps) {
  const draft = useDraftText(String(timer.seconds), (text) => {
    onCommit(timer.phase, Number(text));
  }, { isValid: (text) => wholeSeconds.test(text) });
  const off = timer.canBeOff && timer.seconds === 0;
  return (
    <TextField
      id={timerFieldId(timer.phase)}
      label={timerNames[timer.phase]}
      value={draft.text}
      error={error}
      className={styles.timer}
      inputMode="numeric"
      autoComplete="off"
      maxLength={3}
      showCount={false}
      help={[off ? timerIsOff : null, timerRange(timer), timerRunsOut[timer.phase], timer.canBeOff ? timerOffCopy : null]
        .filter((line) => line !== null)
        .join(" ")}
      onChange={(event) => {
        draft.change(event.target.value.replace(/\D/g, ""));
      }}
      onBlur={draft.blur}
    />
  );
}
