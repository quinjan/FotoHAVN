import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { PrintObject } from "./PrintObject.tsx";

function print(image: HTMLElement): HTMLElement {
  const element = image.parentElement;
  if (element === null) {
    throw new Error("The image has no print around it.");
  }
  return element;
}

describe("PrintObject", () => {
  it("shows the image at its native proportion on a resting print, fitted to its parent", () => {
    const { container } = render(<PrintObject src="strip.png" alt="Photo Strip from this Event" width={600} height={1800} className="slot" />);
    const image = screen.getByRole("img", { name: "Photo Strip from this Event" });
    expect(image).toHaveAttribute("src", "strip.png");
    expect(image).toHaveAttribute("width", "600");
    expect(image).toHaveAttribute("height", "1800");
    expect(image).toHaveAttribute("draggable", "false");

    const paper = print(image);
    expect(paper).toHaveAttribute("data-fit", "contain");
    expect(paper).toHaveAttribute("data-shadow", "contact");
    expect(paper).not.toHaveAttribute("data-rotated");
    expect(paper.style.getPropertyValue("--print-ratio")).toBe(String(600 / 1800));
    expect(paper.style.getPropertyValue("--print-display-width")).toBe("600");
    expect(paper.className).not.toContain("slot");
    // `contain` wraps the print in a box that fills the parent; the layout class goes on that box.
    expect(container.firstElementChild).toBe(paper.parentElement);
    expect(container.firstElementChild?.className).toContain("slot");
  });

  it("takes the print shadow when lifted", () => {
    render(<PrintObject src="strip.png" alt="Strip" width={600} height={1800} shadow="lifted" />);
    expect(print(screen.getByRole("img"))).toHaveAttribute("data-shadow", "lifted");
  });

  it("lays out a quarter-turned print in the turned box", () => {
    render(<PrintObject src="panorama.png" alt="Panorama" width={1800} height={600} rotated />);
    const image = screen.getByRole("img", { name: "Panorama" });
    const paper = print(image);
    expect(paper).toHaveAttribute("data-rotated", "true");
    expect(paper.style.getPropertyValue("--print-ratio")).toBe(String(600 / 1800));
    expect(paper.style.getPropertyValue("--print-display-width")).toBe("600");
    // The file keeps its native orientation; only the print turns.
    expect(image).toHaveAttribute("width", "1800");
    expect(image).toHaveAttribute("height", "600");
  });

  it("shows a blank print of the same shape while there is no image yet", () => {
    const { container } = render(<PrintObject src={null} alt="Not shown" width={1800} height={600} rotated />);
    expect(screen.queryByRole("img")).toBeNull();
    const blank = container.querySelector("[data-blank]");
    expect(blank).toHaveAttribute("aria-hidden", "true");
    const paper = blank?.parentElement;
    expect(paper).toHaveAttribute("data-rotated", "true");
    expect(paper?.style.getPropertyValue("--print-ratio")).toBe(String(600 / 1800));
  });

  it("sizes by native pixels at a common scale, with no wrapping box", () => {
    const { container } = render(
      <PrintObject src="panorama.png" alt="Panorama" width={1800} height={600} fit="scale" className="on-table" />,
    );
    const paper = print(screen.getByRole("img"));
    expect(container.firstElementChild).toBe(paper);
    expect(paper).toHaveAttribute("data-fit", "scale");
    expect(paper.className).toContain("on-table");
    expect(paper.style.getPropertyValue("--print-ratio")).toBe("3");
    expect(paper.style.getPropertyValue("--print-display-width")).toBe("1800");
  });
});
