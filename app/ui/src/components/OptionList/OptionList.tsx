import { useId, useRef, type KeyboardEvent, type ReactNode } from "react";
import { ChoiceMark, choiceStyles } from "../shared/ChoiceMark.tsx";
import { cx } from "../shared/cx.ts";
import { describedBy, FieldMessages, fieldStyles, joinIds, present, useFieldIds, type FieldText } from "../shared/field.tsx";
import { arrowStep, handleRadioArrow, nextEnabledIndex, tabStopValue } from "../shared/roving.ts";
import styles from "./OptionList.module.css";

export interface OptionListItem {
  readonly value: string;
  /** The option's name in the serif Subheading role, e.g. a Template or Look name from the brain. */
  readonly title: ReactNode;
  /** A Supporting line under the title. */
  readonly description?: ReactNode;
  /** A count or meta line in the Meta role, e.g. `4 PHOTOGRAPHS` or `1920 × 1080 · LEFT`. */
  readonly meta?: ReactNode;
  /** A small bordered marker beside the title, e.g. "Operator Screen" or "Not connected". */
  readonly badge?: ReactNode;
  /** A small picture before the text, on a Cream Paper mat. Decorative: the title names the option. */
  readonly thumbnail?: ReactNode;
  readonly disabled?: boolean | undefined;
}

/** The "default" radio on each row of a checkbox list, e.g. the Default Template among the offered Templates. */
export interface OptionListDefaultChoice {
  /** The default item's value; `null` when none is chosen. */
  readonly value: string | null;
  readonly onChange?: ((value: string) => void) | undefined;
  /** The visible label beside each row's radio. "Default" when omitted. */
  readonly label?: string | undefined;
}

interface OptionListBaseProps extends FieldText {
  readonly items: readonly OptionListItem[];
  /** Hides the legend visually (it stays the group's accessible name), when a heading above already says it. */
  readonly hideLabel?: boolean | undefined;
  readonly id?: string | undefined;
  readonly className?: string | undefined;
}

export interface OptionListRadioProps extends OptionListBaseProps {
  /** Choose one, e.g. the guest's Template or Look, or the Guest display. */
  readonly variant: "radio";
  /** The chosen value; `null` when nothing is chosen. */
  readonly value: string | null;
  readonly onChange?: ((value: string) => void) | undefined;
}

export interface OptionListCheckboxProps extends OptionListBaseProps {
  /** Choose any number, e.g. the offered Templates or Looks in Event setup. */
  readonly variant: "checkbox";
  /** The checked values. */
  readonly values: readonly string[];
  /** Called with the new checked values, in the items' order. */
  readonly onChange?: ((values: readonly string[]) => void) | undefined;
  /** Adds a "Default" radio to each row, enabled only on checked rows. */
  readonly defaultChoice?: OptionListDefaultChoice | undefined;
}

export type OptionListProps = OptionListRadioProps | OptionListCheckboxProps;

/**
 * The brand option list (brand components.md): rows separated by Hairline rules, each with a circular radio or a
 * checkbox first, then an optional thumbnail, a serif title, a description, and a meta line. The selected row takes Warm
 * Ivory, a 2 px Muted Brass left rule, and the checked mark, never color alone. Works on both screens: targets and
 * type follow the role.
 *
 * - `radio`: one tab stop; the arrow keys move between rows and select (the ARIA radio group pattern).
 * - `checkbox`: each row is a checkbox; the up and down arrows (and Home and End) move between rows, Space toggles.
 *   With `defaultChoice`, each row also gets a "Default" radio, enabled on checked rows, forming its own radio group.
 */
export function OptionList(props: OptionListProps) {
  const { label, help, error, items, hideLabel = false, id, className } = props;
  const ids = useFieldIds(id);
  const name = useId();
  const defaultName = useId();
  const mainInputs = useRef<(HTMLInputElement | null)[]>([]);
  const defaultInputs = useRef<(HTMLInputElement | null)[]>([]);
  const invalid = present(error) || undefined;

  const isChecked = (item: OptionListItem) =>
    props.variant === "radio" ? props.value === item.value : props.values.includes(item.value);
  const radioTabStop = props.variant === "radio" ? tabStopValue(items, props.value) : undefined;

  const defaultChoice = props.variant === "checkbox" ? props.defaultChoice : undefined;
  const defaultItems = items.map((item) => ({ value: item.value, disabled: item.disabled === true || !isChecked(item) }));
  const defaultTabStop = defaultChoice === undefined ? undefined : tabStopValue(defaultItems, defaultChoice.value);

  function choose(item: OptionListItem) {
    if (props.variant === "radio") {
      props.onChange?.(item.value);
      return;
    }
    const checked = new Set(props.values);
    if (checked.has(item.value)) {
      checked.delete(item.value);
    } else {
      checked.add(item.value);
    }
    props.onChange?.(items.map((each) => each.value).filter((value) => checked.has(value)));
  }

  function handleMainKey(event: KeyboardEvent<HTMLInputElement>, index: number) {
    if (props.variant === "radio") {
      handleRadioArrow(event, items, index, mainInputs.current, (value) => props.onChange?.(value));
      return;
    }
    let next: number | undefined;
    if (event.key === "Home" || event.key === "End") {
      const enabled = items.flatMap((item, at) => (item.disabled === true ? [] : [at]));
      next = event.key === "Home" ? enabled[0] : enabled.at(-1);
    } else {
      const step = arrowStep(event.key, "vertical");
      if (step === undefined) {
        return;
      }
      next = nextEnabledIndex(items, index, step, false);
    }
    event.preventDefault();
    if (next !== undefined) {
      mainInputs.current[next]?.focus();
    }
  }

  return (
    <fieldset
      id={ids.control}
      className={cx(fieldStyles.field, styles.root, className)}
      aria-describedby={describedBy(ids, { help, error })}
    >
      <legend id={ids.label} className={cx(fieldStyles.label, fieldStyles.legend, hideLabel && fieldStyles.visuallyHidden)}>
        {label}
      </legend>
      <ul className={styles.list}>
        {items.map((item, index) => {
          const checked = isChecked(item);
          const rowId = `${ids.control}-${String(index)}`;
          const titleId = `${rowId}-title`;
          const badgeId = `${rowId}-badge`;
          const descriptionId = `${rowId}-description`;
          const metaId = `${rowId}-meta`;
          const defaultLabelId = `${rowId}-default`;
          return (
            <li key={item.value} className={styles.row} data-selected={checked || undefined} data-disabled={item.disabled === true || undefined}>
              <label className={styles.choice}>
                <input
                  ref={(element) => {
                    mainInputs.current[index] = element;
                  }}
                  type={props.variant}
                  className={choiceStyles.input}
                  name={props.variant === "radio" ? name : undefined}
                  value={item.value}
                  checked={checked}
                  disabled={item.disabled}
                  tabIndex={props.variant === "radio" ? (item.value === radioTabStop ? 0 : -1) : undefined}
                  aria-labelledby={titleId}
                  aria-describedby={joinIds(
                    present(item.badge) ? badgeId : undefined,
                    present(item.description) ? descriptionId : undefined,
                    present(item.meta) ? metaId : undefined,
                  )}
                  aria-invalid={props.variant === "checkbox" ? invalid : undefined}
                  onChange={() => {
                    choose(item);
                  }}
                  onKeyDown={(event) => {
                    handleMainKey(event, index);
                  }}
                />
                <ChoiceMark kind={props.variant} />
                {present(item.thumbnail) && (
                  <span className={styles.thumbnail} aria-hidden="true">
                    {item.thumbnail}
                  </span>
                )}
                <span className={styles.text}>
                  <span className={styles.titleLine}>
                    <span id={titleId} className={styles.title}>
                      {item.title}
                    </span>
                    {present(item.badge) && (
                      <span id={badgeId} className={styles.badge}>
                        {item.badge}
                      </span>
                    )}
                  </span>
                  {present(item.description) && (
                    <span id={descriptionId} className={styles.description}>
                      {item.description}
                    </span>
                  )}
                  {present(item.meta) && (
                    <span id={metaId} className={styles.meta}>
                      {item.meta}
                    </span>
                  )}
                </span>
              </label>
              {defaultChoice !== undefined && (
                <label className={styles.default}>
                  <input
                    ref={(element) => {
                      defaultInputs.current[index] = element;
                    }}
                    type="radio"
                    className={choiceStyles.input}
                    name={defaultName}
                    value={item.value}
                    checked={defaultChoice.value === item.value}
                    disabled={defaultItems[index]?.disabled}
                    tabIndex={item.value === defaultTabStop ? 0 : -1}
                    aria-labelledby={`${defaultLabelId} ${titleId}`}
                    onChange={() => defaultChoice.onChange?.(item.value)}
                    onKeyDown={(event) => {
                      handleRadioArrow(event, defaultItems, index, defaultInputs.current, (value) => defaultChoice.onChange?.(value));
                    }}
                  />
                  <ChoiceMark kind="radio" />
                  <span id={defaultLabelId} className={styles.defaultLabel}>
                    {defaultChoice.label ?? "Default"}
                  </span>
                </label>
              )}
            </li>
          );
        })}
      </ul>
      <FieldMessages ids={ids} help={help} error={error} />
    </fieldset>
  );
}
