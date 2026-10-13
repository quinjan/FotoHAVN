import { fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { GuestStrip, OperatorGuestCycle } from "../GuestCycleList/index.ts";
import { GuestCycleDetail } from "./GuestCycleDetail.tsx";

const image = (name: string, width = 1920, height = 1080) => ({ src: `${name}.png`, alt: `${name} alt`, width, height });

const completed: OperatorGuestCycle = {
  id: "c1",
  time: "9:14 PM",
  outcome: "completed",
  templateName: "Signature Strip",
  lookName: "Sepia",
  strip: image("strip", 600, 1800),
  captures: [image("one"), image("two"), image("three"), image("four")],
};

describe("GuestCycleDetail, operator", () => {
  it("shows the Photo Strip as a resting print beside its Captures in a Cream Paper media frame", () => {
    render(<GuestCycleDetail mode="operator" cycle={completed} onOpenFolder={vi.fn()} onDelete={vi.fn()} className="detail" />);
    const section = screen.getByRole("region", { name: "Guest Cycle, 9:14 PM" });
    expect(section.className).toContain("detail");
    const strip = screen.getByRole("img", { name: "strip alt" });
    expect(strip.parentElement).toHaveAttribute("data-shadow", "contact");
    expect(strip.parentElement).not.toHaveAttribute("data-rotated");

    const captures = screen.getByRole("list");
    expect(captures.tagName).toBe("OL");
    const frame = captures.closest("[data-tone='paper']");
    expect(frame).toHaveAttribute("data-count", "4");
    const pair = frame?.parentElement;
    expect(pair?.style.getPropertyValue("--strip-ratio")).toBe(String(600 / 1800));
    expect(pair?.style.getPropertyValue("--capture-columns")).toBe("2");
    expect(pair?.style.getPropertyValue("--capture-rows")).toBe("2");
    expect(pair?.style.getPropertyValue("--capture-ratio")).toBe(String(1920 / 1080));
    expect(within(captures).getAllByRole("img").map((capture) => capture.getAttribute("alt"))).toEqual([
      "one alt",
      "two alt",
      "three alt",
      "four alt",
    ]);
  });

  it("lists the metadata as Meta labels with values", () => {
    render(<GuestCycleDetail mode="operator" cycle={completed} onOpenFolder={vi.fn()} onDelete={vi.fn()} />);
    const terms = screen.getAllByRole("term").map((term) => term.textContent);
    const values = screen.getAllByRole("definition").map((value) => value.textContent);
    expect(terms).toEqual(["Time", "Outcome", "Template", "Look", "Captures"]);
    expect(values).toEqual(["9:14 PM", "Completed", "Signature Strip", "Sepia", "4"]);
  });

  it("opens the folder, deletes, and closes when it can", () => {
    const onOpenFolder = vi.fn();
    const onDelete = vi.fn();
    const onClose = vi.fn();
    const { rerender } = render(<GuestCycleDetail mode="operator" cycle={completed} onOpenFolder={onOpenFolder} onDelete={onDelete} />);
    expect(screen.queryByRole("button", { name: "CLOSE" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "OPEN FOLDER" }));
    fireEvent.click(screen.getByRole("button", { name: "DELETE" }));
    expect(onOpenFolder).toHaveBeenCalledOnce();
    expect(onDelete).toHaveBeenCalledOnce();

    rerender(<GuestCycleDetail mode="operator" cycle={completed} onOpenFolder={onOpenFolder} onDelete={onDelete} onClose={onClose} />);
    fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it("says No Photo Strip for a Guest Cycle without one, and No Captures when none were kept", () => {
    const startedOver: OperatorGuestCycle = {
      ...completed,
      outcome: "started-over",
      strip: null,
      captures: [image("only")],
    };
    const { rerender } = render(<GuestCycleDetail mode="operator" cycle={startedOver} onOpenFolder={vi.fn()} onDelete={vi.fn()} />);
    expect(screen.getByText("No Photo Strip")).toBeInTheDocument();
    const frame = screen.getByRole("list").closest("[data-tone='paper']");
    expect(frame).toHaveAttribute("data-count", "1");
    expect(frame?.parentElement?.style.getPropertyValue("--strip-ratio")).toBe(String(1 / 3));
    expect(frame?.parentElement?.style.getPropertyValue("--capture-columns")).toBe("1");
    expect(frame?.parentElement?.style.getPropertyValue("--capture-rows")).toBe("1");
    expect(screen.getAllByRole("definition")[1]).toHaveTextContent("Started over");

    const nothingKept: OperatorGuestCycle = {
      id: "c0",
      time: "8:42 PM",
      outcome: "app-closed",
      templateName: "Signature Strip",
      strip: null,
      captures: [],
    };
    rerender(<GuestCycleDetail mode="operator" cycle={nothingKept} onOpenFolder={vi.fn()} onDelete={vi.fn()} />);
    expect(screen.getByText("No Captures")).toBeInTheDocument();
    expect(screen.queryByRole("list")).toBeNull();
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual(["Time", "Outcome", "Template", "Captures"]);
    expect(screen.getAllByRole("definition").at(-1)).toHaveTextContent("0");
  });
});

describe("GuestCycleDetail, guest", () => {
  const strip: GuestStrip = { id: "s1", ...image("Photo Strip from this Event", 600, 1800) };

  it("lifts the Photo Strip beside its arranged Captures, with CLOSE and nothing else", () => {
    const onClose = vi.fn();
    const { container } = render(
      <GuestCycleDetail mode="guest" strip={strip} captures={[image("one"), image("two")]} onClose={onClose} className="view" />,
    );
    const section = screen.getByRole("region", { name: "Photo Strip from this Event alt" });
    expect(section).toHaveAttribute("data-tone", "light");
    expect(section.className).toContain("view");
    const print = screen.getByRole("img", { name: "Photo Strip from this Event alt" });
    expect(print.parentElement).toHaveAttribute("data-shadow", "lifted");
    expect(print.parentElement).not.toHaveAttribute("data-rotated");
    expect(within(screen.getByRole("list")).getAllByRole("img")).toHaveLength(2);

    expect(screen.getAllByRole("button")).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(container.querySelector("dl")).toBeNull();
    expect(screen.queryByText(/Template|Look|Completed|Captures/)).toBeNull();
  });

  it("turns a landscape Photo Strip a quarter counterclockwise, as guest previews do", () => {
    const { container } = render(
      <GuestCycleDetail mode="guest" strip={{ ...strip, width: 1800, height: 600 }} captures={[]} onClose={vi.fn()} />,
    );
    expect(screen.getByRole("img").parentElement).toHaveAttribute("data-rotated", "true");
    const frame = container.querySelector("[data-tone='paper']");
    expect(frame).toHaveAttribute("data-count", "0");
    expect(frame?.parentElement?.style.getPropertyValue("--strip-ratio")).toBe(String(600 / 1800));
    expect(frame?.parentElement?.style.getPropertyValue("--capture-ratio")).toBe(String(16 / 9));
    expect(screen.queryByRole("list")).toBeNull();
    expect(screen.queryByText("No Captures")).toBeNull();
  });
});
