import { useState } from "react";
import { DateField } from "../../components/DateField/index.ts";
import { fieldStyles, FieldError } from "../../components/shared/field.tsx";
import { Switch } from "../../components/Switch/index.ts";
import { TextArea } from "../../components/TextArea/index.ts";
import { TextField } from "../../components/TextField/index.ts";
import type { PhaseTimerOption } from "../../protocol/generated.ts";
import { formatEventDate } from "../format.ts";
import { timerControlCopy, timerFieldId, timerHelp, timerNames } from "./setupCopy.ts";
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

/** The Event's date: the native date input, sent whole as soon as the operator picks or clears it, with the date in words beside it. */
export function DraftDateField({ id, label, value, error, required = false, onCommit }: DraftFieldProps) {
  const draft = useDraftText(value, onCommit, { delayMs: 0 });
  return (
    <DateField
      id={id}
      label={label}
      value={draft.text}
      error={error}
      required={required}
      formatValue={formatEventDate}
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
  /** Every timer, so the help can leave out what the Timing step already says once for the group. */
  readonly timers: readonly PhaseTimerOption[];
  readonly error: string | undefined;
  readonly onCommit: (phase: PhaseTimerOption["phase"], seconds: number) => void;
}

/**
 * One Phase Timer: its name, a seconds field with an `s` unit, and, where the timer can be off, an On/Off switch beside
 * it (off is the stored 0). Turning a timer off keeps its last seconds in the dimmed field, and turning it on again
 * restores them; a timer never on in this session comes back at its default. Help says only what differs for the
 * phase: what happens when the time runs out. A half-typed value stays visible while typing and reverts on blur.
 */
export function TimerField({ timer, timers, error, onCommit }: TimerFieldProps) {
  const draft = useDraftText(
    String(timer.seconds),
    (text) => {
      onCommit(timer.phase, Number(text));
    },
    // Zero isn't a value to type for a timer that can be off: the switch is how to turn it off.
    { isValid: (text) => wholeSeconds.test(text) && (!timer.canBeOff || Number(text) > 0) },
  );
  const [lastOn, setLastOn] = useState(timer.defaultSeconds);
  if (timer.seconds > 0 && timer.seconds !== lastOn) {
    setLastOn(timer.seconds);
  }
  const off = timer.canBeOff && timer.seconds === 0;
  const name = timerNames[timer.phase];
  const labelId = `${timerFieldId(timer.phase)}-label`;
  const helpId = `${timerFieldId(timer.phase)}-help`;
  const errorId = `${timerFieldId(timer.phase)}-error`;
  const describedBy = error === undefined ? helpId : `${errorId} ${helpId}`;
  const restored = lastOn >= timer.min && lastOn <= timer.max ? lastOn : timer.defaultSeconds;

  return (
    <div role="group" aria-labelledby={labelId} className={styles.timerField}>
      <span id={labelId} className={fieldStyles.label}>
        {name}
      </span>
      <div className={styles.timerControls}>
        {timer.canBeOff && (
          <Switch
            id={`${timerFieldId(timer.phase)}-switch`}
            label={timerControlCopy.switchName(name)}
            hideLabel
            className={styles.timerSwitch}
            checked={!off}
            aria-describedby={helpId}
            onCheckedChange={(on) => {
              onCommit(timer.phase, on ? restored : 0);
            }}
          />
        )}
        <TextField
          id={timerFieldId(timer.phase)}
          label={timerControlCopy.secondsName(name)}
          hideLabel
          suffix={timerControlCopy.unit}
          value={off ? String(restored) : draft.text}
          disabled={off}
          className={styles.timer}
          inputMode="numeric"
          autoComplete="off"
          maxLength={3}
          showCount={false}
          invalid={error !== undefined}
          aria-describedby={describedBy}
          onChange={(event) => {
            draft.change(event.target.value.replace(/\D/g, ""));
          }}
          onBlur={draft.blur}
        />
      </div>
      {error !== undefined && <FieldError id={errorId}>{error}</FieldError>}
      <p id={helpId} className={fieldStyles.help}>
        {timerHelp(timer, timers)}
      </p>
    </div>
  );
}
