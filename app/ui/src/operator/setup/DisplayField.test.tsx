import { fireEvent, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import type { EventSetupGuestDisplayView, GuestDisplayOption } from "../../protocol/generated.ts";
import { renderOperator } from "../../test/operatorHarness.tsx";
import { DisplayField, displayPosition } from "./DisplayField.tsx";

const display = (overrides: Partial<GuestDisplayOption>): GuestDisplayOption => ({
  id: "d",
  name: "Display",
  x: 0,
  y: 0,
  width: 1920,
  height: 1080,
  number: 1,
  isOperatorScreen: false,
  ...overrides,
});

const operator = display({ id: "operator", name: "Built-in display", isOperatorScreen: true });

describe("displayPosition", () => {
  it("numbers the display as Identify displays does", () => {
    expect(displayPosition(operator, operator)).toBe("Display 1");
    expect(displayPosition(display({ number: 3 }), undefined)).toBe("Display 3");
  });

  it.each([
    [{ x: 1920 }, "Display 2, right of the Operator Screen"],
    [{ x: -1920 }, "Display 2, left of the Operator Screen"],
    [{ y: -1080 }, "Display 2, above the Operator Screen"],
    [{ y: 1080 }, "Display 2, below the Operator Screen"],
    [{ x: 1920, y: 200 }, "Display 2, right of the Operator Screen"],
  ] as const)("places a display at %o against the Operator Screen", (place, text) => {
    expect(displayPosition(display({ id: "other", number: 2, ...place }), operator)).toBe(text);
  });
});

function view(overrides: Partial<EventSetupGuestDisplayView> = {}): EventSetupGuestDisplayView {
  return {
    displays: [operator, display({ id: "guest", name: "DELL P2419H", x: 1920, number: 2 })],
    selectedId: null,
    selectedName: null,
    state: "none",
    coversOperatorScreen: false,
    ...overrides,
  };
}

describe("DisplayField", () => {
  it("lists the displays with their numbers, sizes, and the Operator Screen marker", () => {
    renderOperator(<DisplayField guestDisplay={view()} error={undefined} identifying={false} onSelect={vi.fn()} onIdentify={vi.fn()} />);
    const guest = screen.getByRole("radio", { name: "DELL P2419H" });
    expect(guest).toHaveAccessibleDescription(expect.stringContaining("1920 × 1080 · Display 2, right of the Operator Screen"));
    expect(screen.getByRole("radio", { name: "Built-in display" })).toHaveAccessibleDescription(expect.stringContaining("Operator Screen"));
  });

  it("chooses a display and identifies them", () => {
    const onSelect = vi.fn();
    const onIdentify = vi.fn();
    renderOperator(<DisplayField guestDisplay={view()} error={undefined} identifying={false} onSelect={onSelect} onIdentify={onIdentify} />);
    fireEvent.click(screen.getByRole("radio", { name: "DELL P2419H" }));
    fireEvent.click(screen.getByRole("button", { name: "IDENTIFY DISPLAYS" }));
    expect(onSelect).toHaveBeenCalledExactlyOnceWith("guest");
    expect(onIdentify).toHaveBeenCalledOnce();
  });

  it("can't identify again while the numbers are showing", () => {
    const onIdentify = vi.fn();
    renderOperator(<DisplayField guestDisplay={view()} error={undefined} identifying onSelect={vi.fn()} onIdentify={onIdentify} />);
    const identify = screen.getByRole("button", { name: "IDENTIFY DISPLAYS" });
    expect(identify).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(identify);
    expect(onIdentify).not.toHaveBeenCalled();
  });

  it("keeps a bound display that isn't connected, says so, and never swaps it", () => {
    renderOperator(
      <DisplayField
        guestDisplay={view({ displays: [operator], selectedId: "guest", selectedName: "DELL P2419H", state: "missing" })}
        error={undefined}
        identifying={false}
        onSelect={vi.fn()}
        onIdentify={vi.fn()}
      />,
    );
    expect(screen.getByRole("radio", { name: "DELL P2419H" })).toBeChecked();
    expect(screen.getByRole("radio", { name: "DELL P2419H" })).toBeDisabled();
    expect(screen.getByText("The chosen guest display isn't connected.")).toBeVisible();
  });

  it("warns that the Guest Screen will cover the Operator Screen when the chosen display is its own", () => {
    renderOperator(
      <DisplayField
        guestDisplay={view({ displays: [operator], selectedId: "operator", selectedName: "Built-in display", state: "connected", coversOperatorScreen: true })}
        error={undefined}
        identifying={false}
        onSelect={vi.fn()}
        onIdentify={vi.fn()}
      />,
    );
    expect(screen.getAllByText("The Guest Screen will cover this screen. Use the hold and PIN to return.")).toHaveLength(1);
  });

  it("gives the same warning when the brain says the chosen display covers the Operator Screen, whichever it is", () => {
    renderOperator(
      <DisplayField
        guestDisplay={view({ selectedId: "guest", selectedName: "DELL P2419H", state: "connected", coversOperatorScreen: true })}
        error={undefined}
        identifying={false}
        onSelect={vi.fn()}
        onIdentify={vi.fn()}
      />,
    );
    expect(screen.getAllByText("The Guest Screen will cover this screen. Use the hold and PIN to return.")).toHaveLength(1);
  });

  it("shows its error in words", () => {
    renderOperator(<DisplayField guestDisplay={view()} error="Choose the guest display." identifying={false} onSelect={vi.fn()} onIdentify={vi.fn()} />);
    expect(screen.getByText("Choose the guest display.")).toBeVisible();
  });
});
