import { StrictMode } from "react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { App } from "./App.tsx";

describe("App placeholder", () => {
  it("renders the heading and status text inside main", () => {
    render(<App />);

    const main = screen.getByRole("main");
    expect(main).toContainElement(screen.getByRole("heading", { level: 1, name: "FOTOHAVN" }));
    expect(main).toHaveTextContent("The booth is getting ready.");
  });

  it("posts ready to the host exactly once, even under StrictMode", () => {
    const postMessage = vi.fn();
    vi.stubGlobal("chrome", { webview: { postMessage } });

    render(
      <StrictMode>
        <App />
      </StrictMode>,
    );

    expect(postMessage).toHaveBeenCalledTimes(1);
    expect(postMessage).toHaveBeenCalledWith({ type: "ready" });
  });

  it("renders without throwing outside WebView2", () => {
    expect(window.chrome).toBeUndefined();
    expect(() => render(<App />)).not.toThrow();
  });
});
