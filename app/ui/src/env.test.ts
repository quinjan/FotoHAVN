import { describe, expect, it } from "vitest";
import { env, readEnv } from "./env.ts";

function source(values: Partial<ImportMetaEnv>): ImportMetaEnv {
  return { DEV: false, ...values } as ImportMetaEnv;
}

describe("env", () => {
  it("is an E2E build only when VITE_E2E is the string true", () => {
    expect(readEnv(source({ VITE_E2E: "true" })).e2e).toBe(true);
    expect(readEnv(source({ VITE_E2E: "false" })).e2e).toBe(false);
    expect(readEnv(source({})).e2e).toBe(false);
  });

  it("is a development build only under the dev server", () => {
    expect(readEnv(source({ DEV: true })).dev).toBe(true);
    expect(readEnv(source({ DEV: false })).dev).toBe(false);
  });

  it("reads import.meta.env, which has no VITE_E2E and DEV true under Vitest", () => {
    expect(env).toEqual({ e2e: false, dev: true });
  });
});
