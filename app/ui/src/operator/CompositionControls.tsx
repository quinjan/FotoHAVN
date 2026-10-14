import { SecondaryAction } from "../components/SecondaryAction/index.ts";
import { Select } from "../components/Select/index.ts";
import { lookChoices, templateChoices } from "../components/CameraDiagnosticsScreen/catalogNames.ts";
import styles from "./CompositionControls.module.css";

export interface CompositionControlsProps {
  readonly templateId: string;
  readonly lookId: string;
  readonly onTemplateChange: (templateId: string) => void;
  readonly onLookChange: (lookId: string) => void;
  readonly onCompose: () => void;
  readonly onBlankPreview: () => void;
  /** A composition is in flight: both actions are unavailable until it ends. */
  readonly composing?: boolean;
}

/**
 * The E2E composition controls of the camera diagnostics screen: a Template and a Look to choose, `COMPOSE` (the
 * diagnostics captures into that Template) and `BLANK PREVIEW` (the Template with every window empty). They send
 * `test.composeDiagnostics` and `test.composeBlankPreview` through `CameraDiagnosticsHooks`, the only module that
 * imports this one, so field builds contain neither the controls nor the hook names.
 */
export function CompositionControls({
  templateId,
  lookId,
  onTemplateChange,
  onLookChange,
  onCompose,
  onBlankPreview,
  composing = false,
}: CompositionControlsProps) {
  return (
    <div className={styles.compose}>
      <Select
        label="Template"
        value={templateId}
        onChange={(event) => {
          onTemplateChange(event.target.value);
        }}
        options={templateChoices.map((choice) => ({ value: choice.id, label: choice.name }))}
      />
      <Select
        label="Look"
        value={lookId}
        onChange={(event) => {
          onLookChange(event.target.value);
        }}
        options={lookChoices.map((choice) => ({ value: choice.id, label: choice.name }))}
      />
      <SecondaryAction disabled={composing} onClick={onCompose}>
        COMPOSE
      </SecondaryAction>
      <SecondaryAction disabled={composing} onClick={onBlankPreview}>
        BLANK PREVIEW
      </SecondaryAction>
    </div>
  );
}
