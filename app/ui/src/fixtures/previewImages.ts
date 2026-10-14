import { fixtureStrip, type FixtureTemplate } from "../components/shared/fixturePhotographs.ts";

const templates: readonly FixtureTemplate[] = ["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"];

/**
 * The image the fixture lane shows for a preview key (`<templateId>.<lookId>.r<revision>`): the placeholder Photo Strip
 * of that Template as a data URL, since no host serves `preview.fotohavn` in the lane. An unknown Template shows the
 * Signature Strip; the revision varies the scenes, so two revisions of one Template differ.
 */
export function fixturePreviewUrl(key: string): string {
  const [templateId, , revision] = key.split(".");
  const template = templates.find((candidate) => candidate === templateId) ?? "signature-strip";
  const seed = Number(revision?.slice(1));
  return fixtureStrip(template, Number.isFinite(seed) ? seed : 0).src;
}
