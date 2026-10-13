import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import styles from "../shared/fixtureLayout.module.css";
import { TextLink } from "./TextLink.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "text-link-guest",
    title: "Text link, guest, with arrow",
    surface: "light",
    role: "guest",
    render: () => (
      <TextLink arrow="right" onClick={noop}>
        SEE THE GOOD COMPANY
      </TextLink>
    ),
  },
  {
    id: "text-link-back",
    title: "Text link, guest, back",
    surface: "light",
    role: "guest",
    render: () => (
      <TextLink arrow="left" onClick={noop}>
        BACK TO START
      </TextLink>
    ),
  },
  {
    id: "text-link-row-actions",
    title: "Text links, operator row actions in the Meta role",
    surface: "light",
    role: "operator",
    render: () => (
      <div className={styles.row}>
        <TextLink size="meta" onClick={noop}>
          START
        </TextLink>
        <TextLink size="meta" onClick={noop}>
          GALLERY
        </TextLink>
        <TextLink size="meta" onClick={noop}>
          EDIT
        </TextLink>
        <TextLink size="meta" onClick={noop}>
          OPEN FOLDER
        </TextLink>
        <TextLink size="meta" onClick={noop}>
          DELETE
        </TextLink>
      </div>
    ),
  },
  {
    id: "text-link-disabled",
    title: "Text link, disabled",
    surface: "light",
    role: "operator",
    render: () => (
      <TextLink size="meta" disabled onClick={noop}>
        START
      </TextLink>
    ),
  },
  {
    id: "text-link-focus",
    title: "Text link, keyboard focus",
    surface: "light",
    role: "guest",
    render: () => (
      <FocusFirst>
        <TextLink onClick={noop}>CLOSE</TextLink>
      </FocusFirst>
    ),
  },
  {
    id: "text-link-inverted",
    title: "Text link, inverted on dark",
    surface: "dark",
    role: "guest",
    render: () => (
      <TextLink arrow="up-right" onClick={noop}>
        ASK ABOUT YOUR DATE
      </TextLink>
    ),
  },
  {
    id: "text-link-anchor",
    title: "Text link as a link",
    surface: "light",
    role: "operator",
    render: () => (
      <TextLink href="?gallery" arrow="right">
        ALL FIXTURES
      </TextLink>
    ),
  },
];
