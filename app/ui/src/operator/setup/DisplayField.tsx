import { COVERS_OPERATOR_NOTE, DisplayPicker, type DisplayPickerDisplay } from "../../components/DisplayPicker/index.ts";
import type { EventSetupGuestDisplayView, GuestDisplayOption } from "../../protocol/generated.ts";
import { fieldIds } from "./setupCopy.ts";

/** Where a display sits against the Operator Screen's, in words: "Display 2, right of the Operator Screen". */
export function displayPosition(display: GuestDisplayOption, operator: GuestDisplayOption | undefined): string {
  const label = `Display ${String(display.number)}`;
  if (operator === undefined || display.id === operator.id) {
    return label;
  }
  const dx = display.x + display.width / 2 - (operator.x + operator.width / 2);
  const dy = display.y + display.height / 2 - (operator.y + operator.height / 2);
  const side = Math.abs(dx) >= Math.abs(dy) ? (dx > 0 ? "right of" : "left of") : dy > 0 ? "below" : "above";
  return `${label}, ${side} the Operator Screen`;
}

export interface DisplayFieldProps {
  readonly guestDisplay: EventSetupGuestDisplayView;
  readonly error: string | undefined;
  /** `IDENTIFY DISPLAYS` is in progress: the numbers are on the displays. */
  readonly identifying: boolean;
  readonly onSelect: (displayId: string) => void;
  readonly onIdentify: () => void;
}

/** The Guest display field: the displays with their numbers, the Operator Screen marked, and `IDENTIFY DISPLAYS`. */
export function DisplayField({ guestDisplay, error, identifying, onSelect, onIdentify }: DisplayFieldProps) {
  const operator = guestDisplay.displays.find((display) => display.isOperatorScreen);
  const displays: DisplayPickerDisplay[] = guestDisplay.displays.map((display) => ({
    id: display.id,
    name: display.name,
    width: display.width,
    height: display.height,
    position: displayPosition(display, operator),
    isOperatorScreen: display.isOperatorScreen,
  }));
  const chosen = guestDisplay.displays.find((display) => display.id === guestDisplay.selectedId);
  // One screen only: the picker's own note is about the Operator Screen's row, so it says nothing for another display.
  const coversWithoutMarker = guestDisplay.coversOperatorScreen && chosen !== undefined && !chosen.isOperatorScreen;
  return (
    <DisplayPicker
      id={fieldIds.guestDisplay}
      displays={displays}
      value={guestDisplay.selectedId}
      boundDisplayName={guestDisplay.selectedName ?? undefined}
      error={error}
      help={coversWithoutMarker ? COVERS_OPERATOR_NOTE : undefined}
      identifyDisabled={identifying}
      onChange={onSelect}
      onIdentify={onIdentify}
    />
  );
}
