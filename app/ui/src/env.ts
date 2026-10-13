export interface AppEnv {
  /** True only in E2E builds (`npm run build:e2e`). */
  readonly e2e: boolean;
}

export function readEnv(source: ImportMetaEnv): AppEnv {
  return { e2e: source.VITE_E2E === "true" };
}

export const env: AppEnv = readEnv(import.meta.env);
