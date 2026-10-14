import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CompositionControls, type CompositionControlsProps } from "./CompositionControls.tsx";

function renderControls(overrides: Partial<CompositionControlsProps> = {}) {
  const props = {
    templateId: "signature-strip",
    lookId: "naturale-v1",
    onTemplateChange: vi.fn(),
    onLookChange: vi.fn(),
    onCompose: vi.fn(),
    onBlankPreview: vi.fn(),
    ...overrides,
  };
  render(<CompositionControls {...props} />);
  return props;
}

describe("CompositionControls", () => {
  it("offers every Template and Look by name, with the chosen ones selected", () => {
    renderControls({ templateId: "panorama-four", lookId: "sepia-v1" });
    const template = screen.getByRole("combobox", { name: "Template" });
    const look = screen.getByRole("combobox", { name: "Look" });
    expect(template).toHaveValue("panorama-four");
    expect(look).toHaveValue("sepia-v1");
    expect([...template.querySelectorAll("option")].map((option) => option.textContent)).toEqual([
      "Signature Strip",
      "Panorama Four",
      "Butter Gingham",
      "Polka Keepsake",
    ]);
    expect([...look.querySelectorAll("option")].map((option) => option.textContent)).toEqual([
      "Monochrome",
      "Naturale",
      "Soft Monochrome",
      "Soft Color",
      "Sepia",
      "Warm Monochrome",
      "Classic",
    ]);
  });

  it("reports the choices and the two actions, none of them primary", () => {
    const wired = renderControls();

    fireEvent.change(screen.getByRole("combobox", { name: "Template" }), { target: { value: "butter-gingham" } });
    fireEvent.change(screen.getByRole("combobox", { name: "Look" }), { target: { value: "classic-v1" } });
    fireEvent.click(screen.getByRole("button", { name: "COMPOSE" }));
    fireEvent.click(screen.getByRole("button", { name: "BLANK PREVIEW" }));

    expect(wired.onTemplateChange).toHaveBeenCalledWith("butter-gingham");
    expect(wired.onLookChange).toHaveBeenCalledWith("classic-v1");
    expect(wired.onCompose).toHaveBeenCalledOnce();
    expect(wired.onBlankPreview).toHaveBeenCalledOnce();
    expect(document.querySelectorAll("[data-fotohavn-action='primary']")).toHaveLength(0);
  });

  it("can't compose while a composition is in flight", () => {
    const wired = renderControls({ composing: true });

    fireEvent.click(screen.getByRole("button", { name: "COMPOSE" }));
    fireEvent.click(screen.getByRole("button", { name: "BLANK PREVIEW" }));

    expect(screen.getByRole("button", { name: "COMPOSE" })).toHaveAttribute("aria-disabled", "true");
    expect(wired.onCompose).not.toHaveBeenCalled();
    expect(wired.onBlankPreview).not.toHaveBeenCalled();
  });
});
