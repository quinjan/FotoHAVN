import { describe, expect, it } from "vitest";
import { env, readEnv } from "./env.ts";

describe("env", () => {
  it("is an E2E build only when VITE_E2E is the string true", () => {
    expect(readEnv({ VITE_E2E: "true" } as ImportMetaEnv).e2e).toBe(true);
    expect(readEnv({ VITE_E2E: "false" } as ImportMetaEnv).e2e).toBe(false);
    expect(readEnv({} as ImportMetaEnv).e2e).toBe(false);
  });

  it("reads import.meta.env, which has no VITE_E2E under Vitest", () => {
    expect(env.e2e).toBe(false);
  });
});
