import { describe, expect, it } from "vitest";
import { defaultFixturePort, fixturePort } from "./fixture-port.ts";

describe("fixturePort", () => {
  it("defaults to 4173 when the variable is unset or blank", () => {
    expect(defaultFixturePort).toBe(4173);
    expect(fixturePort({})).toBe(4173);
    expect(fixturePort({ FOTOHAVN_FIXTURE_PORT: "  " })).toBe(4173);
  });

  it("reads FOTOHAVN_FIXTURE_PORT", () => {
    expect(fixturePort({ FOTOHAVN_FIXTURE_PORT: "4174" })).toBe(4174);
  });

  it.each(["abc", "0", "70000", "41.5"])("rejects %s", (value) => {
    expect(() => fixturePort({ FOTOHAVN_FIXTURE_PORT: value })).toThrow(/FOTOHAVN_FIXTURE_PORT/);
  });
});
