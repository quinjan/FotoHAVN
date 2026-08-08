export const responsiveOwnerState = {
  "saved-events": "maximum-cards",
  "event-setup": "camera-unavailable",
  "guest-start": "exit-holding",
  "guest-start-unavailable": "retry-failed",
  capture: "countdown-1",
  "operator-assistance": "retry-failed",
  "photo-strip": "returning",
  confirmation: "delete-busy",
};

const list = (value) => value ? value.split(",").map((item) => item.trim()).filter(Boolean) : [];
const field = (body, name) => body.match(new RegExp(`^    ${name}: \\[(.*)\\]$`, "m"))?.[1] ?? "";

export function parseMatrix(source) {
  const surfaceSection = source.match(/^surfaces:\r?\n([\s\S]*?)^viewports:/m)?.[1] ?? "";
  const viewportSection = source.match(/^viewports:\r?\n([\s\S]*?)^acceptance:/m)?.[1] ?? "";
  const surfaces = surfaceSection.split(/(?=^  - id: )/m).filter((block) => block.trim()).map((block) => ({
    id: block.match(/^  - id: ([^\r\n]+)/m)?.[1],
    audience: block.match(/^    audience: (.+)$/m)?.[1],
    canonicalStates: list(field(block, "canonicalStates")),
    responsiveRisk: block.match(/^    responsiveRisk: (.+)$/m)?.[1],
  }));
  const viewports = viewportSection.split(/(?=^  - id: )/m).filter((block) => block.trim()).map((block) => ({
    id: block.match(/^  - id: ([^\r\n]+)/m)?.[1],
    size: block.match(/^    size: ([^\r\n]+)/m)?.[1],
    coverage: block.match(/^    coverage: ([^\r\n]+)/m)?.[1],
  }));
  return { surfaces, viewports };
}

export function parseWinuiMappingSource(source) {
  const semanticTokens = [...source.matchAll(/^  - \{ id: ([^,]+), resource: ([^,]+), kind: (.+) \}$/gm)].map((match) => ({
    semanticId: match[1],
    xamlResourceKey: match[2],
    winuiType: match[3],
    resourceDictionary: "src/FotoHavn.App/DesignSystem/FotoHavnDesignResources.xaml",
    verificationIdentifiers: ["MANUAL-SEMANTIC-RESOURCE-AUDIT"],
  }));
  const section = source.match(/\r?\ncomponents:\r?\n([\s\S]*?)\r?\nverificationSeams:/)?.[1] ?? "";
  const components = section.split(/(?=^  - id: )/m).filter((block) => block.trim()).map((block) => {
    const semanticId = block.match(/^  - id: ([^\r\n]+)/m)?.[1];
    const body = block.replace(/^  - id: [^\r\n]+\r?\n/m, "");
    return {
      semanticId,
      controlType: body.match(/^    control: (.+)$/m)?.[1],
      sourceOwnership: `docs/design-system/components/${semanticId.replace("component.", "")}.md`,
      styleOwnership: list(field(body, "styles")),
      properties: list(field(body, "properties")),
      visualStateGroups: body.match(/^    visualStateGroups: (.+)$/m)?.[1],
      automationIdPrefix: `FotoHavn.${semanticId.replace("component.", "").split("-").map((part) => part[0].toUpperCase() + part.slice(1)).join("")}`,
      accessibilityObligations: list(field(body, "accessibility")),
      sharedVerificationIdentifiers: list(field(body, "verification")),
    };
  });
  return { semanticTokens, components };
}

const quoted = (source, expression) => source.match(expression)?.[1]?.replaceAll('\\"', '"');

export function parseAnnotation(source) {
  const readingOrderBlock = source.match(/^readingOrder:\r?\n([\s\S]*?)(?=^[A-Za-z])/m)?.[1] ?? "";
  return {
    heading: quoted(source, /^heading:\r?\n  text: "(.*)"$/m),
    automationName: quoted(source, /^automation:\r?\n  name: "(.*)"$/m),
    automationRole: quoted(source, /^automation:\r?\n(?:.*\r?\n)*?  role: "(.*)"$/m),
    automationState: quoted(source, /^automation:\r?\n(?:.*\r?\n)*?  state: "(.*)"$/m),
    readingOrder: [...readingOrderBlock.matchAll(/^  - "(.*)"$/gm)].map((match) => match[1]).join(" -> "),
    initialFocus: quoted(source, /^focus:\r?\n  initial: "(.*)"$/m),
    focusOrder: quoted(source, /^focus:\r?\n(?:.*\r?\n)*?  order: "(.*)"$/m),
    focusReturn: quoted(source, /^focus:\r?\n(?:.*\r?\n)*?  returnTarget: "(.*)"$/m),
    announcement: quoted(source, /^announcements:\r?\n  - text: "(.*)"$/m),
    announcementPriority: quoted(source, /^announcements:\r?\n(?:.*\r?\n)*?    priority: "(.*)"$/m),
  };
}
