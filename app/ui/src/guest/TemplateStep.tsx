import { useBrainClient } from "../brain/BrainClientContext.ts";
import { OptionList, type OptionListItem } from "../components/OptionList/index.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { PrintObject } from "../components/PrintObject/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, TemplateView } from "../protocol/generated.ts";
import { templateActions, templateCopy, templatePreviewAlt } from "./captureCopy.ts";
import { usePhotographsCommands } from "./PhotographsCommands.ts";
import { templateImageSource } from "./TemplateArt.ts";
import styles from "./TemplateStep.module.css";

/**
 * The Template step (app/DESIGN.md, Guest Screen): a 5 / 7 split. The selected Template's blank preview rests on a
 * Cream Paper mat beside the heading (Panorama Four turned a quarter counterclockwise, as the website shows it), and the
 * offered Templates are an option list with a thumbnail, name, and description each. `CONTINUE TO PHOTOGRAPHS →` is the
 * one primary action. The brain owns which Template is selected; the list only asks.
 */
export function TemplateStep({ view }: { readonly cycle: GuestCycleView; readonly view: TemplateView }) {
  const client = useBrainClient();
  const commands = usePhotographsCommands();
  const selected = view.templates.find((template) => template.id === view.selectedId) ?? null;

  const items: readonly OptionListItem[] = view.templates.map((template) => ({
    value: template.id,
    title: template.name,
    description: template.description,
    thumbnail: <img src={templateImageSource(template, client.previewUrl)} alt="" draggable={false} />,
  }));

  return (
    <div className={styles.step} data-selected-template={view.selectedId}>
      <div className={styles.print}>
        <StatementHeading
          level={1}
          size="heading-sm"
          lead={templateCopy.lead}
          turn={templateCopy.turn}
          supporting={templateCopy.supporting}
        />
        <div className={styles.mat} data-tone="paper">
          {selected !== null && (
            <PrintObject
              src={templateImageSource(selected, client.previewUrl)}
              alt={templatePreviewAlt(selected.name)}
              width={selected.width}
              height={selected.height}
              rotated={selected.landscape}
            />
          )}
        </div>
      </div>
      <div className={styles.options}>
        <OptionList
          variant="radio"
          label={templateActions.listLabel}
          hideLabel
          items={items}
          value={view.selectedId}
          onChange={commands.selectTemplate}
        />
        <div className={styles.action}>
          <PrimaryAction
            arrow="right"
            disabled={!view.canContinue}
            onClick={() => {
              commands.goToStep("photographs");
            }}
          >
            {templateActions.continue}
          </PrimaryAction>
        </div>
      </div>
    </div>
  );
}
