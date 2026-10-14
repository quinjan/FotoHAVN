import { render, screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import type { GalleryEntry } from "./discover.ts";
import { Gallery, GalleryError, manifestElementId } from "./Gallery.tsx";

const entries: GalleryEntry[] = [
  {
    source: "components/Thing/Thing.fixtures.tsx",
    fixture: { id: "thing-guest", title: "Thing, guest", surface: "dark", role: "guest", render: () => <p>Guest thing</p> },
  },
  {
    source: "components/Thing/Thing.fixtures.tsx",
    fixture: {
      id: "thing-operator",
      title: "Thing, operator",
      surface: "light",
      role: "operator",
      layout: "fill",
      render: () => <p>Operator thing</p>,
    },
  },
];

describe("Gallery", () => {
  it("lists and renders every fixture by component on the index", () => {
    render(<Gallery entries={entries} selection="" />);
    expect(screen.getByRole("heading", { level: 1, name: "Fixture gallery" })).toBeVisible();
    const group = screen.getByRole("region", { name: "Thing" });
    expect(within(group).getByRole("link", { name: "thing-guest" })).toHaveAttribute("href", "?gallery=thing-guest");
    expect(within(group).getByText("Thing, guest · guest · dark")).toBeVisible();
    expect(within(group).getByText("Guest thing").parentElement).toHaveAttribute("data-tone", "dark");
    expect(within(group).getByText("Operator thing").parentElement).toHaveAttribute("data-fotohavn-role", "operator");
    expect(document.documentElement).not.toHaveAttribute("data-fotohavn-role");
  });

  it("publishes the manifest as JSON", () => {
    render(<Gallery entries={entries} selection="manifest" />);
    const manifest = document.getElementById(manifestElementId);
    expect(JSON.parse(manifest?.textContent ?? "")).toEqual({
      fixtures: [
        { id: "thing-guest", title: "Thing, guest", surface: "dark", role: "guest", layout: "centered", source: "components/Thing/Thing.fixtures.tsx" },
        { id: "thing-operator", title: "Thing, operator", surface: "light", role: "operator", layout: "fill", source: "components/Thing/Thing.fixtures.tsx" },
      ],
    });
  });

  it("renders one fixture alone with its tone, layout, and window role, and marks the document ready", async () => {
    const { unmount } = render(<Gallery entries={entries} selection="thing-guest" />);
    const main = screen.getByRole("main", { name: "Thing, guest" });
    expect(main).toHaveAttribute("data-tone", "dark");
    expect(main).toHaveAttribute("data-layout", "centered");
    expect(main).toHaveAttribute("data-fotohavn-fixture", "thing-guest");
    expect(within(main).getByText("Guest thing")).toBeVisible();
    expect(document.documentElement).toHaveAttribute("data-fotohavn-role", "guest");
    await waitFor(() => {
      expect(document.documentElement).toHaveAttribute("data-fotohavn-gallery-ready", "true");
    });

    unmount();
    expect(document.documentElement).not.toHaveAttribute("data-fotohavn-role");
    expect(document.documentElement).not.toHaveAttribute("data-fotohavn-gallery-ready");
  });

  it("uses the fill layout when a fixture asks for it", () => {
    render(<Gallery entries={entries} selection="thing-operator" />);
    expect(screen.getByRole("main", { name: "Thing, operator" })).toHaveAttribute("data-layout", "fill");
    expect(document.documentElement).toHaveAttribute("data-fotohavn-role", "operator");
  });

  it("says when a fixture doesn't exist", () => {
    render(<Gallery entries={entries} selection="nope" />);
    expect(screen.getByRole("heading", { level: 1, name: "No fixture named “nope”" })).toBeVisible();
    expect(screen.getByRole("link", { name: "ALL FIXTURES" })).toHaveAttribute("href", "?gallery");
  });

  it("shows a discovery error", () => {
    const { rerender } = render(<GalleryError error={new Error("Fixture id \"a\" is used twice.")} />);
    expect(screen.getByRole("alert")).toHaveTextContent('Fixture id "a" is used twice.');
    rerender(<GalleryError error="plain" />);
    expect(screen.getByRole("alert")).toHaveTextContent("plain");
  });
});
