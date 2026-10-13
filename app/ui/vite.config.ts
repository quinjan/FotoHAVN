import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The WPF host serves dist/ from the https://app.fotohavn/ virtual host root.
export default defineConfig({
  plugins: [react()],
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
