import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import vm from "node:vm";
import ts from "typescript";

function compile(name, dependencies = {}, globals = {}) {
  const source = readFileSync(new URL(`../src/components/onlinePhotobooth/${name}`, import.meta.url), "utf8");
  const result = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2020, jsx: ts.JsxEmit.ReactJSX } });
  const exports = {};
  vm.runInNewContext(result.outputText, { exports, require: (id) => {
    assert.ok(id in dependencies, `Unmapped import ${id}`); return dependencies[id];
  }, ...globals });
  return exports;
}
const presets = compile("presets.ts");
const { sessionReducer: reduce, initialSession } = compile("session.ts", { "./presets": presets });
const plain = (value) => JSON.parse(JSON.stringify(value));

function loaded(ids = ["A", "B", "C", "D"]) {
  let state = reduce(initialSession, { type: "stage", stage: "capture" });
  state = reduce(state, { type: "begin", mode: "import", id: 1 });
  ids.forEach((id, index) => {
    state = reduce(state, { type: "photo", operationId: 1, photo: { id, url: `blob:${id}` }, index });
  });
  return reduce(state, { type: "finish", id: 1 });
}

function fixture(first = loaded()) {
  let state = first, cursor = 0, root, hit, ready = true, commits = 0, cancellations = 0;
  const cells = [], effects = [], owners = new Map(), fileDrops = [];
  const listeners = new Map();
  const eventTarget = { addEventListener: (name, callback) => listeners.set(name, callback), removeEventListener: (name) => listeners.delete(name) };
  const document = { ...eventTarget, elementFromPoint: () => hit, hidden: false };
  const react = {
    useRef: (value) => { const index = cursor++; return cells[index] ?? (cells[index] = { current: value }); },
    useState: (value) => {
      const index = cursor++; if (!(index in cells)) cells[index] = value;
      return [cells[index], (next) => { cells[index] = typeof next === "function" ? next(cells[index]) : next; }];
    },
    useEffect: (effect) => { effects.push(effect); },
  };
  const jsx = (type, props) => ({ type, props });
  const { PrintPreview } = compile("PrintPreview.tsx", {
    "@phosphor-icons/react": { DotsSixVerticalIcon: "drag-handle" },
    "./presets": presets, "./session": { sessionReducer: reduce }, react,
    "react/jsx-runtime": { jsx, jsxs: jsx }, "./OnlinePhotobooth.module.css": { default: {} },
  }, { document, window: eventTarget });
  const descendants = (node) => {
    if (!node || typeof node !== "object") return [];
    return [node, ...[node.props?.children].flat(Infinity).flatMap(descendants)];
  };
  const frame = { contains: (element) => [...owners.values()].includes(element) };
  function render() {
    cursor = 0;
    root = PrintPreview({ state, ready, interactive: true,
      onSelect: (index) => { state = reduce(state, state.move ? { type: "move-to", index } : { type: "select", index }); },
      onReorder: (source, index) => { state = reduce(state, { type: "move-position", source, index }); commits += 1; },
      onDragCancel: () => { cancellations += 1; },
      onFiles: (files, index) => fileDrops.push({ files, index }),
    });
    const print = descendants(root).find((node) => node.props?.["data-layout"]);
    if (print.props.ref) print.props.ref.current = frame;
  }
  const position = (index) => descendants(root).find((node) => node.props?.["data-photo-position"] === index).props;
  function owner(index) {
    if (!owners.has(index)) {
      const element = { dataset: { photoPosition: String(index) }, ownerDocument: document, disabled: false,
        closest: () => element, setPointerCapture() { this.captured = true; },
        hasPointerCapture() { return !!this.captured; }, releasePointerCapture() { this.captured = false; },
      };
      owners.set(index, element);
    }
    return owners.get(index);
  }
  function pointer(index, overrides = {}) {
    return { currentTarget: owner(index), pointerId: 1, pointerType: "mouse", isPrimary: true, button: 0,
      clientX: 40, clientY: 40, preventDefault() { this.prevented = true; }, stopPropagation() {}, ...overrides };
  }
  render();
  return {
    get dropSurface() { return root.props; }, get fileDrops() { return fileDrops; },
    render, position, pointer, hit: (index) => { hit = index === null ? null : owner(index); },
    mountEffects: () => effects.map((effect) => effect()), blur: () => listeners.get("blur")?.(),
    foreignTarget: () => { const external = { closest: () => external, dataset: { photoPosition: "1" } }; hit = external; },
    get state() { return state; }, get commits() { return commits; }, get cancellations() { return cancellations; },
    update: (action) => { state = reduce(state, action); }, readiness: (value) => { ready = value; },
    drag(from, to) {
      const source = position(from);
      source.onPointerDown(pointer(from));
      this.hit(to);
      source.onPointerMove(pointer(from, { clientY: 140 }));
      source.onPointerUp(pointer(from, { clientY: 140 }));
    },
  };
}

test("a rapid drag commits its source without waiting for a move-state render", () => {
  const booth = fixture();
  booth.drag(0, 3);
  assert.deepEqual(plain(booth.state.slots), ["D", "B", "C", "A"]);
  assert.equal(booth.commits, 1);
  booth.position(0).onClick({ detail: 1, preventDefault() {}, stopPropagation() {} });
  assert.equal(booth.state.selected, 3, "the generated click must not select the source after the drop");
  booth.position(0).onLostPointerCapture?.(booth.pointer(0));
  assert.equal(booth.commits, 1, "release/lost capture must not repeat the swap");
});

test("an ordinary click, slight motion, and touch retain selection and tap Move behavior", () => {
  const booth = fixture(), source = booth.position(2);
  source.onPointerDown(booth.pointer(2));
  source.onPointerMove(booth.pointer(2, { clientX: 42, clientY: 43 }));
  source.onPointerUp(booth.pointer(2, { clientX: 42, clientY: 43 }));
  source.onClick({ detail: 1 });
  assert.equal(booth.state.selected, 2); assert.equal(booth.commits, 0);
  const touch = booth.pointer(2, { pointerType: "touch" });
  source.onPointerDown(touch); assert.equal(touch.prevented, undefined);
  assert.equal(touch.currentTarget.captured, false, "touch remains available for browser scrolling");
  booth.update({ type: "move-start", source: { kind: "slot", index: 2 } }); booth.render();
  booth.position(0).onClick({ detail: 0 });
  assert.deepEqual(plain(booth.state.slots), ["C", "B", "A", "D"]);
});

test("outside drops, pointer cancellation, and Escape preserve source assignments", () => {
  for (const mode of ["outside", "pointercancel", "escape", "lostcapture"]) {
    const booth = fixture(), source = booth.position(1);
    source.onPointerDown(booth.pointer(1)); booth.hit(3);
    source.onPointerMove(booth.pointer(1, { clientY: 140 }));
    if (mode === "outside") { booth.hit(null); source.onPointerUp(booth.pointer(1, { clientY: 140 })); }
    if (mode === "pointercancel") source.onPointerCancel(booth.pointer(1));
    if (mode === "lostcapture") source.onLostPointerCapture(booth.pointer(1));
    if (mode === "escape") source.onKeyDown({ key: "Escape", preventDefault() {}, stopPropagation() {} });
    source.onPointerUp(booth.pointer(1, { clientY: 140 }));
    assert.deepEqual(plain(booth.state.slots), ["A", "B", "C", "D"], mode);
    assert.equal(booth.commits, 0, mode); assert.equal(booth.cancellations, 1, mode);
  }
});

test("pending work and stale pointer identities cannot mutate a changed session", () => {
  for (const action of [
    { type: "begin", id: 2, mode: "retake", target: 1 },
    { type: "layout", id: "pair-v1" },
    { type: "stage", stage: "look" },
  ]) {
    const booth = fixture(), source = booth.position(0);
    source.onPointerDown(booth.pointer(0)); booth.hit(1);
    source.onPointerMove(booth.pointer(0, { clientY: 140 }));
    booth.update(action);
    const before = booth.state;
    source.onPointerUp(booth.pointer(0, { clientY: 140 }));
    assert.equal(booth.state, before, action.type);
  }
  const booth = fixture(); booth.readiness(false); booth.render(); booth.drag(0, 1);
  assert.equal(booth.commits, 0);
  assert.deepEqual(plain(booth.state.slots), ["A", "B", "C", "D"]);
});

test("Pair pointer placement preserves the retained tray and restores every source", () => {
  const booth = fixture(); booth.update({ type: "layout", id: "pair-v1" }); booth.render(); booth.drag(0, 1);
  assert.deepEqual(plain(booth.state.slots), ["B", "A"]); assert.deepEqual(plain(booth.state.tray), ["C", "D"]);
  booth.update({ type: "layout", id: "long-strip-v1" });
  assert.deepEqual(plain(booth.state.slots), ["B", "A", "C", "D"]);
});

test("a re-render during a gesture preserves its original source and supports an empty destination", () => {
  const booth = fixture(loaded(["A"]));
  booth.position(0).onPointerDown(booth.pointer(0)); booth.hit(2);
  booth.position(0).onPointerMove(booth.pointer(0, { clientY: 140 }));
  booth.render();
  booth.position(0).onPointerUp(booth.pointer(0, { clientY: 140 }));
  assert.deepEqual(plain(booth.state.slots), [null, null, "A", null]);
  assert.equal(booth.commits, 1);
});

test("only a primary booth pointer may place a photograph; foreign targets and native drags are rejected", () => {
  for (const override of [{ button: 2 }, { isPrimary: false }, { pointerType: "touch" }]) {
    const booth = fixture(), source = booth.position(0);
    source.onPointerDown(booth.pointer(0, override)); booth.hit(1);
    source.onPointerMove(booth.pointer(0, { clientY: 140 }));
    source.onPointerUp(booth.pointer(0, { clientY: 140 }));
    assert.equal(booth.commits, 0);
  }
  const booth = fixture(), source = booth.position(0);
  source.onPointerDown(booth.pointer(0)); booth.hit(1);
  source.onPointerMove(booth.pointer(0, { pointerId: 2, clientY: 140 }));
  source.onPointerUp(booth.pointer(0, { pointerId: 2, clientY: 140 }));
  assert.equal(booth.commits, 0);
  booth.foreignTarget(); source.onPointerUp(booth.pointer(0, { clientY: 140 }));
  assert.equal(booth.commits, 0);
  let prevented = false;
  source.onDragStart({ preventDefault: () => { prevented = true; } });
  assert.equal(prevented, true); assert.equal(source.draggable, false);
});

test("blur and workspace cleanup release an active pointer without committing a placement", () => {
  for (const abandon of ["blur", "cleanup"]) {
    const booth = fixture(), cleanups = booth.mountEffects(), source = booth.position(0);
    source.onPointerDown(booth.pointer(0)); booth.hit(1);
    source.onPointerMove(booth.pointer(0, { clientY: 140 }));
    if (abandon === "blur") booth.blur(); else cleanups.forEach((cleanup) => cleanup());
    source.onPointerUp(booth.pointer(0, { clientY: 140 }));
    assert.equal(booth.pointer(0).currentTarget.captured, false);
    assert.equal(booth.commits, 0);
    assert.deepEqual(plain(booth.state.slots), ["A", "B", "C", "D"]);
  }
});

test("atomic placement rejects repeated delivery and a mismatched photo identity", () => {
  const first = loaded();
  const source = { index: 0, photoId: "A", revision: first.revision };
  assert.equal(reduce(first, { type: "move-position", source: { ...source, photoId: "B" }, index: 1 }), first);
  const action = { type: "move-position", source, index: 3 };
  const placed = reduce(first, action);
  assert.deepEqual(plain(placed.slots), ["D", "B", "C", "A"]);
  assert.equal(reduce(placed, action), placed, "a repeated release cannot swap back");
});

test("download names identify distinct results even at the same timestamp", () => {
  const { compositionFilename } = compile("download.ts");
  const when = new Date("2026-09-14T06:07:08.009Z");
  const first = compositionFilename("long-strip-v1", when, "abcdef12-0000");
  assert.equal(first, "FOTOHAVN-long-strip-2026-09-14T06-07-08-009Z-abcdef12.png");
  assert.equal(compositionFilename("long-strip-v1", when, "abcdef12-0000"), first);
  assert.notEqual(compositionFilename("long-strip-v1", when, "12345678-0000"), first);
  assert.notEqual(compositionFilename("long-strip-v1", new Date(when.getTime() + 1), "abcdef12-0000"), first);
});

for (const [environment, globals] of [["crypto without randomUUID", { crypto: {} }], ["no crypto global", {}]]) {
  test(`download names work with ${environment} and distinguish results at the same timestamp`, () => {
    const { compositionFilename } = compile("download.ts", {}, globals);
    const when = new Date("2026-09-14T06:07:08.009Z");
    const names = Array.from({ length: 256 }, () => compositionFilename("story-strip-v1", when));
    names.forEach((name) => assert.match(name, /^FOTOHAVN-story-strip-2026-09-14T06-07-08-009Z-[a-f0-9]{8}\.png$/));
    assert.equal(new Set(names).size, names.length, "each newly rendered result needs its own filename");
  });
}

test("download names preserve the available UUID API and an explicit stable result identity", () => {
  let calls = 0;
  const crypto = { randomUUID() {
    assert.equal(this, crypto, "the UUID method keeps its required receiver");
    calls += 1;
    return "abcdef12-3456-7890-abcd-ef1234567890";
  } };
  const { compositionFilename } = compile("download.ts", {}, { crypto });
  const when = new Date("2026-09-14T06:07:08.009Z");
  const first = compositionFilename("pair-v1", when);
  assert.equal(first, "FOTOHAVN-pair-2026-09-14T06-07-08-009Z-abcdef12.png");
  assert.equal(compositionFilename("pair-v1", when, "abcdef12-3456-7890-abcd-ef1234567890"), first);
  assert.equal(calls, 1, "reusing a result identity must not generate another one");
});


test("file drops target the actual template slot and are blocked during editing or composition", () => {
  const booth = fixture();
  const file = { name: "replacement.png", type: "image/png" };
  const target = { dataset: { photoPosition: "2" } };
  const event = () => ({ dataTransfer: { types: ["Files"], files: [file] },
    target: { closest: () => target }, currentTarget: { contains: () => true }, preventDefault() { this.prevented = true; } });
  let drop = event(); booth.dropSurface.onDrop(drop);
  assert.equal(drop.prevented, true); assert.equal(booth.fileDrops[0].index, 2);
  assert.equal(booth.fileDrops[0].files[0], file);
  booth.readiness(false); booth.render(); booth.dropSurface.onDrop(event());
  assert.equal(booth.fileDrops.length, 1);
  booth.readiness(true); booth.update({ type: "begin", mode: "retake", id: 10, target: 1 }); booth.render();
  booth.dropSurface.onDrop(event()); assert.equal(booth.fileDrops.length, 1);
  booth.update({ type: "cancel" }); booth.render();
  drop = event(); drop.target.closest = () => null; booth.dropSurface.onDrop(drop);
  assert.equal(booth.fileDrops[1].index, null, "dropping on mat fills empty slots");
});

test("horizontal previews rotate counterclockwise and keep interaction identities aligned", () => {
  const state = reduce(loaded(), { type: "template", id: "panorama-four" });
  const booth = fixture(state);
  assert.equal(booth.position(0).style.top, "75%", "original leftmost photograph becomes bottom");
  assert.equal(booth.position(3).style.top, "0%", "original rightmost photograph becomes top");
  assert.equal(booth.position(0).style.width, "100%");
  assert.equal(booth.position(0).style.height, "25%");
  booth.drag(0, 3);
  assert.deepEqual(plain(booth.state.slots), ["D", "B", "C", "A"]);
  assert.equal(booth.commits, 1);
});
