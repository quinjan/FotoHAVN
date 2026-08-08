import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const root = new URL("../", import.meta.url);

test("the verification catalog covers every approved canonical injection identity", async () => {
  const scenarioUrl = new URL("docs/design-system/traceability/scenarios/", root);
  const surfaces = [
    "capture",
    "confirmation",
    "event-setup",
    "guest-start-unavailable",
    "guest-start",
    "operator-assistance",
    "photo-strip",
    "saved-events",
  ];
  const expected = [];
  for (const surface of surfaces) {
    const scenarioFile = JSON.parse(await readFile(new URL(`${surface}.json`, scenarioUrl), "utf8"));
    expected.push(...scenarioFile.scenarios.map((scenario) => scenario.deterministicInjectionIdentity));
  }

  const catalog = JSON.parse(await readFile(
    new URL("src/FotoHavn.App/UiVerification/ApprovedInjectionCatalog.json", root),
    "utf8",
  ));

  assert.equal(catalog.length, 71);
  assert.deepEqual(
    catalog.map((entry) => entry.identity).sort(),
    expected.sort(),
  );
});

test("verification code and assets compile only in an explicit verification build", async () => {
  const project = await readFile(new URL("src/FotoHavn.App/FotoHavn.App.csproj", root), "utf8");
  assert.match(project, /<Compile Remove="UiVerification\\\*\*\\\*\.cs"\s*\/>/);
  assert.match(project, /Condition="'\$\(UiVerificationBuild\)' == 'true'"/);
  assert.match(project, /<DefineConstants>\$\(DefineConstants\);UI_VERIFICATION<\/DefineConstants>/);
  assert.match(project, /ApprovedInjectionCatalog\.json/);

  const fieldProfile = await readFile(
    new URL("src/FotoHavn.App/Properties/PublishProfiles/FieldTest-win-x64.pubxml", root),
    "utf8",
  );
  assert.match(fieldProfile, /<UiVerificationBuild>false<\/UiVerificationBuild>/);
  assert.match(project, /RejectUiVerificationFieldTestPublish/);
});

test("all mapped production compositions and semantic roots are implemented", async () => {
  const mapping = JSON.parse(await readFile(
    new URL("docs/design-system/traceability/mapping.json", root),
    "utf8",
  ));
  const adapter = await readFile(
    new URL("src/FotoHavn.App/Surfaces/ApplicationPresentationAdapter.cs", root),
    "utf8",
  );

  for (const surface of mapping.surfaces) {
    const typeName = surface.productionComposition.split(".").at(-1);
    assert.match(adapter, new RegExp(`class ${typeName}\\b`));
    assert.ok(adapter.includes(surface.semanticRootAutomationId));
  }
});
