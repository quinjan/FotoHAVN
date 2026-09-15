import * as THREE from "three";
import { createBoothModel } from "./createBoothModel";

export type BoothView = "three-quarter" | "front" | "side" | "back" | "inside" | "bench";
export type BoothScene = ReturnType<typeof createBoothScene>;

/** Fit the complete cabinet in the available portrait canvas, with room to drag. */
export function portraitBoothDistance(
  bounds: THREE.Box3,
  view: { yaw: number; pitch: number; lookX: number; lookY: number; lookZ: number; fov: number },
  aspect: number,
) {
  const direction = new THREE.Vector3(
    Math.sin(view.yaw) * Math.cos(view.pitch), Math.sin(view.pitch),
    Math.cos(view.yaw) * Math.cos(view.pitch),
  );
  const right = new THREE.Vector3(Math.cos(view.yaw), 0, -Math.sin(view.yaw));
  const up = new THREE.Vector3().crossVectors(direction, right);
  const vertical = Math.tan(THREE.MathUtils.degToRad(view.fov / 2)) * 0.88;
  const horizontal = vertical * Math.max(0.1, aspect);
  let distance = 0;
  for (const x of [bounds.min.x, bounds.max.x]) {
    for (const y of [bounds.min.y, bounds.max.y]) {
      for (const z of [bounds.min.z, bounds.max.z]) {
        const point = new THREE.Vector3(x - view.lookX, y - view.lookY, z - view.lookZ);
        distance = Math.max(distance, point.dot(direction) + Math.max(
          Math.abs(point.dot(right)) / horizontal, Math.abs(point.dot(up)) / vertical,
        ));
      }
    }
  }
  return distance;
}

export function createBoothScene(
  host: HTMLDivElement,
  asset: (name: string) => string,
  onError: () => void,
  onInteract: () => void,
) {
  const cleanups: Array<() => void> = [];
  let frame = 0;
  let disposed = false;
  function dispose() {
    if (disposed) return;
    disposed = true;
    cancelAnimationFrame(frame);
    cleanups.reverse().forEach((cleanup) => cleanup());
  }
  try {
    const renderer = new THREE.WebGLRenderer({
      antialias: true,
      alpha: true,
      powerPreference: "low-power",
    });
    cleanups.push(() => {
      renderer.dispose();
      renderer.domElement.remove();
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 1.6));
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.25;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFShadowMap;
    const canvas = renderer.domElement;
    canvas.setAttribute("aria-hidden", "true");
    host.appendChild(canvas);
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(35, 1, 0.05, 40);
    const ambient = new THREE.HemisphereLight("#fff8e8", "#90735b", 2.0);
    scene.add(ambient);
    const key = new THREE.DirectionalLight("#fff6e3", 3.0);
    cleanups.push(() => key.shadow.dispose());
    key.position.set(-3.5, 5, 5);
    key.castShadow = true;
    key.shadow.mapSize.set(1024, 1024);
    key.shadow.camera.left = -3;
    key.shadow.camera.right = 3;
    key.shadow.camera.top = 4;
    key.shadow.camera.bottom = -3;
    key.shadow.normalBias = 0.025;
    scene.add(key);
    const fill = new THREE.DirectionalLight("#ffffff", 1.15);
    fill.position.set(4, 3, -2);
    scene.add(fill);
    const interiorLight = new THREE.PointLight("#fff5de", 2.8, 3);
    interiorLight.position.set(0.02, 2.1, 0.1);
    scene.add(interiorLight);
    const floor = new THREE.Mesh(
      new THREE.PlaneGeometry(30, 30),
      new THREE.ShadowMaterial({ opacity: 0.16 }),
    );
    cleanups.push(() => {
      floor.geometry.dispose();
      floor.material.dispose();
    });
    floor.rotation.x = -Math.PI / 2;
    floor.receiveShadow = true;
    floor.position.y = -0.006;
    scene.add(floor);

    const reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
    const portrait = window.matchMedia("(max-width: 767px) and (orientation: portrait)");
    const target = {
      yaw: -0.48,
      pitch: 0.16,
      distance: 6.65,
      curtain: 0,
      lookY: 1.35,
      lookX: 0,
      lookZ: 0,
      fov: 35,
    };
    const current = {
      ...target,
      yaw: reduce.matches ? target.yaw : -0.8,
      distance: reduce.matches ? target.distance : 7.1,
    };
    let visible = true;
    let inside = false;
    let previousTime = 0;
    let lastCurtain = -1;
    function invalidate() {
      if (!disposed && visible && !document.hidden && !frame)
        frame = requestAnimationFrame(render);
    }
    const model = createBoothModel(asset, invalidate, onError);
    cleanups.push(() => model.dispose());
    scene.add(model.group);
    const exteriorBounds = new THREE.Box3().setFromObject(model.group);

    // A simple daylight studio on a reflection-only layer gives the actual
    // mirror something to reflect without putting a room around the explorer.
    // These are surroundings, not a grid painted onto the mirror surface.
    const surroundings = new THREE.Group();
    surroundings.name = "mirror-surroundings";
    const roomGeometry = new THREE.BoxGeometry(18, 12, 18);
    const roomMaterial = new THREE.MeshBasicMaterial({ color: "#7d8783", side: THREE.BackSide });
    const room = new THREE.Mesh(roomGeometry, roomMaterial);
    room.position.y = 4;
    surroundings.add(room);
    const windowGeometry = new THREE.PlaneGeometry(1.55, 2.2);
    const windowMaterial = new THREE.MeshBasicMaterial({ color: "#dce5e5" });
    for (const x of [-7.2, -5.4, -3.6, -1.8, 0, 1.8, 3.6, 5.4, 7.2]) {
      for (const y of [1.8, 4.1]) {
        const pane = new THREE.Mesh(windowGeometry, windowMaterial);
        pane.rotation.y = Math.PI;
        pane.position.set(x, y, 8.9);
        surroundings.add(pane);
      }
    }
    surroundings.traverse((object) => object.layers.set(1));
    scene.add(surroundings);
    model.mirror.getReflectionCamera(camera).layers.enable(1);
    cleanups.push(() => {
      roomGeometry.dispose();
      roomMaterial.dispose();
      windowGeometry.dispose();
      windowMaterial.dispose();
    });
    function render(time: number) {
      frame = 0;
      if (disposed || !visible || document.hidden) return;
      const delta = Math.min((time - previousTime) / 1000 || 0.016, 0.05);
      previousTime = time;
      let moving = false;
      for (const key of Object.keys(target) as Array<keyof typeof target>) {
        current[key] = reduce.matches
          ? target[key]
          : THREE.MathUtils.damp(current[key], target[key], 9, delta);
        if (Math.abs(current[key] - target[key]) > 0.0001) moving = true;
      }
      const fit = inside ? 1 : Math.max(1, 0.95 / camera.aspect);
      const distance = portrait.matches && !inside
        ? portraitBoothDistance(exteriorBounds, current, camera.aspect) * current.distance / 6.65
        : current.distance * fit;
      // Preserve the useful horizontal view of the camera wall/bench without
      // moving the camera backwards through the cabinet on a tall screen.
      const fov = portrait.matches && inside
        ? THREE.MathUtils.radToDeg(2 * Math.atan(
          Math.tan(THREE.MathUtils.degToRad(current.fov / 2)) * Math.max(1, 1.25 / camera.aspect),
        ))
        : current.fov;
      if (camera.fov !== fov) {
        camera.fov = fov;
        camera.updateProjectionMatrix();
      }
      camera.position.set(
        current.lookX +
          Math.sin(current.yaw) * Math.cos(current.pitch) * distance,
        current.lookY + Math.sin(current.pitch) * distance,
        current.lookZ +
          Math.cos(current.yaw) * Math.cos(current.pitch) * distance,
      );
      camera.lookAt(current.lookX, current.lookY, current.lookZ);
      if (Math.abs(current.curtain - lastCurtain) > 0.0001) {
        model.setCurtain(current.curtain);
        lastCurtain = current.curtain;
      }
      try {
        renderer.render(scene, camera);
      } catch {
        onError();
        return;
      }
      if (moving) invalidate();
    }
    const resize = new ResizeObserver(() => {
      const { width, height } = host.getBoundingClientRect();
      if (!width || !height) return;
      camera.aspect = width / height;
      camera.updateProjectionMatrix();
      renderer.setSize(width, height);
      invalidate();
    });
    cleanups.push(() => resize.disconnect());
    resize.observe(host);
    const visibility = new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting;
      if (visible) invalidate();
      else {
        cancelAnimationFrame(frame);
        frame = 0;
      }
    });
    cleanups.push(() => visibility.disconnect());
    visibility.observe(host);
    const visibilityChanged = () => {
      if (document.hidden) {
        cancelAnimationFrame(frame);
        frame = 0;
      } else invalidate();
    };
    document.addEventListener("visibilitychange", visibilityChanged);
    cleanups.push(() =>
      document.removeEventListener("visibilitychange", visibilityChanged),
    );
    const reducedChanged = () => invalidate();
    reduce.addEventListener("change", reducedChanged);
    cleanups.push(() => reduce.removeEventListener("change", reducedChanged));
    portrait.addEventListener("change", reducedChanged);
    cleanups.push(() => portrait.removeEventListener("change", reducedChanged));

    let pointer: { id: number; x: number; y: number; moved: boolean } | null =
      null;
    function down(event: PointerEvent) {
      if (event.button !== 0 || !event.isPrimary) return;
      pointer = {
        id: event.pointerId,
        x: event.clientX,
        y: event.clientY,
        moved: false,
      };
      canvas.setPointerCapture(event.pointerId);
    }
    function move(event: PointerEvent) {
      if (!pointer || pointer.id !== event.pointerId) return;
      const dx = event.clientX - pointer.x,
        dy = event.clientY - pointer.y;
      if (!pointer.moved) {
        if (event.pointerType === "touch") {
          if (Math.abs(dy) >= 6 && Math.abs(dy) > Math.abs(dx)) {
            pointer = null;
            return;
          }
          if (Math.abs(dx) < 6 || Math.abs(dx) < Math.abs(dy) * 1.3) return;
        } else if (Math.abs(dx) + Math.abs(dy) < 4) return;
        onInteract();
        pointer.moved = true;
      }
      target.yaw -= dx * 0.009;
      if (event.pointerType !== "touch")
        target.pitch = THREE.MathUtils.clamp(
          target.pitch + dy * 0.005,
          -0.12,
          0.65,
        );
      pointer.x = event.clientX;
      pointer.y = event.clientY;
      invalidate();
    }
    function up() {
      pointer = null;
    }
    function rotate(direction: number) {
      target.yaw += direction * 0.28;
      onInteract();
      invalidate();
    }
    function zoom(direction: number) {
      target.distance = THREE.MathUtils.clamp(
        target.distance - direction * (inside ? 0.1 : 0.55),
        inside ? 0.65 : 3.8,
        8,
      );
      invalidate();
    }
    function keydown(event: KeyboardEvent) {
      if (event.target !== host) return;
      if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
        event.preventDefault();
        rotate(event.key === "ArrowLeft" ? -1 : 1);
      }
      if (event.key === "+" || event.key === "=") {
        event.preventDefault();
        zoom(1);
      }
      if (event.key === "-") {
        event.preventDefault();
        zoom(-1);
      }
      if (event.key === "ArrowUp" || event.key === "ArrowDown") {
        event.preventDefault();
        onInteract();
        target.pitch = THREE.MathUtils.clamp(
          target.pitch + (event.key === "ArrowUp" ? 0.08 : -0.08),
          -0.12,
          0.65,
        );
        invalidate();
      }
    }
    const lost = (event: Event) => {
      event.preventDefault();
      onError();
    };
    canvas.addEventListener("pointerdown", down);
    canvas.addEventListener("pointermove", move);
    canvas.addEventListener("pointerup", up);
    canvas.addEventListener("pointercancel", up);
    canvas.addEventListener("lostpointercapture", up);
    canvas.addEventListener("webglcontextlost", lost);
    host.addEventListener("keydown", keydown);
    cleanups.push(() => {
      canvas.removeEventListener("pointerdown", down);
      canvas.removeEventListener("pointermove", move);
      canvas.removeEventListener("pointerup", up);
      canvas.removeEventListener("pointercancel", up);
      canvas.removeEventListener("lostpointercapture", up);
      canvas.removeEventListener("webglcontextlost", lost);
      host.removeEventListener("keydown", keydown);
    });

    return {
      setView(view: BoothView) {
        const presets = {
          "three-quarter": {
            yaw: -0.48,
            pitch: 0.16,
            distance: 6.65,
            lookY: 1.35,
            lookX: 0,
            lookZ: 0,
          },
          front: {
            yaw: 0,
            pitch: 0.055,
            distance: 6.65,
            lookY: 1.35,
            lookX: 0,
            lookZ: 0,
          },
          side: {
            yaw: -1.2,
            pitch: 0.16,
            distance: 6.65,
            lookY: 1.35,
            lookX: 0,
            lookZ: 0,
          },
          back: {
            yaw: Math.PI + 0.35,
            pitch: 0.12,
            distance: 6.65,
            lookY: 1.35,
            lookX: 0,
            lookZ: 0,
          },
          inside: {
            yaw: 1.43,
            pitch: 0,
            distance: 1.65,
            lookY: 1.52,
            lookX: -0.87,
            lookZ: -0.04,
          },
          bench: {
            yaw: -1.43,
            pitch: 0.025,
            distance: 1.73,
            lookY: 1.26,
            lookX: 0.82,
            lookZ: 0,
          },
        };
        inside = view === "inside" || view === "bench";
        Object.assign(target, presets[view], { fov: view === "bench" ? 65 : 35 });
        invalidate();
      },
      setCurtain(open: boolean) {
        target.curtain = open ? 1 : 0;
        invalidate();
      },
      rotate,
      zoom,
      dispose,
    };
  } catch (error) {
    dispose();
    throw error;
  }
}
