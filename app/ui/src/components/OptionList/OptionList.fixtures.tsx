import { useEffect, useRef, type ReactNode } from "react";
import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/formFixture.module.css";
import thumbs from "./OptionList.fixtureThumbnails.module.css";
import { OptionList, type OptionListItem } from "./OptionList.tsx";

function Strip({ windows }: { readonly windows: number }) {
  const height = 30 / windows;
  return (
    <svg className={thumbs.template} viewBox="0 0 12 36" aria-hidden="true">
      <rect className={thumbs.paper} x="0.5" y="0.5" width="11" height="35" />
      {Array.from({ length: windows }, (_, index) => (
        <rect key={index} className={thumbs.window} x="1.5" y={1.5 + index * height} width="9" height={height - 1} />
      ))}
    </svg>
  );
}

function Panorama() {
  return (
    <svg className={thumbs.template} viewBox="0 0 36 12" aria-hidden="true">
      <rect className={thumbs.paper} x="0.5" y="0.5" width="35" height="11" />
      {[0, 1, 2, 3].map((index) => (
        <rect key={index} className={thumbs.window} x={1.5 + index * 8.5} y="1.5" width="7.5" height="8" />
      ))}
    </svg>
  );
}

function Butter() {
  return (
    <svg className={thumbs.template} viewBox="0 0 12 36" aria-hidden="true">
      <rect className={thumbs.butter} x="0.5" y="0.5" width="11" height="35" />
      {[0, 1, 2].map((index) => (
        <rect key={index} className={thumbs.window} x="1.5" y={2 + index * 10.5} width="9" height="9" />
      ))}
    </svg>
  );
}

const templates: readonly OptionListItem[] = [
  { value: "signature-strip", title: "Signature Strip", meta: "4 photographs", thumbnail: <Strip windows={4} /> },
  { value: "panorama-four", title: "Panorama Four", meta: "4 photographs", thumbnail: <Panorama /> },
  { value: "butter-gingham", title: "Butter Gingham", meta: "3 photographs", thumbnail: <Butter /> },
  { value: "polka-keepsake", title: "Polka Keepsake", meta: "4 photographs", thumbnail: <Strip windows={4} /> },
];

const lookNames = [
  ["monochrome", "Monochrome", "Black and white with deep blacks and strong contrast."],
  ["naturale", "Naturale", "Original colors. No filter."],
  ["soft-monochrome", "Soft Monochrome", "Black and white with softer contrast."],
  ["soft-color", "Soft Color", "Slightly muted colors and softer highlights."],
  ["sepia", "Sepia", "Soft brown tones with faded contrast."],
  ["warm-monochrome", "Warm Monochrome", "Black and white with a light warm tint."],
  ["classic", "Classic", "Warm colors with a golden tint."],
] as const;

const looks: readonly OptionListItem[] = lookNames.map(([value, title]) => ({
  value,
  title,
  thumbnail: <span className={thumbs.look} data-look={value} />,
}));

const describedLooks: readonly OptionListItem[] = lookNames.map(([value, title, description]) => ({
  value,
  title,
  description,
  thumbnail: <span className={thumbs.look} data-look={value} />,
}));

/** Focuses the radio group's tab stop (the chosen value), so the fixture shows where Tab lands. */
function FocusTabStop({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>('input[tabindex="0"]')?.focus();
  }, []);
  return <div ref={ref}>{children}</div>;
}

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "option-list-template-guest",
    title: "Option list, guest Template choice (radio)",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={styles.wide}>
        <OptionList variant="radio" label="Template" hideLabel items={templates} value="signature-strip" onChange={noop} />
      </div>
    ),
  },
  {
    id: "option-list-look-guest",
    title: "Option list, guest Look choice, all seven Looks",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={styles.wide}>
        <OptionList variant="radio" label="Look" hideLabel items={looks} value="naturale" onChange={noop} />
      </div>
    ),
  },
  {
    id: "option-list-guest-focus",
    title: "Option list, guest, keyboard focus on the chosen row",
    surface: "light",
    role: "guest",
    render: () => (
      <div className={styles.wide}>
        <FocusTabStop>
          <OptionList variant="radio" label="Template" hideLabel items={templates} value="panorama-four" onChange={noop} />
        </FocusTabStop>
      </div>
    ),
  },
  {
    id: "option-list-templates-offered",
    title: "Option list, offered Templates with a default (checkbox and radio)",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <OptionList
          variant="checkbox"
          label="Offered Templates"
          items={templates}
          values={["signature-strip", "panorama-four", "polka-keepsake"]}
          onChange={noop}
          defaultChoice={{ value: "signature-strip", onChange: noop }}
          help="Guests choose from the offered Templates; the default is chosen first."
        />
      </div>
    ),
  },
  {
    id: "option-list-looks-offered",
    title: "Option list, offered Looks with Naturale as default",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <OptionList
          variant="checkbox"
          label="Offered Looks"
          items={looks}
          values={["monochrome", "naturale", "sepia", "classic"]}
          onChange={noop}
          defaultChoice={{ value: "naturale", onChange: noop }}
        />
      </div>
    ),
  },
  {
    id: "option-list-error",
    title: "Option list, none offered, error in text",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <OptionList
          variant="checkbox"
          label="Offered Templates"
          items={templates}
          values={[]}
          onChange={noop}
          defaultChoice={{ value: null, onChange: noop }}
          error="Offer at least one Template."
        />
      </div>
    ),
  },
  {
    id: "option-list-checkbox-focus",
    title: "Option list, checkbox variant, keyboard focus",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <FocusFirst>
          <OptionList
            variant="checkbox"
            label="Offered Templates"
            items={templates}
            values={["signature-strip"]}
            onChange={noop}
            defaultChoice={{ value: "signature-strip", onChange: noop }}
          />
        </FocusFirst>
      </div>
    ),
  },
  {
    id: "option-list-descriptions",
    title: "Option list, radio with descriptions and a disabled row",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <OptionList
          variant="radio"
          label="Default Look"
          items={describedLooks.slice(0, 4).map((item) =>
            item.value === "soft-color" ? { ...item, disabled: true, badge: "Not offered" } : item,
          )}
          value="naturale"
          onChange={noop}
        />
      </div>
    ),
  },
  {
    id: "option-list-text-only",
    title: "Option list, text only, longest copy",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.wide}>
        <OptionList
          variant="radio"
          label="Event"
          items={[
            {
              value: "a",
              title: "The Santos and Villanueva wedding reception, Grand Ballroom, Manila Hotel",
              description: "Customer or host: Isabel Santos-Villanueva and family, with the coordinator Ana at registration.",
              meta: "Saturday 17 October 2026 · 128 Guest Cycles",
            },
            { value: "b", title: "Launch night", meta: "Friday 23 October 2026 · No Guest Cycles yet" },
          ]}
          value={null}
          onChange={noop}
        />
      </div>
    ),
  },
];
