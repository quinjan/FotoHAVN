import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { draftIdleMs, useDraftText, type DraftTextOptions } from "./useDraftText.ts";

function Probe({ value, commit, options }: { readonly value: string; readonly commit: (text: string) => void; readonly options?: DraftTextOptions }) {
  const draft = useDraftText(value, commit, options);
  return (
    <input
      aria-label="field"
      value={draft.text}
      onChange={(event) => {
        draft.change(event.target.value);
      }}
      onBlur={draft.blur}
    />
  );
}

const field = () => screen.getByLabelText("field");
const type = (text: string) => {
  fireEvent.change(field(), { target: { value: text } });
};

describe("useDraftText", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("starts from the brain's value", () => {
    render(<Probe value="Harbour gala" commit={vi.fn()} />);
    expect(field()).toHaveValue("Harbour gala");
  });

  it("sends a change after the idle time, once, with everything typed so far", () => {
    const commit = vi.fn();
    render(<Probe value="" commit={commit} />);
    type("H");
    act(() => {
      vi.advanceTimersByTime(draftIdleMs - 1);
    });
    type("Ha");
    act(() => {
      vi.advanceTimersByTime(draftIdleMs - 1);
    });
    expect(commit).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(commit).toHaveBeenCalledExactlyOnceWith("Ha");
    expect(field()).toHaveValue("Ha");
  });

  it("sends a pending change at once on blur, and not again when the idle time would have passed", () => {
    const commit = vi.fn();
    render(<Probe value="" commit={commit} />);
    type("Harbour");
    fireEvent.blur(field());
    expect(commit).toHaveBeenCalledExactlyOnceWith("Harbour");
    act(() => {
      vi.advanceTimersByTime(draftIdleMs * 2);
    });
    expect(commit).toHaveBeenCalledOnce();
  });

  it("sends nothing on blur when nothing was typed, or when the change was already sent", () => {
    const commit = vi.fn();
    render(<Probe value="a" commit={commit} />);
    fireEvent.blur(field());
    type("ab");
    act(() => {
      vi.advanceTimersByTime(draftIdleMs);
    });
    fireEvent.blur(field());
    expect(commit).toHaveBeenCalledExactlyOnceWith("ab");
  });

  it("sends at once, with no idle time, when asked to", () => {
    const commit = vi.fn();
    render(<Probe value="" commit={commit} options={{ delayMs: 0 }} />);
    type("2026-11-14");
    expect(commit).toHaveBeenCalledExactlyOnceWith("2026-11-14");
  });

  it("doesn't let the brain's echo of an earlier keystroke undo newer typing", () => {
    const { rerender } = render(<Probe value="" commit={vi.fn()} />);
    type("a");
    type("ab");
    rerender(<Probe value="a" commit={vi.fn()} />);
    expect(field()).toHaveValue("ab");
    rerender(<Probe value="ab" commit={vi.fn()} />);
    expect(field()).toHaveValue("ab");
  });

  it("shows a value the brain changed while the field isn't being edited", () => {
    const { rerender } = render(<Probe value="Harbour gala" commit={vi.fn()} />);
    rerender(<Probe value="" commit={vi.fn()} />);
    expect(field()).toHaveValue("");
    rerender(<Probe value="Another Event" commit={vi.fn()} />);
    expect(field()).toHaveValue("Another Event");
  });

  it("takes the brain's value again after the field is left", () => {
    const { rerender } = render(<Probe value="a" commit={vi.fn()} />);
    type("ab");
    fireEvent.blur(field());
    rerender(<Probe value="ab" commit={vi.fn()} />);
    rerender(<Probe value="Reset" commit={vi.fn()} />);
    expect(field()).toHaveValue("Reset");
  });

  describe("text that can't be sent", () => {
    const options: DraftTextOptions = { isValid: (text) => /^\d+$/.test(text) };

    it("isn't sent while typing, and reverts to the brain's value on blur", () => {
      const commit = vi.fn();
      render(<Probe value="30" commit={commit} options={options} />);
      type("");
      act(() => {
        vi.advanceTimersByTime(draftIdleMs);
      });
      expect(commit).not.toHaveBeenCalled();
      expect(field()).toHaveValue("");
      fireEvent.blur(field());
      expect(commit).not.toHaveBeenCalled();
      expect(field()).toHaveValue("30");
    });

    it("is sent once it can be", () => {
      const commit = vi.fn();
      render(<Probe value="30" commit={commit} options={options} />);
      type("");
      type("4");
      fireEvent.blur(field());
      expect(commit).toHaveBeenCalledExactlyOnceWith("4");
    });
  });

  it("uses the latest commit callback", () => {
    const first = vi.fn();
    const second = vi.fn();
    const { rerender } = render(<Probe value="" commit={first} />);
    type("a");
    rerender(<Probe value="" commit={second} />);
    act(() => {
      vi.advanceTimersByTime(draftIdleMs);
    });
    expect(first).not.toHaveBeenCalled();
    expect(second).toHaveBeenCalledExactlyOnceWith("a");
  });

  it("drops a pending change when the field goes away", () => {
    const commit = vi.fn();
    const { unmount } = render(<Probe value="" commit={commit} />);
    type("a");
    unmount();
    act(() => {
      vi.advanceTimersByTime(draftIdleMs * 2);
    });
    expect(commit).not.toHaveBeenCalled();
  });
});
