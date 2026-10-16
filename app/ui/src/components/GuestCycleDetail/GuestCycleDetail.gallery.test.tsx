import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { createRef } from "react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { OperatorGuestCycle } from "../GuestCycleList/index.ts";
import { GuestCycleDetail } from "./GuestCycleDetail.tsx";

const image = (name: string, width = 1920, height = 1080) => ({ src: `${name}.png`, alt: `${name} alt`, width, height });

const completed: OperatorGuestCycle = {
  id: "c1",
  time: "13 Oct, 8:42 PM",
  outcome: "completed",
  templateName: "Signature Strip",
  lookName: "Sepia",
  strip: image("strip", 600, 1800),
  captures: [image("one"), image("two")],
};

afterEach(cleanup);

const bare: OperatorGuestCycle = { id: "c1", time: "13 Oct, 8:42 PM", outcome: "started-over", strip: null, captures: [image("one"), image("two")] };

describe("GuestCycleDetail, operator: more states", () => {
  it("leaves out the Template and Look rows of a Guest Cycle that recorded none", () => {
    render(<GuestCycleDetail mode="operator" cycle={bare} onOpenFolder={vi.fn()} onDelete={vi.fn()} />);
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual(["Time", "Outcome", "Captures"]);
    expect(screen.getByText("No Photo Strip")).toBeInTheDocument();
  });

  it("says once that the files are gone, with an empty mat and no Captures fact", () => {
    const { container } = render(
      <GuestCycleDetail
        mode="operator"
        cycle={{ ...completed, strip: null, captures: [], unavailable: true, outcome: "app-closed" }}
        onOpenFolder={vi.fn()}
        onDelete={vi.fn()}
      />,
    );
    expect(screen.getAllByText("These photographs can't be found.")).toHaveLength(1);
    expect(screen.queryByText("No Photo Strip")).toBeNull();
    expect(screen.queryByText("No Captures")).toBeNull();
    expect(screen.getAllByRole("term").map((term) => term.textContent)).toEqual(["Time", "Outcome", "Template", "Look"]);
    expect(container.querySelector("[aria-hidden='true'][data-tone='paper']")).not.toBeNull();
  });

  it("keeps DELETE and OPEN FOLDER unavailable, without sending, while the brain says so", () => {
    const onOpenFolder = vi.fn();
    const onDelete = vi.fn();
    render(<GuestCycleDetail mode="operator" cycle={completed} canDelete={false} canOpenFolder={false} onOpenFolder={onOpenFolder} onDelete={onDelete} />);
    const remove = screen.getByRole("button", { name: "DELETE" });
    const folder = screen.getByRole("button", { name: "OPEN FOLDER" });
    expect(remove).toHaveAttribute("aria-disabled", "true");
    expect(folder).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(remove);
    fireEvent.click(folder);
    expect(onDelete).not.toHaveBeenCalled();
    expect(onOpenFolder).not.toHaveBeenCalled();
  });

  it("tabs through OPEN FOLDER and DELETE", () => {
    render(<GuestCycleDetail mode="operator" cycle={completed} onOpenFolder={vi.fn()} onDelete={vi.fn()} />);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["OPEN FOLDER", "DELETE"]);
  });
});

describe("GuestCycleDetail, guest: what a guest sees", () => {
  const strip = image("guest-strip", 600, 1800);
  const captures = [image("capture-a"), image("capture-b"), image("capture-c")];

  it("renders the strip, its Captures, and CLOSE only: no time, name, outcome, count, or action", () => {
    const { container } = render(<GuestCycleDetail mode="guest" strip={strip} captures={captures} onClose={vi.fn()} />);
    expect(container.textContent).toBe("CLOSE");
    expect(screen.getAllByRole("img").map((picture) => picture.getAttribute("alt"))).toEqual(["guest-strip alt", "capture-a alt", "capture-b alt", "capture-c alt"]);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["CLOSE"]);
    for (const absent of ["Time", "Outcome", "Template", "Look", "Captures", "DELETE", "OPEN FOLDER", "Completed", "BACK TO START"]) {
      expect(screen.queryByText(absent)).toBeNull();
    }
    expect(container.querySelector("dl, dt, dd")).toBeNull();
  });

  it("offers BACK TO START beside CLOSE when the view asks, in that order, each sending its own command", () => {
    const onClose = vi.fn();
    const onBack = vi.fn();
    const closeRef = createRef<HTMLButtonElement>();
    render(<GuestCycleDetail mode="guest" strip={strip} captures={captures} onClose={onClose} onBack={onBack} closeRef={closeRef} />);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["CLOSE", "BACK TO START"]);
    expect(closeRef.current).toBe(screen.getByRole("button", { name: "CLOSE" }));
    fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
    fireEvent.click(screen.getByRole("button", { name: "BACK TO START" }));
    expect(onClose).toHaveBeenCalledOnce();
    expect(onBack).toHaveBeenCalledOnce();
  });

  it("lifts the strip and turns a landscape one a quarter counterclockwise", () => {
    const { rerender } = render(<GuestCycleDetail mode="guest" strip={strip} captures={captures} onClose={vi.fn()} />);
    expect(screen.getByAltText("guest-strip alt").parentElement).toHaveAttribute("data-shadow", "lifted");
    expect(screen.getByAltText("guest-strip alt").parentElement).not.toHaveAttribute("data-rotated");
    rerender(<GuestCycleDetail mode="guest" strip={image("wide", 1800, 600)} captures={captures} onClose={vi.fn()} />);
    expect(screen.getByAltText("wide alt").parentElement).toHaveAttribute("data-rotated", "true");
  });
});
