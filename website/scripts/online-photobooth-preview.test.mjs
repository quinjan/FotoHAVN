import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import ts from 'typescript';
function compile(file, dependencies) {
  const source = readFileSync(new URL(`../src/components/onlinePhotobooth/${file}`, import.meta.url), 'utf8');
  const result = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020, jsx: ts.JsxEmit.ReactJSX } });
  const exports = {};
  vm.runInNewContext(result.outputText, { exports, require: (id) => { assert.ok(id in dependencies, id); return dependencies[id]; }, AbortController, URL: { createObjectURL: () => `blob:test-${++serial}`, revokeObjectURL() {} } });
  return exports;
}
let serial = 0;
const presets = compile('presets.ts', {});
const { cameraGuide } = compile('CameraFrameGuide.tsx', { './presets': presets, react: {}, 'react/jsx-runtime': {}, './OnlinePhotobooth.module.css': {} });
test('camera framing matches normalized 4:3 source and portrait/landscape template crops', () => {
  const portrait = cameraGuide(1280, 720, 1200, 600, .75);
  assert.equal(portrait.width, 450); assert.equal(portrait.height, 600);
  assert.equal(portrait.left, 375); assert.equal(portrait.top, 0);
  const landscape = cameraGuide(720, 1280, 320, 480, 4 / 3);
  assert.equal(landscape.width, 270); assert.equal(landscape.height, 202.5);
  assert.equal(landscape.left, 25); assert.equal(landscape.top, 138.75);
  assert.equal(cameraGuide(0, 720, 1200, 600, .75), null);
});

function hookFixture() {
  let cursor = 0; const cells = [], effects = [], renders = [];
  const changed = (a, b) => !a || a.length !== b.length || a.some((value, i) => value !== b[i]);
  const react = {
    useState(initial) { const i = cursor++; cells[i] ??= { value: initial }; return [cells[i].value, (next) => { cells[i].value = typeof next === 'function' ? next(cells[i].value) : next; }]; },
    useMemo(create, deps) { const i = cursor++; if (!cells[i] || changed(cells[i].deps, deps)) cells[i] = { value: create(), deps }; return cells[i].value; },
    useEffect(effect, deps) { const i = cursor++; if (!cells[i] || changed(cells[i].deps, deps)) { cells[i]?.cleanup?.(); cells[i] = { deps }; effects.push(() => { cells[i].cleanup = effect(); }); } },
  };
  const { useComposition } = compile('useComposition.ts', {
    react,
    './compositor': { loadCompositionFonts: async () => ({}), renderComposition: (source) => new Promise((resolve) => renders.push({ source, resolve })) },
    './media': { decodeImage: async () => ({}) }, './download': { compositionFilename: () => 'strip.png' },
  });
  return { renders, render(state) {
    cursor = 0;
    // This fixture runs the hook against the simulated React dispatcher above.
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const value = useComposition(state, true);
    effects.splice(0).forEach((effect) => effect());
    return value;
  } };
}
const tick = () => new Promise((resolve) => setImmediate(resolve));
test('candidate preview invalidates readiness even at the same revision and never changes accepted slots', async () => {
  const fixture = hookFixture();
  const state = { layoutId: 'signature-strip', frameId: 'ivory-v1', lookId: 'naturale-v1', revision: 1, slots: ['A'], photos: { A: { id: 'A' } }, operation: null };
  assert.equal(fixture.render(state).ready, false); await tick();
  fixture.renders[0].resolve({}); await tick();
  assert.equal(fixture.render(state).ready, true);
  const candidate = { ...state, photos: { ...state.photos, B: { id: 'B' } }, operation: { target: 0, candidate: 'B' } };
  assert.equal(fixture.render(candidate).ready, false); await tick();
  assert.equal(fixture.renders[1].source.photos[0].id, 'B');
  assert.equal(candidate.slots[0], 'A');
  fixture.renders[1].resolve({}); await tick();
  assert.equal(fixture.render(candidate).ready, true);
  assert.equal(fixture.render(state).ready, false, 'Keep original must await restored composition'); await tick();
  assert.equal(fixture.renders[2].source.photos[0].id, 'A');
  fixture.renders[2].resolve({}); await tick();
  assert.equal(fixture.render(state).ready, true);
});
