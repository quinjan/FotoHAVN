import { defineConfig, type HtmlTagDescriptor, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import { fixturePort } from "./scripts/fixture-port.ts";

/** Written into the E2E UI build only; the host and the publish guard refuse a field build that contains it. */
const e2eBuildMarker = "e2e-build.txt";

/**
 * The latin font files every first screen needs, preloaded from index.html so `font-display: block` never waits on
 * CSS parsing. They come from the exact-pinned @fontsource packages (src/styles/fonts.css), bundled, never fetched.
 */
const preloadedFonts = [
  "@fontsource/cormorant-garamond/files/cormorant-garamond-latin-400-normal.woff2",
  "@fontsource/cormorant-garamond/files/cormorant-garamond-latin-400-italic.woff2",
  "@fontsource/cormorant-garamond/files/cormorant-garamond-latin-500-normal.woff2",
  "@fontsource/manrope/files/manrope-latin-400-normal.woff2",
  "@fontsource/manrope/files/manrope-latin-600-normal.woff2",
] as const;

function fileStem(path: string): string {
  return path.replace(/^.*\//, "").replace(/\.woff2$/, "");
}

/**
 * Adds `<link rel="preload" as="font">` for each preloaded font: the hashed bundle URL in builds, the package file
 * under the dev server. A build fails if a font is missing from the bundle, so a renamed file can't silently drop
 * its preload.
 */
function fontPreload(): Plugin {
  let base = "/";
  return {
    name: "fotohavn-font-preload",
    configResolved(config) {
      base = config.base;
    },
    transformIndexHtml: {
      order: "post",
      handler(_html, context) {
        const tags: HtmlTagDescriptor[] = [];
        const assets = Object.values(context.bundle ?? {}).filter((output) => output.type === "asset");
        for (const font of preloadedFonts) {
          let href = `/node_modules/${font}`;
          if (context.bundle !== undefined) {
            const stem = fileStem(font);
            const asset = assets.find(({ fileName }) => new RegExp(`(?:^|/)${stem}-[\\w-]+\\.woff2$`).test(fileName));
            if (asset === undefined) {
              throw new Error(`fotohavn-font-preload: ${font} is not in the bundle; check src/styles/fonts.css.`);
            }
            href = `${base}${asset.fileName}`;
          }
          tags.push({
            tag: "link",
            attrs: { rel: "preload", as: "font", type: "font/woff2", href, crossorigin: "" },
            injectTo: "head",
          });
        }
        return tags;
      },
    },
  };
}

/** Module paths that exist only for development and E2E builds: the fixture lane, the gallery, and fixtures. */
const devOnlyModule = /\/src\/(?:fixtures|gallery)\/|\.fixtures\.tsx$|\/components\/shared\/fixture/;

/**
 * Fails a field build (any mode other than `e2e`) that bundles a development-only module, backing up the build-time
 * guards in main.tsx: `dist/` must hold no fixture lane, gallery, or component fixture code.
 */
function fieldBuildGuard(): Plugin {
  let isE2E = false;
  return {
    name: "fotohavn-field-build-guard",
    apply: "build",
    configResolved(config) {
      isE2E = config.mode === "e2e";
    },
    generateBundle(_options, bundle) {
      if (isE2E) {
        return;
      }
      for (const output of Object.values(bundle)) {
        if (output.type !== "chunk") {
          continue;
        }
        const leaked = output.moduleIds.find((id) => devOnlyModule.test(id.replaceAll("\\", "/")));
        if (leaked !== undefined) {
          this.error(`A field build must not bundle ${leaked} (chunk ${output.fileName}); keep it behind the DEV/E2E guard.`);
        }
      }
    },
  };
}

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
  plugins: [react(), e2eBuildOutput(), fontPreload(), fieldBuildGuard()],
  build: {
    outDir: "dist",
    emptyOutDir: true,
  },
  preview: {
    host: "127.0.0.1",
    port: fixturePort(),
    strictPort: true,
  },
});
