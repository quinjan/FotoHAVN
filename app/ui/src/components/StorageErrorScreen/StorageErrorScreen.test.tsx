import { act, render, screen } from "@testing-library/react";
import type { ReactElement } from "react";
import { renderToString } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import type { StorageErrorReason } from "../../protocol/generated.ts";
import { StorageErrorScreen, storageErrorCopy } from "./StorageErrorScreen.tsx";

const kinds: readonly StorageErrorReason[] = [
  "diskFull",
  "accessDenied",
  "locked",
  "notLocalDisk",
  "cloudSynced",
  "notWritable",
  "integrityFailed",
  "corrupt",
  "unknown",
];

const dataPath = "C:\\FOTOHAVN\\Data";

async function settle(): Promise<void> {
  await act(async () => {
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
}

/** Renders, then lets the screen's announcement timer fire, as it does a moment after the screen mounts. */
async function renderSettled(ui: ReactElement) {
  const result = render(ui);
  await settle();
  return result;
}

describe("StorageErrorScreen", () => {
  it.each(kinds)("shows the heading, the %s explanation, and the Data folder path", async (kind) => {
    await renderSettled(<StorageErrorScreen kind={kind} dataPath={dataPath} onRetry={vi.fn()} />);
    const heading = screen.getByRole("heading", { level: 1 });
    expect(heading).toHaveTextContent("The Data folder needs attention.");
    expect(heading.querySelector("em")).toHaveTextContent("attention.");
    expect(screen.getAllByText(storageErrorCopy[kind])).toHaveLength(1);
    expect(screen.getByText("Data folder")).toBeVisible();
    expect(screen.getByText(dataPath)).toBeVisible();
    expect(screen.getByRole("button", { name: "TRY AGAIN" })).toHaveAttribute("data-fotohavn-action", "primary");
  });

  it("uses the agreed copy for every kind", () => {
    expect(storageErrorCopy).toEqual({
      diskFull: "The disk is full. Free some space, then try again.",
      accessDenied: "FOTOHAVN can't write to the Data folder. Check its permissions, then try again.",
      locked: "Another program is using a FOTOHAVN file. Close it, then try again.",
      notLocalDisk: "The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk.",
      cloudSynced:
        "The Data folder is inside a cloud-synced folder, which can change files while the booth runs. Move FOTOHAVN outside it.",
      notWritable: "The Data folder can't be written to. Choose a folder FOTOHAVN can write to.",
      integrityFailed:
        "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team.",
      corrupt: "The Event database didn't pass its check. Copy the Data folder somewhere safe and contact the FOTOHAVN team.",
      unknown: "Something went wrong with the Data folder. Try again.",
    });
  });

  it("mounts the problem status empty, then fills it once, so the live region announces it", async () => {
    const html = renderToString(<StorageErrorScreen kind="locked" dataPath={dataPath} onRetry={vi.fn()} />);
    expect(html).not.toContain(storageErrorCopy.locked);
    expect(html).toContain('role="status"');

    const { container } = render(<StorageErrorScreen kind="locked" dataPath={dataPath} onRetry={vi.fn()} />);
    const problem = container.querySelector("[role='status']");
    expect(problem).toBeEmptyDOMElement();
    await settle();
    expect(problem).toHaveTextContent(storageErrorCopy.locked);
    expect(problem?.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
  });

  it("doesn't announce the problem again while retrying or after a rejection", async () => {
    const { rerender } = await renderSettled(<StorageErrorScreen kind="diskFull" dataPath={dataPath} onRetry={vi.fn()} />);
    const problem = screen.getAllByRole("status")[0];
    const changes: string[] = [];
    const observer = new MutationObserver(() => {
      changes.push(problem?.textContent ?? "");
    });
    if (problem !== undefined) {
      observer.observe(problem, { childList: true, subtree: true, characterData: true });
    }
    rerender(<StorageErrorScreen kind="diskFull" dataPath={dataPath} retrying onRetry={vi.fn()} />);
    rerender(<StorageErrorScreen kind="diskFull" dataPath={dataPath} notice="That isn't ready yet." onRetry={vi.fn()} />);
    await settle();
    observer.disconnect();
    expect(changes).toEqual([]);
    expect(problem).toHaveTextContent(storageErrorCopy.diskFull);
  });

  it("sends the retry when TRY AGAIN is activated", async () => {
    const onRetry = vi.fn();
    await renderSettled(<StorageErrorScreen kind="unknown" dataPath={dataPath} onRetry={onRetry} />);
    act(() => {
      screen.getByRole("button", { name: "TRY AGAIN" }).click();
    });
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("is unavailable while trying again, and says so", async () => {
    const onRetry = vi.fn();
    await renderSettled(<StorageErrorScreen kind="unknown" dataPath={dataPath} retrying onRetry={onRetry} />);
    const button = screen.getByRole("button", { name: "TRY AGAIN" });
    expect(button).toHaveAttribute("aria-disabled", "true");
    act(() => {
      button.click();
    });
    expect(onRetry).not.toHaveBeenCalled();
    expect(screen.getByText("Trying again…")).toBeVisible();
  });

  it("shows an outcome line in an alert status when a try didn't work", async () => {
    await renderSettled(
      <StorageErrorScreen kind="unknown" dataPath={dataPath} notice="The booth didn't answer. Try again." onRetry={vi.fn()} />,
    );
    const line = screen.getByText("The booth didn't answer. Try again.");
    expect(line.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
    expect(screen.queryByText("Trying again…")).not.toBeInTheDocument();
  });

  it("cancels the announcement timer when it unmounts first", () => {
    const { unmount } = render(<StorageErrorScreen kind="unknown" dataPath={dataPath} onRetry={vi.fn()} />);
    expect(() => {
      unmount();
    }).not.toThrow();
  });
});
