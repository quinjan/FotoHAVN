import { defineConfig, mergeConfig } from "vitest/config";
import viteConfig from "./vite.config.ts";

export default mergeConfig(
  viteConfig,
  defineConfig({
    test: {
      environment: "jsdom",
      include: ["src/**/*.test.{ts,tsx}"],
      setupFiles: ["./src/test/setup.ts"],
      restoreMocks: true,
      unstubGlobals: true,
      coverage: {
        provider: "v8",
        reportsDirectory: "coverage",
        reporter: ["text", "text-summary", "html", "lcov", "cobertura"],
        include: ["src/**/*.{ts,tsx}"],
        exclude: [
          // Entry point: only mounts <App /> into #root; covered by the fixture lane and end-to-end runs.
          "src/main.tsx",
          // Type declarations contain no executable code.
          "src/**/*.d.ts",
          // Tests and their setup are not product code.
          "src/**/*.test.{ts,tsx}",
          "src/test/**",
        ],
        thresholds: {
          lines: 80,
          branches: 80,
          functions: 80,
          statements: 80,
        },
      },
    },
  }),
);
