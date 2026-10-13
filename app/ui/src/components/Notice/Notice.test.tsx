import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Notice } from "./Notice.tsx";

describe("Notice", () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  it("is a polite live region holding the message", () => {
    const { container } = render(
      <Notice id="notice" className="extra">
        Move cancelled.
      </Notice>,
    );
    expect(screen.getByRole("status")).toHaveTextContent("Move cancelled.");
    expect(container.firstElementChild).toHaveAttribute("id", "notice");
    expect(container.firstElementChild?.className).toContain("extra");
    expect(screen.queryByRole("button")).toBeNull();
  });

  it("never dismisses itself", () => {
    vi.useFakeTimers();
    render(<Notice onDismiss={vi.fn()}>The booth didn&apos;t answer. Try again.</Notice>);
    act(() => {
      vi.advanceTimersByTime(10 * 60 * 1000);
    });
    expect(screen.getByRole("status")).toHaveTextContent("The booth didn't answer. Try again.");
    expect(vi.getTimerCount()).toBe(0);
  });

  it("offers CLOSE outside the live region when dismissible", () => {
    const onDismiss = vi.fn();
    render(<Notice onDismiss={onDismiss}>That didn&apos;t work. Try again.</Notice>);
    const close = screen.getByRole("button", { name: "CLOSE" });
    expect(close).toHaveAttribute("data-fotohavn-action", "text");
    expect(screen.getByRole("status")).not.toContainElement(close);
    close.focus();
    expect(close).toHaveFocus();
    fireEvent.click(close);
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("puts its own action before CLOSE", () => {
    render(
      <Notice onDismiss={vi.fn()} action={<button type="button">TRY AGAIN</button>}>
        Your strip couldn&apos;t be prepared. Try again.
      </Notice>,
    );
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["TRY AGAIN", "CLOSE"]);
  });

  it("keeps an empty live region, with no actions, until a message arrives", () => {
    const { container, rerender } = render(<Notice onDismiss={vi.fn()} />);
    const region = screen.getByRole("status");
    expect(region).toBeEmptyDOMElement();
    expect(container.firstElementChild).toHaveAttribute("data-empty", "true");
    expect(screen.queryByRole("button")).toBeNull();

    rerender(<Notice onDismiss={vi.fn()}>Move cancelled.</Notice>);
    expect(screen.getByRole("status")).toBe(region);
    expect(region).toHaveTextContent("Move cancelled.");
    expect(container.firstElementChild).not.toHaveAttribute("data-empty");

    for (const empty of [null, false, ""]) {
      rerender(<Notice>{empty}</Notice>);
      expect(container.firstElementChild).toHaveAttribute("data-empty", "true");
    }
  });
});
