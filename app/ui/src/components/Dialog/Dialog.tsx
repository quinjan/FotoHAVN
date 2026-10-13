import { useEffect, useId, useRef, type ReactNode, type RefObject } from "react";
import { cx } from "../shared/cx.ts";
import styles from "./Dialog.module.css";

const focusableSelector = [
  "button:not([disabled])",
  "a[href]",
  "input:not([disabled])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  '[tabindex]:not([tabindex="-1"])',
].join(", ");

function focusableIn(container: HTMLElement): HTMLElement[] {
  return [...container.querySelectorAll<HTMLElement>(focusableSelector)];
}

/** Escape cancels; Tab and Shift+Tab wrap between the panel's first and last controls. */
function handleKeyDown(event: KeyboardEvent, panel: HTMLElement, onCancel: () => void): void {
  if (event.key === "Escape") {
    event.preventDefault();
    event.stopPropagation();
    onCancel();
    return;
  }
  if (event.key !== "Tab") {
    return;
  }
  const focusable = focusableIn(panel);
  const first = focusable[0];
  const last = focusable.at(-1);
  if (first === undefined || last === undefined) {
    event.preventDefault();
    panel.focus();
    return;
  }
  const active = document.activeElement;
  if (event.shiftKey && (active === first || active === panel)) {
    event.preventDefault();
    last.focus();
  } else if (!event.shiftKey && active === last) {
    event.preventDefault();
    first.focus();
  }
}

export interface DialogProps {
  /** The sentence-case title, e.g. "Start over?". It labels the dialog. */
  readonly title: string;
  /** The short line under the title, e.g. "Your photographs stay with the Event, but no strip will be made." It describes the dialog. */
  readonly description?: string;
  /** Anything between the description and the actions, such as the PIN pad. */
  readonly children?: ReactNode;
  /** The dialog's actions: at most one PrimaryAction, with a SecondaryAction or TextLink to cancel. */
  readonly actions?: ReactNode;
  /** Escape, or the cancel action the caller wires. The parent unmounts the dialog to close it. */
  readonly onCancel: () => void;
  /** `alertdialog` for a confirmation that interrupts (ConfirmDialog); `dialog` (default) otherwise. */
  readonly role?: "dialog" | "alertdialog";
  /** The element to focus when the dialog opens. Default: the first focusable element, or the panel itself. */
  readonly initialFocus?: RefObject<HTMLElement | null>;
  /** `wide` gives the panel room for content beside the text, such as the PIN pad. Default `narrow`. */
  readonly size?: "narrow" | "wide";
  readonly className?: string;
}

/**
 * A modal dialog (app/DESIGN.md, Dialogs): an Off-white panel with an 8 radius and a Hairline border over an Ebony
 * scrim at about 32%, above every view but the exit hold (`--app-z-dialog`). Use it for the Start Over confirmation,
 * the Operator PIN, and operator confirmations, on either screen.
 *
 * Render it while it is open and unmount it to close. It focuses `initialFocus` (or its first control) on open, keeps
 * Tab and Shift+Tab inside, cancels on Escape, and returns focus to the element that opened it when it closes. It
 * covers the viewport with `position: fixed`, so render it at the screen's root.
 */
export function Dialog({
  title,
  description,
  children,
  actions,
  onCancel,
  role = "dialog",
  initialFocus,
  size = "narrow",
  className,
}: DialogProps) {
  const panelRef = useRef<HTMLDivElement>(null);
  const initialFocusRef = useRef(initialFocus);
  const onCancelRef = useRef(onCancel);
  const titleId = useId();
  const descriptionId = useId();

  useEffect(() => {
    onCancelRef.current = onCancel;
  });

  useEffect(() => {
    const opener = document.activeElement;
    const panel = panelRef.current;
    if (panel === null) {
      return undefined;
    }
    (initialFocusRef.current?.current ?? focusableIn(panel)[0] ?? panel).focus();
    const panelElement = panel;
    function onKeyDown(event: KeyboardEvent): void {
      handleKeyDown(event, panelElement, onCancelRef.current);
    }
    panel.addEventListener("keydown", onKeyDown);
    return () => {
      panel.removeEventListener("keydown", onKeyDown);
      if (opener instanceof HTMLElement && opener.isConnected) {
        opener.focus();
      }
    };
  }, []);

  return (
    <div className={styles.scrim}>
      <div
        ref={panelRef}
        className={cx(styles.panel, className)}
        role={role}
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={description === undefined ? undefined : descriptionId}
        tabIndex={-1}
        data-tone="light"
        data-size={size}
      >
        <h2 id={titleId} className={styles.title}>
          {title}
        </h2>
        {description !== undefined && (
          <p id={descriptionId} className={styles.description}>
            {description}
          </p>
        )}
        {children !== undefined && <div className={styles.body}>{children}</div>}
        {actions !== undefined && <div className={styles.actions}>{actions}</div>}
      </div>
    </div>
  );
}
