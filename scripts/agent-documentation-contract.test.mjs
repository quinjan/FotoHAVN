import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";
import test from "node:test";

const repoRoot = fileURLToPath(new URL("../", import.meta.url));

function read(relativePath) {
  return readFileSync(path.join(repoRoot, relativePath), "utf8");
}

function assertRoute(document, trigger, target) {
  const content = read(document);
  assert.match(content, trigger);
  assert.match(content, new RegExp(target.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
  assert.equal(existsSync(path.join(repoRoot, target)), true, `${target} must exist`);
}

const promptRoutes = [
  {
    prompt: "name or change product behavior",
    trigger: /Domain language or product behavior/,
    target: "docs/agents/domain.md",
  },
  {
    prompt: "design a FOTOHAVN poster",
    trigger: /Brand, visual identity/,
    target: "brand/README.md",
  },
  {
    prompt: "fix a Camera bug",
    trigger: /Desktop app/,
    target: "app/AGENTS.md",
  },
  {
    prompt: "change the website hero",
    trigger: /Website or online booth/,
    target: "website/AGENTS.md",
  },
  {
    prompt: "deploy the website",
    trigger: /Production deployment/,
    target: "deploy/fotohavn/AGENTS.md",
  },
  {
    prompt: "triage a GitHub issue",
    trigger: /Issue triage/,
    target: "docs/agents/triage-labels.md",
  },
];

for (const route of promptRoutes) {
  test(`root router handles: ${route.prompt}`, () => {
    assertRoute("AGENTS.md", route.trigger, route.target);
  });
}

test("every root task route points to an existing document", () => {
  for (const target of [
    "docs/agents/domain.md",
    "brand/README.md",
    "app/AGENTS.md",
    "website/AGENTS.md",
    "website/docs/website-analytics.md",
    "deploy/fotohavn/AGENTS.md",
    "marketing/reels/AGENTS.md",
    "docs/agents/issue-tracker.md",
    "docs/agents/triage-labels.md",
  ]) {
    assert.equal(existsSync(path.join(repoRoot, target)), true, `${target} must exist`);
  }
});

test("context map routes shared, desktop, and website language", () => {
  const content = read("CONTEXT-MAP.md");
  for (const target of [
    "CONTEXT.md",
    "app/CONTEXT.md",
    "app/docs/adr/0001-capture-from-one-windows-video-stream.md",
    "website/CONTEXT.md",
    "deploy/fotohavn/docs/adr/0002-deploy-website-as-an-immutable-container.md",
  ]) {
    assert.match(content, new RegExp(target.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")));
    assert.equal(existsSync(path.join(repoRoot, target)), true);
  }
});

test("website agent file preserves managed rules and adds project routing", () => {
  const content = read("website/AGENTS.md");
  assert.equal((content.match(/BEGIN:nextjs-agent-rules/g) ?? []).length, 1);
  assert.equal((content.match(/END:nextjs-agent-rules/g) ?? []).length, 1);
  assert.match(content, /# FotoHAVN website work/);
  assert.match(content, /DESIGN\.md/);
  assert.match(content, /docs\/website-analytics\.md/);
  assert.match(content, /npm run check/);
});

test("website exposes one complete local verification command", () => {
  const packageJson = JSON.parse(read("website/package.json"));
  for (const script of ["typecheck", "test:online", "test", "check"]) {
    assert.equal(typeof packageJson.scripts[script], "string", `${script} script must exist`);
  }
  assert.match(packageJson.scripts.check, /lint/);
  assert.match(packageJson.scripts.check, /typecheck/);
  assert.match(packageJson.scripts.check, /npm test/);
  assert.match(packageJson.scripts.check, /build/);
});

test("issue tracker keeps optional Wayfinder behavior behind a pointer", () => {
  const content = read("docs/agents/issue-tracker.md");
  assert.doesNotMatch(content, /\bjq\b/);
  assert.match(content, /wayfinder\.md/);
  assert.equal(existsSync(path.join(repoRoot, "docs/agents/wayfinder.md")), true);
});

function markdownFiles(relativeDirectory) {
  return readdirSync(path.join(repoRoot, relativeDirectory), { recursive: true })
    .filter((entry) => entry.endsWith(".md"))
    .map((entry) => path.posix.join(relativeDirectory, entry.split(path.sep).join("/")));
}

test("local Markdown links in the documentation entry points resolve", () => {
  for (const document of [
    "README.md",
    "CONTEXT-MAP.md",
    "app/README.md",
    "website/README.md",
    "website/DESIGN.md",
    ...markdownFiles("brand"),
  ]) {
    const content = read(document);
    const documentDirectory = path.dirname(path.join(repoRoot, document));
    const links = [...content.matchAll(/\[[^\]]+\]\(([^)]+)\)/g)].map((match) => match[1]);

    for (const link of links) {
      if (/^[a-z]+:/i.test(link) || link.startsWith("#")) continue;
      const target = link.split("#", 1)[0];
      assert.equal(
        existsSync(path.resolve(documentDirectory, target)),
        true,
        `${document} points to missing ${link}`,
      );
    }
  }
});

test("brand system tokens are complete and the website interprets them", () => {
  const brand = JSON.parse(read("brand/tokens/tokens.json"));
  const colors = Object.entries(brand.color);
  assert.equal(colors.length, 9, "brand palette has nine primitive colors");
  for (const [name, token] of colors) {
    const meta = token.$extensions?.["com.fotohavn.brand"];
    assert.match(token.$value, /^#[0-9A-F]{6}$/, `${name} has an uppercase HEX value`);
    assert.equal(meta?.rgb?.length, 3, `${name} has RGB`);
    assert.equal(meta?.cmyk?.length, 4, `${name} has CMYK`);
  }

  const website = JSON.parse(read("website/tokens.json"));
  for (const [name, token] of colors) {
    assert.equal(website.color[name]?.$value?.toUpperCase(), token.$value, `website ${name} matches brand`);
  }

  const design = read("website/DESIGN.md");
  assert.match(design, /## Brand interpretation/);
  assert.match(design, /\.\.\/brand\/README\.md/);
  assert.match(read("marketing/reels/AGENTS.md"), /brand\/README\.md/);
});

test("Claude compatibility shims delegate to the canonical agent guides", () => {
  assert.equal(read("CLAUDE.md").trim(), "@AGENTS.md");
  assert.equal(read("app/CLAUDE.md").trim(), "@AGENTS.md");
  assert.equal(read("website/CLAUDE.md").trim(), "@AGENTS.md");
});

test("agent documentation contract runs in CI when routing files change", () => {
  const workflow = read(".github/workflows/agent-documentation.yml");
  assert.match(workflow, /scripts\/\*\*/);
  assert.match(workflow, /run: \.\/scripts\/verify-agent-docs\.ps1/);
});

test("repository root contains only cross-product modules and repository concerns", () => {
  for (const legacyEntry of [
    "src",
    "tests",
    "tools",
    "prototypes",
    "design-qa",
    "design-qa.md",
    "artifacts",
    "output",
    "research",
    "FotoHAVN.slnx",
    "Directory.Build.props",
    "global.json",
    "NuGet.Config",
  ]) {
    assert.equal(existsSync(path.join(repoRoot, legacyEntry)), false, `${legacyEntry} must not return to the root`);
  }

  for (const appEntry of [
    "AGENTS.md",
    "CLAUDE.md",
    "CONTEXT.md",
    "README.md",
    "FotoHAVN.slnx",
    "Directory.Build.props",
    "global.json",
    "NuGet.Config",
    "src",
    "tests",
    "tools",
    "prototypes",
    "scripts",
    "docs",
    "design-qa",
    "design-qa.md",
    "docs/local-development-guide.md",
    "docs/design-system/README.md",
  ]) {
    assert.equal(existsSync(path.join(repoRoot, "app", appEntry)), true, `app/${appEntry} must exist`);
  }

  assert.deepEqual(readdirSync(path.join(repoRoot, "docs")).sort(), ["agents"]);
  assert.deepEqual(
    readdirSync(path.join(repoRoot, "scripts")).sort(),
    ["agent-documentation-contract.test.mjs", "verify-agent-docs.ps1"],
  );
  assert.deepEqual(readdirSync(path.join(repoRoot, "app", "prototypes")).sort(), ["camera-qualification"]);
});

test("desktop solution project paths resolve inside app", () => {
  const solution = read("app/FotoHAVN.slnx");
  const projectPaths = [...solution.matchAll(/<Project Path="([^"]+)"/g)].map((match) => match[1]);
  assert.ok(projectPaths.length > 0);
  for (const projectPath of projectPaths) {
    assert.equal(
      existsSync(path.join(repoRoot, "app", projectPath)),
      true,
      `app/${projectPath} must exist`,
    );
  }
});

test("desktop UI workflows execute inside the app module", () => {
  for (const workflowName of [
    "ui-verification-batch-3.yml",
    "ui-verification-batch-4.yml",
    "ui-verification-batch-5.yml",
  ]) {
    const workflow = read(`.github/workflows/${workflowName}`);
    assert.match(workflow, /working-directory: app/);
    assert.match(workflow, /hashFiles\('app\/artifacts\/ui-verification\//);
    assert.match(workflow, /app\/docs\/design-system\/reference-states\/targets/);
  }
});
