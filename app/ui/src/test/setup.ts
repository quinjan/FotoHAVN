import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

afterEach(() => {
  cleanup();
  // The App writes data-fotohavn-* on the root element; reset it between tests. Node-environment tests have no DOM.
  if (typeof document !== "undefined") {
    const root = document.documentElement;
    for (const name of root.getAttributeNames().filter((attribute) => attribute.startsWith("data-"))) {
      root.removeAttribute(name);
    }
  }
});
