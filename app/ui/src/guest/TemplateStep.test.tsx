import { fireEvent, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { names, renderTemplate, stubClient, templateOf } from "./PhotographsTestKit.tsx";

describe("TemplateStep", () => {
  it("shows the heading, the supporting line, and every offered Template with its description", () => {
    renderTemplate(templateOf("guest-ui-template-signature"), stubClient());

    expect(screen.getByRole("heading", { level: 1, name: "Make a little Moment" })).toBeVisible();
    expect(screen.getByText("Choose your template.")).toBeVisible();
    const list = screen.getByRole("group", { name: "Template" });
    expect(within(list).getAllByRole("radio")).toHaveLength(4);
    expect(within(list).getByText("Four edge-to-edge photographs with a signature across the middle.")).toBeVisible();
    expect(within(list).getByRole("radio", { name: /Signature Strip/ })).toBeChecked();
    expect(within(list).getByRole("radio", { name: /Panorama Four/ })).not.toBeChecked();
  });

  it("asks the brain for the Template the guest picks, and leaves the choice to the brain", () => {
    const stub = stubClient();
    renderTemplate(templateOf("guest-ui-template-signature"), stub);

    fireEvent.click(screen.getByRole("radio", { name: /Panorama Four/ }));

    expect(stub.sent).toEqual([{ name: "SelectTemplate", payload: { templateId: "panorama-four" } }]);
    expect(screen.getByRole("radio", { name: /Signature Strip/ })).toBeChecked();
  });

  it("continues to Photographs with one GoToStep", () => {
    const stub = stubClient();
    renderTemplate(templateOf("guest-ui-template-signature"), stub);

    fireEvent.click(screen.getByRole("button", { name: "CONTINUE TO PHOTOGRAPHS" }));

    expect(stub.sent).toEqual([{ name: "GoToStep", payload: { step: "photographs" } }]);
  });

  it("sends nothing from a continue action the brain has not made available", () => {
    const stub = stubClient();
    renderTemplate(templateOf("guest-ui-template-signature", { canContinue: false }), stub);

    const action = screen.getByRole("button", { name: "CONTINUE TO PHOTOGRAPHS" });
    expect(action).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(action);

    expect(names(stub.sent)).toEqual([]);
  });

  it("shows the selected Template's blank preview as a print, turned a quarter for Panorama Four", () => {
    const { container } = renderTemplate(templateOf("guest-ui-template-panorama"), stubClient());

    const preview = screen.getByRole("img", { name: "Blank Panorama Four Photo Strip" });
    expect(preview).toHaveAttribute("width", "1800");
    expect(preview.parentElement).toHaveAttribute("data-rotated", "true");
    expect(container.querySelector("[data-selected-template='panorama-four']")).not.toBeNull();
  });

  it("keeps the print upright for a portrait Template, and serves the composed preview when the brain has one", () => {
    const view = templateOf("guest-ui-template-signature");
    const [first, ...rest] = view.templates;
    if (first === undefined) {
      throw new Error("no template");
    }
    renderTemplate(
      { ...view, templates: [{ ...first, blankPreviewKey: "signature-strip.blank" }, ...rest] },
      stubClient({ previewUrl: (key) => `https://preview.fotohavn/${key}.png` }),
    );

    const preview = screen.getByRole("img", { name: "Blank Signature Strip Photo Strip" });
    expect(preview.parentElement).not.toHaveAttribute("data-rotated");
    expect(preview).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.blank.png");
  });

  it("draws nothing for a selection the offer does not contain", () => {
    renderTemplate(templateOf("guest-ui-template-signature", { selectedId: "gone" }), stubClient());

    expect(screen.queryByRole("img", { name: /Blank/ })).toBeNull();
  });
});
