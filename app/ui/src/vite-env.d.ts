/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** "true" in builds made with `npm run build:e2e` (Vite mode `e2e`, see `.env.e2e`). */
  readonly VITE_E2E?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
