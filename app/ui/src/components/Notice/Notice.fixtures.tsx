import type { ComponentFixture } from "../fixture.ts";
import { FocusFirst, noop } from "../shared/fixtureHelpers.tsx";
import { TextLink } from "../TextLink/index.ts";
import { Notice } from "./Notice.tsx";

export const fixtures: readonly ComponentFixture[] = [
  {
    id: "notice-operator",
    title: "Notice, operator",
    surface: "light",
    role: "operator",
    render: () => <Notice>End the running Event to start another.</Notice>,
  },
  {
    id: "notice-dismissible",
    title: "Notice, operator, with CLOSE",
    surface: "light",
    role: "operator",
    render: () => <Notice onDismiss={noop}>The booth didn&apos;t answer. Try again.</Notice>,
  },
  {
    id: "notice-dismissible-focus",
    title: "Notice, keyboard focus on CLOSE",
    surface: "light",
    role: "operator",
    render: () => (
      <FocusFirst>
        <Notice onDismiss={noop}>That didn&apos;t work. Try again.</Notice>
      </FocusFirst>
    ),
  },
  {
    id: "notice-guest",
    title: "Notice, guest",
    surface: "light",
    role: "guest",
    render: () => <Notice>Move cancelled.</Notice>,
  },
  {
    id: "notice-guest-action",
    title: "Notice, guest, with an action and CLOSE",
    surface: "light",
    role: "guest",
    render: () => (
      <Notice
        onDismiss={noop}
        action={
          <TextLink size="meta" onClick={noop}>
            TRY AGAIN
          </TextLink>
        }
      >
        Your strip couldn&apos;t be prepared. Try again.
      </Notice>
    ),
  },
  {
    id: "notice-long",
    title: "Notice, longest realistic copy",
    surface: "light",
    role: "operator",
    render: () => (
      <Notice onDismiss={noop}>
        The Guest Screen will cover this screen. Use the hold and PIN to return. The chosen guest display isn&apos;t
        connected, so the Event can&apos;t start until it is.
      </Notice>
    ),
  },
  {
    id: "notice-dark",
    title: "Notice on dark, with CLOSE",
    surface: "dark",
    role: "guest",
    render: () => <Notice onDismiss={noop}>Move cancelled.</Notice>,
  },
];
