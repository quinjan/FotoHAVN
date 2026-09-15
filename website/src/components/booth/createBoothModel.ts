import * as THREE from "three";
import { Reflector } from "three/addons/objects/Reflector.js";

/** Visual proportions from the Evia photographs, not surveyed dimensions. */
export function createBoothModel(
  asset: (name: string) => string,
  invalidate: () => void,
  onError: () => void,
) {
  const group = new THREE.Group();
  const textures = new Set<THREE.Texture>();
  let disposed = false;
  const materials = new Set<THREE.Material>();
  function dispose() {
    if (disposed) return;
    disposed = true;
    group.traverse((item) => {
      if (item instanceof THREE.Mesh) {
        item.geometry.dispose();
        if (item instanceof Reflector) {
          item.dispose();
          return;
        }
        const list = Array.isArray(item.material)
          ? item.material
          : [item.material];
        list.forEach((material) => materials.add(material));
      }
    });
    materials.forEach((material) => material.dispose());
    textures.forEach((map) => map.dispose());
  }
  try {
    const loader = new THREE.TextureLoader();
    function texture(name: string) {
      const result = loader.load(
        asset(name),
        () => {
          if (!disposed) invalidate();
        },
        undefined,
        () => {
          if (!disposed) onError();
        },
      );
      result.colorSpace = THREE.SRGBColorSpace;
      result.anisotropy = 4;
      textures.add(result);
      return result;
    }
    const grain = texture("booth-wood");
    const wood = new THREE.MeshStandardMaterial({
      color: "#b39478",
      map: grain,
      roughness: 0.72,
    });
    const edge = new THREE.MeshStandardMaterial({
      color: "#503527",
      roughness: 0.55,
    });
    const recess = new THREE.MeshStandardMaterial({
      color: "#241a14",
      roughness: 0.9,
    });
    const brass = new THREE.MeshStandardMaterial({
      color: "#b39a78",
      metalness: 0.58,
      roughness: 0.38,
    });
    const fabric = new THREE.MeshStandardMaterial({
      color: "#e8dfc9",
      side: THREE.DoubleSide,
      roughness: 1,
    });
    [wood, edge, recess, brass, fabric].forEach((material) =>
      materials.add(material),
    );

    function box(
      w: number,
      h: number,
      d: number,
      x: number,
      y: number,
      z: number,
      material: THREE.Material = wood,
    ) {
      const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
      mesh.position.set(x, y, z);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      group.add(mesh);
      return mesh;
    }
    function label(
      text: string,
      sub: string,
      width: number,
      height: number,
      glow = false,
    ) {
      const canvas = document.createElement("canvas");
      canvas.width = 1024;
      canvas.height = Math.round((1024 * height) / width);
      const context = canvas.getContext("2d");
      if (!context) throw new Error("Booth texture canvas is unavailable");
      context.fillStyle = glow ? "#fff1bd" : "#d5c8b4";
      context.fillRect(0, 0, canvas.width, canvas.height);
      context.fillStyle = "#322316";
      context.textAlign = "center";
      context.textBaseline = "middle";
      context.font = `${glow ? 116 : 145}px Georgia, serif`;
      context.fillText(text, 512, canvas.height * (sub ? 0.35 : 0.53), 950);
      if (sub) {
        context.font = "65px Georgia, serif";
        context.fillText(sub, 512, canvas.height * 0.7, 940);
      }
      const map = new THREE.CanvasTexture(canvas);
      map.colorSpace = THREE.SRGBColorSpace;
      textures.add(map);
      const material = new THREE.MeshStandardMaterial({
        map,
        roughness: 0.5,
        emissiveMap: glow ? map : null,
        emissive: glow ? "#fff0c3" : "#000000",
        emissiveIntensity: glow ? 0.5 : 0,
      });
      materials.add(material);
      return material;
    }

    // Coordinates: front +Z, capture wall -X, guest bench/backdrop +X.
    box(2.24, 0.14, 1.64, 0, 0.07, 0, edge);
    box(2.1, 0.045, 1.5, 0, 0.155, 0);
    box(0.07, 2.22, 1.5, -1.065, 1.29, 0).name = "capture-wall";
    box(0.07, 2.22, 1.5, 1.065, 1.29, 0).name = "seating-wall";
    box(2.3, 0.1, 1.72, 0, 2.45, 0, edge);
    box(2.22, 0.08, 1.62, 0, 2.36, 0);
    // Front and back have the same framed central opening. No rear crossbars
    // or solid wall may bridge that opening behind the curtain.
    for (const facing of [1, -1]) {
      const face = facing === 1 ? "front" : "rear";
      for (const x of [-1.07, -0.43, 0.48, 1.07])
        box(0.075, 2.24, 0.11, x, 1.28, facing * 0.77, edge).name = `${face}-stile-${x}`;
      for (const y of [0.22, 1.08, 2.29]) {
        box(0.59, 0.045, 0.045, -0.75, y, facing * 0.8, edge);
        box(0.53, 0.045, 0.045, 0.775, y, facing * 0.8, edge);
      }
    }
    box(0.58, 2.15, 0.065, -0.75, 1.26, -0.745).name = "rear-left-panel";
    box(0.52, 2.15, 0.065, 0.775, 1.26, -0.745).name = "rear-right-panel";
    box(0.58, 2.15, 0.065, -0.75, 1.26, 0.745);
    box(0.52, 0.84, 0.065, 0.775, 0.6, 0.745);
    // Raised side/rear framing gives the real wooden cabinet its depth.
    for (const x of [-1.105, 1.105]) {
      for (const z of [-0.7, 0, 0.7]) box(0.035, 2.17, 0.045, x, 1.27, z, edge);
      for (const y of [0.23, 1.08, 2.29])
        box(0.035, 0.045, 1.42, x, y, 0, edge);
    }

    // Warm PHOTOBOOTH lightbox, exactly the wording in the source photographs.
    box(2.03, 0.34, 0.16, 0, 2.67, 0.65, edge);
    const signMaterial = label("PHOTOBOOTH", "", 1.95, 0.275, true);
    const sign = new THREE.Mesh(
      new THREE.PlaneGeometry(1.95, 0.275),
      signMaterial,
    );
    sign.position.set(0, 2.67, 0.737);
    group.add(sign);

    // Opaque, walnut-backed mirror, not a window into the booth.
    box(0.49, 1.13, 0.045, 0.775, 1.7, 0.758).name = "mirror-backing";
    const mirror = new Reflector(new THREE.PlaneGeometry(0.49, 1.13), {
      color: 0xaaaaa5,
      textureWidth: 256,
      textureHeight: 512,
      clipBias: 0.003,
      multisample: 0,
    });
    mirror.name = "front-right-mirror";
    mirror.position.set(0.775, 1.7, 0.783);
    group.add(mirror);
    box(0.59, 0.055, 0.16, 0.775, 1.1, 0.78, edge);

    // Actual photo-board texture and recessed print hatch.
    box(0.49, 0.65, 0.025, -0.75, 1.78, 0.799, brass);
    const board = new THREE.Mesh(
      new THREE.PlaneGeometry(0.455, 0.605),
      new THREE.MeshStandardMaterial({
        map: texture("booth-photo-board"),
        roughness: 0.8,
      }),
    );
    board.position.set(-0.75, 1.78, 0.817);
    group.add(board);
    box(0.4, 0.2, 0.055, -0.75, 0.76, 0.791, recess);
    for (const x of [-0.965, -0.535])
      box(0.036, 0.26, 0.075, x, 0.76, 0.814, edge);
    for (const y of [0.64, 0.88])
      box(0.43, 0.032, 0.075, -0.75, y, 0.814, edge);
    box(0.38, 0.018, 0.11, -0.75, 0.673, 0.85, wood);
    const plaqueMaterial = label("PHOTOS", "delivered here", 0.23, 0.17);
    const plaque = new THREE.Mesh(
      new THREE.PlaneGeometry(0.23, 0.17),
      plaqueMaterial,
    );
    plaque.position.set(-0.75, 1.0, 0.814);
    group.add(plaque);

    // Right-wall walnut bench faces the separate camera and monitor on the left.
    box(0.43, 0.12, 1.22, 0.785, 0.56, 0, wood).name = "bench-seat";
    for (const z of [-0.49, 0.49])
      box(0.34, 0.325, 0.075, 0.8, 0.34, z, wood).name = `bench-leg-${z}`;
    // Only the monitor has a small bezel; uninterrupted booth wood surrounds
    // this module and the separate circular camera aperture above it.
    box(0.024, 0.27, 0.36, -1.014, 1.35, -0.04, recess).name = "monitor-bezel";
    const screenMaterial = label("FOTOHAVN", "Step inside.", 0.32, 0.23, true);
    const screen = new THREE.Mesh(
      new THREE.PlaneGeometry(0.32, 0.23),
      screenMaterial,
    );
    screen.rotation.y = Math.PI / 2;
    screen.position.set(-0.999, 1.35, -0.04);
    screen.name = "monitor-screen";
    group.add(screen);
    const lens = new THREE.Mesh(
      new THREE.CylinderGeometry(0.055, 0.055, 0.048, 24),
      recess,
    );
    lens.rotation.z = Math.PI / 2;
    lens.position.set(-1.012, 1.73, -0.04);
    lens.name = "camera-aperture";
    group.add(lens);
    const lensGlass = new THREE.Mesh(
      new THREE.CircleGeometry(0.034, 24),
      new THREE.MeshStandardMaterial({
        color: "#52625e",
        metalness: 0.55,
        roughness: 0.1,
      }),
    );
    lensGlass.rotation.y = Math.PI / 2;
    lensGlass.position.set(-0.986, 1.73, -0.04);
    group.add(lensGlass);
    const led = new THREE.MeshStandardMaterial({
      color: "#fff6d5",
      emissive: "#ffefb2",
      emissiveIntensity: 0.75,
    });
    materials.add(led);
    for (const [side, z] of [["left", 0.26], ["right", -0.34]] as const) {
      box(0.022, 0.58, 0.055, -1.014, 1.55, z, brass);
      box(0.023, 0.54, 0.038, -0.991, 1.55, z, led).name = `monitor-led-${side}`;
    }

    // Identical pleats and hem on both entrances, gathered to the viewer's right.
    function entranceCurtain(facing: number) {
      const curtainGeometry = new THREE.PlaneGeometry(0.83, 1.91, 80, 24);
      const curtain = new THREE.Mesh(curtainGeometry, fabric);
      curtain.name = facing === 1 ? "front-curtain" : "rear-curtain";
      curtain.castShadow = true;
      curtain.receiveShadow = true;
      group.add(curtain);
      const rod = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 0.88, 12), brass);
      rod.rotation.z = Math.PI / 2;
      rod.position.set(0.025, 2.285, facing * 0.78);
      group.add(rod);
      return { geometry: curtainGeometry, facing };
    }
    const entrances = [entranceCurtain(1), entranceCurtain(-1)];
    function setCurtain(amount: number) {
      amount = THREE.MathUtils.clamp(amount, 0, 1);
      const width = THREE.MathUtils.lerp(0.83, 0.13, amount);
      const left = 0.44 - width;
      for (const { geometry, facing } of entrances) {
        const positions = geometry.attributes.position;
        for (let i = 0; i < positions.count; i++) {
          const u = (i % 81) / 80;
          const v = Math.floor(i / 81) / 24;
          positions.setXYZ(
            i,
            0.025 + facing * (left + u * width - 0.025),
            2.26 - v * 1.91 + Math.sin(u * Math.PI * 8) * 0.012 * v,
            facing * (0.786 +
            Math.sin(u * Math.PI * 20) * 0.035 -
            Math.sin(v * Math.PI) * 0.018),
          );
        }
        positions.needsUpdate = true;
        geometry.computeVertexNormals();
        geometry.computeBoundingSphere();
      }
    }
    setCurtain(0);

    // Fixed portrait backdrop: along the right wall, above/behind the bench.
    // It never opens with the two entry curtains.
    const backdropGeometry = new THREE.PlaneGeometry(1.3, 1.61, 80, 24);
    const backdropPositions = backdropGeometry.attributes.position;
    for (let i = 0; i < backdropPositions.count; i++) {
      const u = (i % 81) / 80;
      const v = Math.floor(i / 81) / 24;
      backdropPositions.setXYZ(i,
        0.984 + Math.sin(u * Math.PI * 20) * 0.025 - Math.sin(v * Math.PI) * 0.012,
        2.26 - v * 1.61 + Math.sin(u * Math.PI * 8) * 0.01 * v,
        -0.65 + u * 1.3,
      );
    }
    backdropGeometry.computeVertexNormals();
    const backdrop = new THREE.Mesh(backdropGeometry, fabric);
    backdrop.name = "portrait-backdrop";
    backdrop.castShadow = true;
    backdrop.receiveShadow = true;
    group.add(backdrop);
    const backdropRod = new THREE.Mesh(new THREE.CylinderGeometry(0.012, 0.012, 1.34, 12), brass);
    backdropRod.rotation.x = Math.PI / 2;
    backdropRod.position.set(0.984, 2.285, 0);
    group.add(backdropRod);

    return {
      group,
      mirror,
      setCurtain,
      dispose,
    };
  } catch (error) {
    dispose();
    throw error;
  }
}
