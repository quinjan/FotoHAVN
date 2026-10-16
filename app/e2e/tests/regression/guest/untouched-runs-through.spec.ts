import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

test(
  "an untouched cycle (STEP INSIDE only, with the clock at 10×) runs through with the defaults to a saved Photo Strip, recorded like a guest-finished cycle",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Untouched cycle" });
    const guest = await run.expectStartReady();

    await hooks.setClockScale(10);
    try {
      await tap(guest.stepInside);

      await test.step("every phase timer runs out in turn: Template, capture, review, Filters, and Keepsake, back to Start", async () => {
        const cycle = await run.endedCycle("completed", 120_000);
        expect(cycle.ended_at).not.toBeNull();
        await expect(guest.surface("start")).toBeVisible({ timeout: 30_000 });
        await expect(run.list.runningFact("cycles")).toHaveText("1");
      });
    } finally {
      await hooks.setClockScale(1);
    }

    await test.step("the record is a guest-finished one: default Template and Look, four automatic Captures in order, and a Photo Strip", async () => {
      const cycle = await run.onlyCycle();
      const captures = run.captures(cycle.id);
      expect(captures.map((capture) => `${String(capture.sequence)}:${capture.state}`)).toEqual(["1:kept", "2:kept", "3:kept", "4:kept"]);
      run.expectCaptureFiles(cycle.id);
      const strip = run.expectPhotoStrip(cycle.id, "Signature Strip");
      expect(strip.template_id).toBe("signature-strip");
      expect(strip.look_id).toBe("naturale-v1");
      expect(JSON.parse(strip.arrangement)).toEqual(captures.map((capture) => capture.id));
      run.expectNoStrayFiles(cycle.id);
    });

    await run.endEventFromOperator();
  },
);
