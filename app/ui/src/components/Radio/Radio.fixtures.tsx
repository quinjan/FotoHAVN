import type { ComponentFixture } from "../fixture.ts";
import { cx } from "../shared/cx.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import fieldStyles from "../shared/field.module.css";
import styles from "../shared/formFixture.module.css";
import { Radio } from "./Radio.tsx";

function Group({ checked, disabled = false }: { readonly checked: string | null; readonly disabled?: boolean }) {
  return (
    <fieldset className={fieldStyles.field} disabled={disabled}>
      <legend className={cx(fieldStyles.label, fieldStyles.legend)}>Sort Events by</legend>
      <Radio name="sort" value="date" label="Date, newest first" checked={checked === "date"} onChange={noop} />
      <Radio
        name="sort"
        value="used"
        label="Last used"
        description="The most recently run Events come first."
        checked={checked === "used"}
        onChange={noop}
      />
    </fieldset>
  );
}

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "radio-unchecked",
    title: "Radio group, nothing chosen",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Group checked={null} />
      </div>
    ),
  },
  {
    id: "radio-checked",
    title: "Radio group, one chosen",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Group checked="used" />
      </div>
    ),
  },
  {
    id: "radio-focus",
    title: "Radio, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <FocusFirst>
          <Group checked="date" />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "radio-disabled",
    title: "Radio group, disabled",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Group checked="date" disabled />
      </div>
    ),
  },
  {
    id: "radio-dark",
    title: "Radio group, on a dark band",
    surface: "dark",
    role: "operator",
    render: () => (
      <div className={styles.form}>
        <Group checked="used" />
      </div>
    ),
  },
];
