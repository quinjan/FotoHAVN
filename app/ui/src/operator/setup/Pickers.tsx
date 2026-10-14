import { useBrainClient } from "../../brain/BrainClientContext.ts";
import { OptionList, type OptionListItem } from "../../components/OptionList/index.ts";
import { PrintObject } from "../../components/PrintObject/index.ts";
import type { EventSetupLookOption, EventSetupTemplateOption } from "../../protocol/generated.ts";
import { copy, fieldIds } from "./setupCopy.ts";
import styles from "./EventSetup.module.css";

export interface TemplatePickerProps {
  readonly templates: readonly EventSetupTemplateOption[];
  readonly error: string | undefined;
  readonly onOfferedChange: (templateIds: readonly string[]) => void;
  readonly onDefaultChange: (templateId: string) => void;
}

/**
 * The Templates to offer, each with its blank preview (the Template with every window empty, composed by the brain and
 * served at the transport's preview address) and a radio that marks the default among the offered ones. A landscape
 * Template such as Panorama Four is turned a quarter, as the guest sees it.
 */
export function TemplatePicker({ templates, error, onOfferedChange, onDefaultChange }: TemplatePickerProps) {
  const client = useBrainClient();
  const items: OptionListItem[] = templates.map((template) => ({
    value: template.id,
    title: template.name,
    description: template.description,
    meta: `${String(template.width)} × ${String(template.height)}`,
    thumbnail:
      template.blankPreviewKey === null || client.previewUrl === undefined ? undefined : (
        <PrintObject
          src={client.previewUrl(template.blankPreviewKey)}
          alt=""
          width={template.width}
          height={template.height}
          rotated={template.landscape}
        />
      ),
  }));
  return (
    <div className={styles.templatePicker}>
      <OptionList
        variant="checkbox"
        id={fieldIds.templates}
        label={copy.fields.templates}
        help={copy.fields.templatesHelp}
        error={error}
        items={items}
        values={templates.filter((template) => template.offered).map((template) => template.id)}
        defaultChoice={{ value: templates.find((template) => template.isDefault)?.id ?? null, onChange: onDefaultChange }}
        onChange={onOfferedChange}
      />
    </div>
  );
}

export interface LookPickerProps {
  readonly looks: readonly EventSetupLookOption[];
  readonly error: string | undefined;
  readonly onOfferedChange: (lookIds: readonly string[]) => void;
  readonly onDefaultChange: (lookId: string) => void;
}

/** The Looks to offer, in the brand's order, with a radio that marks the default among the offered ones. */
export function LookPicker({ looks, error, onOfferedChange, onDefaultChange }: LookPickerProps) {
  const items: OptionListItem[] = looks.map((look) => ({ value: look.id, title: look.name, description: look.description }));
  return (
    <OptionList
      variant="checkbox"
      id={fieldIds.looks}
      label={copy.fields.looks}
      help={copy.fields.looksHelp}
      error={error}
      items={items}
      values={looks.filter((look) => look.offered).map((look) => look.id)}
      defaultChoice={{ value: looks.find((look) => look.isDefault)?.id ?? null, onChange: onDefaultChange }}
      onChange={onOfferedChange}
    />
  );
}
