// The preview shares one renderer between the hub and three adventure worlds.
import * as T from './vendor/three.module.js';
import { buildEnvironments } from './preview-environments.mjs';
import { dressMuseum, marbleTexture, finishArchitecture } from './art.mjs';
import { buildDoors } from './doors.mjs';
import { buildMim } from './mim.mjs';
import { pedestal, compass, scales, catStatue, dinosaur } from './exhibits.mjs';
import { Reflector } from './vendor/addons/objects/Reflector.js';
import { lightMuseum, cinematicPipeline } from './lighting.mjs';
import {
  buildAdventureMechanism,
  buildRestoration,
} from './preview-mechanisms.mjs';

const colors = {
  stone: 0x96816d,
  light: 0xc5ae8e,
  navy: 0x102c48,
  teal: 0x168d91,
  cream: 0xf2dfbf,
  gold: 0xcfaa59,
};
const material = (color, options = {}) =>
  new T.MeshStandardMaterial({ color, roughness: 0.65, ...options });
const materials = Object.fromEntries(
  Object.entries(colors).map(([key, color]) => [key, material(color)])
);
materials.gold.metalness = 0.7;
materials.gold.roughness = 0.26;
materials.cream.roughness = 0.82;
const limestone = new T.TextureLoader().load('./assets/limestone.png');
limestone.colorSpace = T.SRGBColorSpace;
limestone.wrapS = limestone.wrapT = T.RepeatWrapping;
limestone.repeat.set(2, 2);
materials.stone.map = limestone;
materials.light.map = limestone;
materials.navy.roughness = 0.4;
materials.navy.metalness = 0.22;
const emissive = material(0x31c9c4, {
  emissive: 0x13988c,
  emissiveIntensity: 1.3,
  roughness: 0.3,
});

function mesh(parent, geometry, surface, position = [0, 0, 0]) {
  const object = new T.Mesh(geometry, surface);
  object.position.set(...position);
  object.castShadow = true;
  object.receiveShadow = true;
  parent.add(object);
  return object;
}
function box(parent, dimensions, surface, position) {
  return mesh(parent, new T.BoxGeometry(...dimensions), surface, position);
}
function sphere(parent, dimensions, surface, position) {
  const shape = mesh(
    parent,
    new T.SphereGeometry(1, 32, 20),
    surface,
    position
  );
  shape.scale.set(...dimensions);
  return shape;
}
function cylinder(parent, radii, surface, position) {
  return mesh(parent, new T.CylinderGeometry(...radii, 32), surface, position);
}

function column(parent, x, z) {
  box(parent, [1.3, 0.22, 1.3], materials.light, [x, 0.11, z]);
  cylinder(parent, [0.41, 0.48, 6.3], materials.stone, [x, 3.3, z]);
  for (let index = 0; index < 12; index++) {
    const angle = (index * Math.PI) / 6;
    cylinder(parent, [0.035, 0.045, 5.1], materials.light, [
      x + Math.cos(angle) * 0.443,
      3.2,
      z + Math.sin(angle) * 0.443,
    ]);
  }
  for (const y of [0.45, 5.95, 6.25])
    cylinder(parent, [0.55, 0.55, 0.18], materials.gold, [x, y, z]);
  box(parent, [1.25, 0.35, 1.25], materials.light, [x, 6.55, z]);
}

function label(parent, text, position, width = 3) {
  const canvas = document.createElement('canvas');
  canvas.width = 768;
  canvas.height = 192;
  const context = canvas.getContext('2d');
  context.fillStyle = '#102c48';
  context.fillRect(0, 0, 768, 192);
  context.strokeStyle = '#c9aa68';
  context.lineWidth = 8;
  context.strokeRect(5, 5, 758, 182);
  context.fillStyle = '#f6e6cb';
  context.font = 'bold 66px Tahoma';
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText(text, 384, 100);
  const texture = new T.CanvasTexture(canvas);
  texture.colorSpace = T.SRGBColorSpace;
  return mesh(
    parent,
    new T.PlaneGeometry(width, width / 4),
    new T.MeshBasicMaterial({ map: texture }),
    position
  );
}

function relic(parent, index, position, title) {
  const group = new T.Group();
  group.position.set(...position);
  parent.add(group);
  pedestal(group, materials);
  [compass, scales, catStatue][index](group, materials);
  const ring = mesh(
    group,
    new T.TorusGeometry(0.95, 0.016, 8, 64),
    emissive,
    [0, 2.6, -0.15]
  );
  ring.userData.titleSign = label(group, title, [0, 0.85, 0.977], 1.75);
  ring.userData.title = title;
  return ring;
}

function museum(scene, content) {
  box(
    scene,
    [30, 0.45, 30],
    material(0x716654, { roughness: 0.23, metalness: 0.22 }),
    [0, -0.24, 1]
  );
  const floor = mesh(
    scene,
    new T.PlaneGeometry(30, 30),
    material(0xafa18d, {
      map: marbleTexture(),
      roughness: 0.24,
      metalness: 0.1,
      transparent: true,
      opacity: 0.82,
      depthWrite: false,
    }),
    [0, 0.01, 1]
  );
  floor.rotation.x = -Math.PI / 2;
  const reflection = new Reflector(new T.PlaneGeometry(30, 30), {
    color: 0x626665,
    textureWidth: 768,
    textureHeight: 768,
    clipBias: 0.003,
  });
  reflection.rotation.x = -Math.PI / 2;
  reflection.position.set(0, 0, 1);
  scene.add(reflection);
  for (const x of [-9.5, 9.5])
    box(scene, [11, 10, 0.7], materials.stone, [x, 5, -12]);
  box(scene, [8, 1.6, 0.7], materials.stone, [0, 9.2, -12]);
  box(scene, [8, 0.3, 10], materials.light, [0, -0.16, -16]);
  for (const x of [-4, 4])
    box(scene, [0.5, 9, 10], materials.stone, [x, 4.5, -16]);
  box(scene, [8, 0.3, 10], materials.stone, [0, 9, -16]);
  box(
    scene,
    [8, 9, 0.3],
    material(0xbd9860, { emissive: 0x8c642c, emissiveIntensity: 0.65 }),
    [0, 4.5, -21]
  );
  for (const z of [-14, -17, -20])
    for (const x of [-3.6, 3.6])
      cylinder(scene, [0.17, 0.22, 5.8], materials.gold, [x, 2.9, z]);
  const exitLight = new T.PointLight(0xffd294, 65, 14, 1.7);
  exitLight.position.set(0, 5, -16);
  scene.add(exitLight);
  for (const x of [-14, 14])
    box(scene, [0.6, 10, 27], materials.stone, [x, 5, 1]);
  for (const x of [-12, -8, 8, 12]) column(scene, x, -10.7);
  for (const x of [-12, 12]) for (const z of [-3, 5, 12]) column(scene, x, z);
  for (const x of [-14, 14])
    for (const y of [1, 7, 9])
      box(scene, [0.9, 0.2, 28], materials.gold, [x, y, 1]);
  for (const x of [-3.85, 3.85])
    box(scene, [0.45, 8.5, 0.7], materials.light, [x, 4.25, -11.4]);
  box(scene, [8.1, 0.45, 0.7], materials.light, [0, 8.3, -11.4]);
  const doors = buildDoors(scene, materials);
  label(scene, content.title, [0, 8.35, -10.92], 4.8);
  for (const x of [-10, 10]) {
    box(scene, [1.8, 3, 0.1], materials.navy, [x, 5, -11.5]);
    label(scene, 'مسار', [x, 5, -11.42], 1.6);
  }
  for (const x of [-11, 11])
    for (const z of [-8, 1, 9]) {
      cylinder(scene, [0.07, 0.12, 2.2], materials.gold, [x, 1.1, z]);
      sphere(
        scene,
        [0.15, 0.28, 0.15],
        material(0xffd692, { emissive: 0xffa847, emissiveIntensity: 2 }),
        [x, 2.35, z]
      );
      const lamp = new T.PointLight(0xffc47d, 10, 9, 2);
      lamp.position.set(x, 2.6, z);
      scene.add(lamp);
    }
  dinosaur(scene, materials);
  dressMuseum(scene, materials);
  finishArchitecture(scene, materials);
  return doors;
}

export function createPreviewWorld(container, callbacks, content) {
  const scene = new T.Scene();
  scene.background = new T.Color(0x192736);
  scene.fog = new T.Fog(0x263442, 27, 55);
  const renderer = new T.WebGLRenderer({ antialias: true, alpha: false });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.7));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = T.PCFShadowMap;
  renderer.toneMapping = T.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.02;
  renderer.domElement.className = 'three-canvas';
  container.prepend(renderer.domElement);
  const camera = new T.PerspectiveCamera(48, 1, 0.1, 100);
  lightMuseum(scene, renderer);
  const composer = cinematicPipeline(renderer, scene, camera);
  const hub = new T.Group();
  scene.add(hub);
  const doors = museum(hub, content);
  const environments = buildEnvironments(scene);
  let destinationIndex = -1;
  const mim = buildMim(materials);
  mim.root.rotation.y = 2.2;
  scene.add(mim.root);
  const flashlight = new T.SpotLight(0xd5ffff, 22, 18, 0.32, 0.6, 1.4);
  flashlight.position.set(-0.65, 1.35, 0.4);
  mim.root.add(flashlight);
  const lightTarget = new T.Object3D();
  lightTarget.position.set(-0.65, 1, 10);
  mim.root.add(lightTarget);
  flashlight.target = lightTarget;
  const lens = material(0xc7ffff, { emissive: 0x98eeed, emissiveIntensity: 2 });
  const torch = cylinder(
    mim.body,
    [0.1, 0.13, 0.35],
    materials.teal,
    [-0.65, 1.05, 0.15]
  );
  torch.rotation.x = Math.PI / 2;
  sphere(mim.body, [0.085, 0.085, 0.035], lens, [-0.65, 1.05, 0.34]);
  const beam = mesh(
    mim.root,
    new T.CylinderGeometry(0.03, 0.8, 5, 32, 1, true),
    new T.MeshBasicMaterial({
      color: 0xbbe9e3,
      transparent: true,
      opacity: 0.013,
      depthWrite: false,
      side: T.DoubleSide,
    }),
    [-0.65, 1.05, 2.85]
  );
  beam.rotation.x = -Math.PI / 2;
  beam.castShadow = false;
  beam.receiveShadow = false;
  const spots = [
    [-6, 0, -4],
    [0, 0, -6],
    [6, 0, -4],
  ];
  const rings = spots.map((position, index) =>
    relic(hub, index, position, content.missions[index].title)
  );
  const raycaster = new T.Raycaster();
  const cursor = new T.Vector2();
  const floor = new T.Plane(new T.Vector3(0, 1, 0), 0);
  const mechanism = buildAdventureMechanism(scene, materials);
  const restoration = buildRestoration(hub, materials);
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  let puzzleActive = false;
  let angle = -0.1,
    pitch = 0.18,
    zoom = 9.8,
    dragStart = null,
    dragged = false;
  let time = 0;
  const listeners = new AbortController();
  const observer = new ResizeObserver(() => {
    const width = container.clientWidth,
      height = container.clientHeight;
    if (width < 1 || height < 1) return;
    renderer.setSize(width, height);
    composer.setSize(width, height);
    camera.aspect = width / height;
    camera.fov = camera.aspect < 1 ? 65 : 48;
    camera.updateProjectionMatrix();
  });
  observer.observe(container);
  container.addEventListener(
    'pointerdown',
    (event) => {
      if (event.target !== renderer.domElement) return;
      dragStart = { x: event.clientX, y: event.clientY };
      dragged = false;
      renderer.domElement.setPointerCapture(event.pointerId);
    },
    { signal: listeners.signal }
  );
  container.addEventListener(
    'pointercancel',
    () => {
      dragStart = null;
    },
    { signal: listeners.signal }
  );
  container.addEventListener(
    'pointermove',
    (event) => {
      if (!dragStart) return;
      const deltaX = event.clientX - dragStart.x,
        deltaY = event.clientY - dragStart.y;
      if (Math.hypot(deltaX, deltaY) > 3) dragged = true;
      if (puzzleActive) {
        // Puzzle pieces are selected directly; dragging cannot change an answer.
      } else {
        angle -= deltaX * 0.008;
        pitch = T.MathUtils.clamp(pitch + deltaY * 0.004, 0.05, 0.62);
      }
      dragStart = { x: event.clientX, y: event.clientY };
    },
    { signal: listeners.signal }
  );
  container.addEventListener(
    'pointerup',
    (event) => {
      if (!dragStart) return;
      dragStart = null;
      if (dragged) return;
      const bounds = container.getBoundingClientRect();
      cursor.set(
        ((event.clientX - bounds.left) / bounds.width) * 2 - 1,
        (-(event.clientY - bounds.top) / bounds.height) * 2 + 1
      );
      raycaster.setFromCamera(cursor, camera);
      if (puzzleActive) {
        const target = mechanism.hit(raycaster);
        if (target) callbacks.select(target);
        return;
      }
      const point = new T.Vector3();
      if (raycaster.ray.intersectPlane(floor, point))
        callbacks.move({
          x: Math.max(7, Math.min(93, ((point.x + 10) / 20) * 100)),
          y: Math.max(58, Math.min(88, ((point.z + 20) / 32) * 100)),
        });
    },
    { signal: listeners.signal }
  );
  container.addEventListener(
    'wheel',
    (event) => {
      event.preventDefault();
      zoom = T.MathUtils.clamp(zoom + event.deltaY * 0.012, 7, 19);
    },
    { passive: false, signal: listeners.signal }
  );
  return {
    showDestination(index) {
      destinationIndex = index;
      hub.visible = index === -1;
      environments.show(index);
    },
    setLessonTitles(titles) {
      rings.forEach((ring, index) => {
        if (ring.userData.title === titles[index]) return;
        const sign = ring.userData.titleSign;
        sign.material.map.dispose();
        sign.material.dispose();
        sign.geometry.dispose();
        ring.parent.remove(sign);
        ring.userData.titleSign = label(
          ring.parent,
          titles[index],
          [0, 0.85, 0.977],
          1.75
        );
        ring.userData.title = titles[index];
      });
    },
    cameraRelativeMovement(horizontal, vertical) {
      return {
        x: horizontal * Math.cos(angle) + vertical * Math.sin(angle),
        y: -horizontal * Math.sin(angle) + vertical * Math.cos(angle),
      };
    },
    rotateCamera(horizontal, vertical, delta) {
      if (puzzleActive || !delta) return;
      angle -= horizontal * 2.05 * delta;
      pitch = T.MathUtils.clamp(pitch + vertical * 1.05 * delta, 0.05, 0.62);
    },
    showPuzzle(state) {
      puzzleActive = true;
      mechanism.show(state);
    },
    hidePuzzle() {
      puzzleActive = false;
      mechanism.hide();
    },
    puzzleTargets() {
      return mechanism.targets(camera);
    },
    setBadge(earned) {
      restoration.badge(mim.body, earned);
    },
    celebrate() {
      mechanism.success();
    },
    render(position, moving, completed, delta) {
      if (reduced.matches) delta = 0;
      time += delta;
      const x = (position.x / 100) * 20 - 10,
        z = (position.y / 100) * 32 - 20;
      const dx = x - mim.root.position.x,
        dz = z - mim.root.position.z;
      if (moving && Math.hypot(dx, dz) > 0.001)
        mim.root.rotation.y = Math.atan2(dx, dz);
      mim.root.position.set(puzzleActive ? -6.1 : x, 0, puzzleActive ? 5.2 : z);
      if (puzzleActive) mim.root.rotation.y = 2.15;
      mim.body.position.y = moving
        ? Math.abs(Math.sin(time * 10)) * 0.07
        : Math.sin(time * 2) * 0.015;
      mim.limbs.forEach((limb, index) => {
        limb.rotation.x = moving
          ? Math.sin(
              time * 9 +
                (index % 2 === 0 ? 0 : Math.PI) +
                (index > 1 ? Math.PI : 0)
            ) * 0.38
          : 0;
      });
      if (puzzleActive) {
        mim.limbs[2].rotation.x = -0.7;
        mim.limbs[3].rotation.x = -0.9;
      }
      const distance = zoom * (camera.aspect < 1 ? 1.4 : 1);
      const orbitDistance = Math.cos(pitch) * distance;
      const desired = puzzleActive
        ? new T.Vector3(0, 9, camera.aspect < 1 ? 24 : 13)
        : new T.Vector3(
            x + Math.sin(angle) * orbitDistance,
            2.4 + Math.sin(pitch) * distance,
            z + Math.cos(angle) * orbitDistance
          );
      camera.position.lerp(
        desired,
        reduced.matches ? 1 : 1 - Math.exp(-delta * 5)
      );
      camera.lookAt(
        puzzleActive ? 0 : x + 1.1,
        puzzleActive ? 1.2 : 2.4,
        puzzleActive ? 3 : z - 3
      );
      mechanism.update(delta);
      restoration.update(completed, delta);
      environments.update(delta, completed);
      rings.forEach((ring, index) => {
        ring.rotation.z = time * 0.2;
        ring.material = index < completed ? materials.gold : emissive;
      });
      doors.forEach((door, index) => {
        door.rotation.y = T.MathUtils.lerp(
          door.rotation.y,
          completed >= 4 ? (index === 0 ? 1.35 : -1.35) : 0,
          reduced.matches ? 1 : 1 - Math.exp(-delta * 4)
        );
      });
      composer.render(delta);
    },
    project(index) {
      const point = new T.Vector3(
        spots[index][0],
        3.6,
        spots[index][2]
      ).project(camera);
      return {
        x: (point.x + 1) * 50,
        y: (1 - point.y) * 50,
        visible: destinationIndex === -1 && point.z < 1,
      };
    },
    dispose() {
      listeners.abort();
      observer.disconnect();
      mechanism.dispose();
      scene.traverse((object) => {
        object.geometry?.dispose?.();
        const surfaces = Array.isArray(object.material)
          ? object.material
          : [object.material];
        surfaces.filter(Boolean).forEach((surface) => {
          for (const value of Object.values(surface)) {
            if (value?.isTexture) value.dispose?.();
          }
          surface.dispose?.();
        });
      });
      composer.dispose?.();
      renderer.dispose();
      renderer.forceContextLoss?.();
      renderer.domElement.remove();
    },
  };
}
