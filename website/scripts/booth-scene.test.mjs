import { test } from "node:test";
import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";
import ts from "typescript";
import * as THREE from "three";
import { Reflector } from "three/addons/objects/Reflector.js";

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url));

// Run the actual TypeScript factories with real Three.js geometry and materials.
// Only browser/GPU boundaries are substituted so failure paths are reproducible.
function harness({ contextAvailable = true, reduced = true, portrait = false, width = 780, height = 625 } = {}) {
  const resources = new Set();
  const released = new Set();
  const renderers = [];
  const requests = [];
  const observers = [];
  const frames = new Map();
  let nextFrame = 0;
  class Element {
    rect = { width, height };
    children = [];
    events = new Map();
    setAttribute() {}
    appendChild(child) {
      this.children.push(child);
      child.parent = this;
    }
    remove() {
      if (this.parent)
        this.parent.children = this.parent.children.filter(
          (child) => child !== this,
        );
    }
    addEventListener(name, handler) {
      this.events.set(name, handler);
    }
    removeEventListener(name) {
      this.events.delete(name);
    }
    getBoundingClientRect() {
      return this.rect;
    }
    setPointerCapture() {}
    getContext() {
      return contextAvailable ? { fillRect() {}, fillText() {} } : null;
    }
  }
  const document = Object.assign(new Element(), {
    hidden: false,
    createElement: () => new Element(),
  });
  const media = Object.assign(new Element(), { matches: reduced });
  const portraitMedia = Object.assign(new Element(), { matches: portrait });
  const host = new Element();
  function track(Class) {
    return class extends Class {
      constructor(...args) {
        super(...args);
        resources.add(this);
        this.addEventListener("dispose", () => released.add(this));
      }
    };
  }
  class Renderer {
    domElement = new Element();
    shadowMap = {};
    disposed = 0;
    constructor() {
      renderers.push(this);
    }
    setPixelRatio() {}
    setSize() {}
    render(scene, camera) {
      this.camera = camera.position.clone();
      this.viewCamera = camera.clone();
      this.viewCamera.updateMatrixWorld(true);
      this.scene = scene;
    }
    dispose() {
      this.disposed++;
    }
  }
  class Observer {
    disconnected = false;
    constructor(callback) {
      this.callback = callback;
      observers.push(this);
    }
    observe() {
      this.callback([{ isIntersecting: true }]);
    }
    disconnect() {
      this.disconnected = true;
    }
  }
  const fakeThree = {
    ...THREE,
    WebGLRenderer: Renderer,
    TextureLoader: class {
      load(_url, onLoad, _progress, onError) {
        const value = new THREE.Texture();
        resources.add(value);
        value.addEventListener("dispose", () => released.add(value));
        requests.push({ onLoad, onError });
        return value;
      }
    },
  };
  for (const name of [
    "BoxGeometry",
    "PlaneGeometry",
    "CylinderGeometry",
    "CircleGeometry",
    "MeshStandardMaterial",
    "MeshBasicMaterial",
    "MeshPhysicalMaterial",
    "ShadowMaterial",
    "CanvasTexture",
  ])
    fakeThree[name] = track(THREE[name]);
  class TrackedReflector extends Reflector {
    constructor(...args) {
      super(...args);
      for (const resource of [this.material, this.getRenderTarget()]) {
        resources.add(resource);
        resource.addEventListener("dispose", () => released.add(resource));
      }
    }
  }
  const common = {
    document,
    window: { devicePixelRatio: 2, matchMedia: (query) => query.includes("prefers-reduced-motion") ? media : portraitMedia },
    ResizeObserver: Observer,
    IntersectionObserver: Observer,
    requestAnimationFrame: (callback) => {
      frames.set(++nextFrame, callback);
      return nextFrame;
    },
    cancelAnimationFrame: (id) => frames.delete(id),
  };
  function load(name) {
    const filename = path.resolve(
      scriptDirectory,
      "../src/components/booth",
      `${name}.ts`,
    );
    const code = ts.transpileModule(fs.readFileSync(filename, "utf8"), {
      compilerOptions: {
        module: ts.ModuleKind.CommonJS,
        target: ts.ScriptTarget.ES2022,
      },
    }).outputText;
    const compiledModule = { exports: {} };
    vm.runInNewContext(
      code,
      {
        ...common,
        module: compiledModule,
        exports: compiledModule.exports,
        require: (id) => {
          if (id === "three") return fakeThree;
          if (id === "three/addons/objects/Reflector.js") return { Reflector: TrackedReflector };
          if (id === "./createBoothModel") return load("createBoothModel");
          throw new Error(`Unexpected test import: ${id}`);
        },
      },
      { filename },
    );
    return compiledModule.exports;
  }
  function flush() {
    for (let i = 0; i < 240 && frames.size; i++) {
      const batch = [...frames.values()];
      frames.clear();
      batch.forEach((callback) => callback((i + 1) * 17));
    }
  }
  return {
    ...load("createBoothScene"),
    ...load("createBoothModel"),
    host,
    document,
    media,
    portraitMedia,
    resources,
    released,
    renderers,
    requests,
    observers,
    frames,
    flush,
  };
}

test("normal disposal releases the model, renderer, observers and listeners exactly once", () => {
  const h = harness();
  const scene = h.createBoothScene(
    h.host,
    (name) => name,
    () => {},
    () => {},
  );
  h.flush();
  assert.equal(h.host.children.length, 1);
  scene.dispose();
  scene.dispose();
  assert.equal(h.host.children.length, 0);
  assert.equal(h.renderers[0].disposed, 1);
  assert.equal(
    h.host.events.size + h.document.events.size + h.media.events.size + h.portraitMedia.events.size,
    0,
  );
  assert.ok(h.observers.every((observer) => observer.disconnected));
  assert.equal(h.frames.size, 0);
  assert.equal(h.released.size, h.resources.size);
});

test("right panel is an opaque planar mirror with a solid walnut back", () => {
  const h = harness();
  const model = h.createBoothModel((name) => name, () => {}, () => {});
  const mirror = model.group.getObjectByName("front-right-mirror");
  const backing = model.group.getObjectByName("mirror-backing");
  assert.ok(mirror instanceof Reflector);
  assert.equal(mirror.material.transparent, false);
  assert.equal(mirror.material.opacity, 1);
  assert.equal(mirror.material.depthWrite, true);
  assert.equal(backing.material.transparent, false);
  assert.ok(mirror.position.z > backing.position.z + backing.geometry.parameters.depth / 2);
  let renderTargetDisposals = 0;
  mirror.getRenderTarget().addEventListener("dispose", () => renderTargetDisposals++);
  model.dispose();
  model.dispose();
  assert.equal(renderTargetDisposals, 1);
  assert.equal(h.released.size, h.resources.size);
});

test("camera and monitor are separate in walnut with symmetric LED bars", () => {
  const h = harness();
  const model = h.createBoothModel((name) => name, () => {}, () => {});
  const mesh = (name) => model.group.getObjectByName(name);
  const camera = mesh("camera-aperture");
  const screen = mesh("monitor-screen");
  const bezel = mesh("monitor-bezel");
  const left = mesh("monitor-led-left");
  const right = mesh("monitor-led-right");
  assert.ok(camera.position.y - camera.geometry.parameters.radiusTop > bezel.position.y + bezel.geometry.parameters.height / 2 + 0.15);
  assert.ok(left.position.z > screen.position.z && right.position.z < screen.position.z);
  assert.ok(Math.abs((left.position.z + right.position.z) / 2 - screen.position.z) < 0.0001);
  assert.equal(left.position.y, right.position.y);
  assert.equal(left.material, right.material);
  assert.ok(left.material.emissiveIntensity > 0);
  // A ray into the space between camera and screen must hit wood, not a console.
  model.group.updateMatrixWorld(true);
  const ray = new THREE.Raycaster(new THREE.Vector3(0, 1.57, -0.04), new THREE.Vector3(-1, 0, 0));
  const hit = ray.intersectObject(model.group, true)[0];
  assert.equal(hit.object.name, "capture-wall");
  model.dispose();
});

test("right-wall bench uses the booth wood with a fixed curtain above it", () => {
  const h = harness();
  const model = h.createBoothModel((name) => name, () => {}, () => {});
  const bench = model.group.getObjectByName("bench-seat");
  const wall = model.group.getObjectByName("capture-wall");
  const backdrop = model.group.getObjectByName("portrait-backdrop");
  const bounds = new THREE.Box3().setFromObject(backdrop);
  assert.ok(bench.position.x > 0.5);
  assert.ok(bench.geometry.parameters.depth > bench.geometry.parameters.width * 2);
  assert.equal(bench.material, wall.material);
  assert.ok(bounds.min.x > bench.position.x);
  assert.ok(bounds.min.y > bench.position.y + bench.geometry.parameters.height / 2);
  const before = [...backdrop.geometry.attributes.position.array];
  model.setCurtain(1);
  assert.deepEqual([...backdrop.geometry.attributes.position.array], before);
  model.dispose();
});

test("front and rear curtains share pleats and open through unobstructed framed doorways", () => {
  const h = harness();
  const model = h.createBoothModel((name) => name, () => {}, () => {});
  const front = model.group.getObjectByName("front-curtain");
  const rear = model.group.getObjectByName("rear-curtain");
  assert.equal(front.material, rear.material);
  for (const amount of [0, 0.5, 1]) {
    model.setCurtain(amount);
    const a = front.geometry.attributes.position;
    const b = rear.geometry.attributes.position;
    for (let i = 0; i < a.count; i++) {
      assert.ok(Math.abs(a.getY(i) - b.getY(i)) < 0.00001);
      assert.ok(Math.abs(a.getZ(i) + b.getZ(i)) < 0.00001);
      assert.ok(Math.abs(a.getX(i) + b.getX(i) - 0.05) < 0.00001);
    }
  }
  model.group.updateMatrixWorld(true);
  // The opened entrances leave a clear front-to-back passage at torso height.
  const ray = new THREE.Raycaster(new THREE.Vector3(0, 1.3, 2), new THREE.Vector3(0, 0, -1));
  assert.equal(ray.intersectObject(model.group, true).length, 0);
  assert.ok(model.group.getObjectByName("rear-left-panel"));
  assert.ok(model.group.getObjectByName("rear-right-panel"));
  model.dispose();
});

test("back and bench presets expose the corrected layout; reflection scenery stays off the main layer", () => {
  const h = harness();
  const controller = h.createBoothScene(h.host, (name) => name, () => {}, () => {});
  controller.setView("back");
  h.flush();
  assert.ok(h.renderers[0].camera.z < -5);
  controller.setView("bench");
  h.flush();
  assert.ok(h.renderers[0].camera.x < -0.7 && h.renderers[0].camera.x > -1);
  const scene = h.renderers[0].scene;
  const bench = scene.getObjectByName("bench-seat");
  const projectedSeat = bench.position.clone().project(h.renderers[0].viewCamera);
  assert.ok(Math.abs(projectedSeat.y) < 0.85, "the Bench preset must show the seat, not only the backdrop");
  const mainCamera = new THREE.PerspectiveCamera();
  scene.getObjectByName("mirror-surroundings").traverse((object) => {
    assert.equal(object.layers.test(mainCamera.layers), false);
  });
  controller.dispose();
});

test("partial construction failure releases all allocations before throwing", () => {
  const h = harness({ contextAvailable: false });
  assert.throws(
    () =>
      h.createBoothScene(
        h.host,
        (name) => name,
        () => {},
        () => {},
      ),
    /texture canvas is unavailable/,
  );
  assert.equal(h.host.children.length, 0);
  assert.equal(h.renderers[0].disposed, 1);
  assert.equal(h.released.size, h.resources.size);
});

test("texture failures reach the photograph fallback once and late callbacks are ignored", () => {
  const h = harness();
  let failures = 0;
  const scene = h.createBoothScene(
    h.host,
    (name) => name,
    () => {
      failures++;
      scene.dispose();
    },
    () => {},
  );
  h.requests[0].onError();
  h.requests[1].onError();
  h.requests[1].onLoad();
  assert.equal(failures, 1);
  assert.equal(h.host.children.length, 0);
  assert.equal(h.frames.size, 0);
});

test("keyboard tilt marks a custom view and the inside preset enters the cabinet", () => {
  const h = harness();
  let interactions = 0;
  const scene = h.createBoothScene(
    h.host,
    (name) => name,
    () => {},
    () => interactions++,
  );
  h.host.events.get("keydown")({
    target: h.host,
    key: "ArrowUp",
    preventDefault() {},
  });
  assert.equal(interactions, 1);
  scene.setView("inside");
  scene.setCurtain(true);
  h.flush();
  assert.ok(h.renderers[0].camera.x > -1 && h.renderers[0].camera.x < 1);
  assert.ok(h.renderers[0].camera.z < 0.75);
  assert.equal(
    h.frames.size,
    0,
    "reduced motion settles without an ongoing animation loop",
  );
  scene.dispose();
});

test("portrait exterior presets frame the entire cabinet at readable scale", () => {
  for (const width of [305, 375, 415]) {
    const h = harness({ portrait: true, width, height: 480 });
    const controller = h.createBoothScene(h.host, name => name, () => {}, () => {});
    for (const view of ["three-quarter", "front", "side", "back"]) {
      controller.setView(view);
      h.flush();
      const renderer = h.renderers[0];
      const cabinet = renderer.scene.children.find(object => object.type === "Group" && object.name !== "mirror-surroundings");
      const bounds = new THREE.Box3().setFromObject(cabinet);
      let extent = 0;
      for (const x of [bounds.min.x, bounds.max.x]) {
        for (const y of [bounds.min.y, bounds.max.y]) {
          for (const z of [bounds.min.z, bounds.max.z]) {
            const point = new THREE.Vector3(x, y, z).project(renderer.viewCamera);
            assert.ok(Math.abs(point.x) <= 0.881 && Math.abs(point.y) <= 0.881,
              `${width}px ${view}: cabinet clipped at ${point.toArray()}`);
            assert.ok(point.z > -1 && point.z < 1);
            extent = Math.max(extent, Math.abs(point.x), Math.abs(point.y));
          }
        }
      }
      assert.ok(extent >= 0.87, `${width}px ${view}: excess empty space makes the booth too small`);
    }
    controller.dispose();
  }
});

test("portrait interior presets retain their position and show camera lights and bench", () => {
  const h = harness({ portrait: true, width: 305, height: 480 });
  const controller = h.createBoothScene(h.host, name => name, () => {}, () => {});
  for (const view of ["inside", "bench"]) {
    controller.setView(view);
    controller.setCurtain(true);
    h.flush();
    const renderer = h.renderers[0];
    const portraitPosition = renderer.camera.clone();
    const subjects = view === "inside" ? ["camera-aperture", "monitor-screen", "monitor-led-left", "monitor-led-right"] : ["bench-seat"];
    for (const name of subjects) {
      const point = renderer.scene.getObjectByName(name).getWorldPosition(new THREE.Vector3()).project(renderer.viewCamera);
      assert.ok(Math.abs(point.x) < 0.9 && Math.abs(point.y) < 0.9, `${view}: ${name} is outside the portrait view`);
    }
    const resourceCount = h.resources.size;
    h.portraitMedia.matches = false;
    h.portraitMedia.events.get("change")();
    h.flush();
    assert.ok(portraitPosition.distanceTo(renderer.camera) < 0.0001, "orientation must not move the interior camera through the cabinet");
    assert.equal(h.resources.size, resourceCount, "orientation must not rebuild the model");
    assert.equal(renderer.viewCamera.fov, view === "bench" ? 65 : 35);
    h.portraitMedia.matches = true;
    h.portraitMedia.events.get("change")();
  }
  controller.dispose();
});

test("vertical touch intent scrolls without rotating, horizontal intent rotates, and cancellation clears drag", () => {
  const h = harness({ portrait: true, width: 375, height: 480 });
  let interactions = 0;
  const controller = h.createBoothScene(h.host, name => name, () => {}, () => interactions++);
  h.flush();
  const initialCamera = h.renderers[0].camera.clone();
  const events = h.renderers[0].domElement.events;
  const event = (x, y) => ({ button: 0, isPrimary: true, pointerId: 1, pointerType: "touch", clientX: x, clientY: y });
  events.get("pointerdown")(event(100, 100));
  events.get("pointermove")(event(102, 150));
  h.flush();
  assert.equal(interactions, 0);
  assert.equal(initialCamera.distanceTo(h.renderers[0].camera), 0);
  events.get("pointerdown")(event(100, 100));
  events.get("pointermove")(event(165, 102));
  h.flush();
  assert.equal(interactions, 1);
  assert.ok(initialCamera.distanceTo(h.renderers[0].camera) > 0.1);
  events.get("lostpointercapture")();
  const releasedCamera = h.renderers[0].camera.clone();
  events.get("pointermove")(event(230, 102));
  h.flush();
  assert.equal(releasedCamera.distanceTo(h.renderers[0].camera), 0);
  controller.dispose();
});
