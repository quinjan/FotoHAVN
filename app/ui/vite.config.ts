import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

/** Written into the E2E UI build only; the host and the publish guard refuse a field build that contains it. */
const e2eBuildMarker = "e2e-build.txt";

/**
 * Mode `e2e` (`npm run build:e2e`) builds to dist-e2e/ instead of dist/, so the E2E bundle never lands where field
 * builds copy the UI from, and marks the build with e2e-build.txt.
 */
function e2eBuildOutput(): Plugin {
  let isE2E = false;
  return {
    name: "fotohavn-e2e-build-output",
    config(_config, { mode }) {
      isE2E = mode === "e2e";
      return isE2E ? { build: { outDir: "dist-e2e" } } : undefined;
    },
    generateBundle() {
      if (isE2E) {
        this.emitFile({
          type: "asset",
          fileName: e2eBuildMarker,
          source: "FOTOHAVN E2E UI build (VITE_E2E=true). Never ship it in a field build.\n",
        });
      }
    },
  };
}

// The WPF host serves dist/ (dist-e2e/ in E2E builds) from the https://app.fotohavn/ virtual host root.
export default defineConfig({
  plugins: [react(), e2eBuildOutput()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
  preview: {
    host: "127.0.0.1",
    port: 4173,
    strictPort: true,
  },
});
