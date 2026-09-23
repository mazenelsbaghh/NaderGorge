import * as T from './vendor/three.module.js';
import { RoomEnvironment } from './vendor/addons/environments/RoomEnvironment.js';
import { cinematicPipeline } from './lighting.mjs';
import { bindFactoryControls } from './factory-controls.mjs';
import { buildMim } from './mim.mjs';
import { buildArchitecture, buildMachine, palette } from './factory-architecture.mjs';

export function createFactoryWorld(container) {
  const scene = new T.Scene();
  scene.background = new T.Color(0x6b6860);
  scene.fog = new T.Fog(0x8b8170, 36, 85);
  const renderer = new T.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 1.4));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = T.PCFSoftShadowMap;
  renderer.toneMapping = T.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.1;
  container.prepend(renderer.domElement);
  renderer.domElement.setAttribute('aria-label', 'مصنع الأسباب ثلاثي الأبعاد. اسحب لتدوير الكاميرا واستخدم الأسهم لتحريك ميم.');
  renderer.domElement.tabIndex = 0;
  const room = new RoomEnvironment(), generator = new T.PMREMGenerator(renderer);
  const environment = generator.fromScene(room, 0.05);
  scene.environment = environment.texture;
  scene.environmentIntensity = 0.3;
  room.dispose(); generator.dispose();
  scene.add(new T.HemisphereLight(0xe7d6b7, 0x423329, 0.9));
  const sun = new T.DirectionalLight(0xffd2a0, 3.8);
  sun.position.set(-14, 14, 7); sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048); sun.shadow.normalBias = 0.035;
  Object.assign(sun.shadow.camera, { left: -18, right: 18, top: 18, bottom: -18, near: 1, far: 55 });
  sun.target.position.set(0, 0, -5); scene.add(sun, sun.target);
  const fill = new T.DirectionalLight(0x82b4c8, 0.45); fill.position.set(10, 6, 12); scene.add(fill);
  const architecture = buildArchitecture(scene), machine = buildMachine(scene), mim = buildMim(palette);
  mim.root.position.set(-4, 0, 5); mim.root.rotation.y = 2.65; scene.add(mim.root);
  const camera = new T.PerspectiveCamera(48, 1, 0.1, 110);
  const composer = cinematicPipeline(renderer, scene, camera);
  const steamGeometry = new T.BufferGeometry();
  const steamPositions = new Float32Array(120 * 3);
  for (let i = 0; i < 120; i++) {
    steamPositions[i * 3] = Math.sin(i * 17) * 0.45;
    steamPositions[i * 3 + 1] = 4 + (i % 30) * 0.1;
    steamPositions[i * 3 + 2] = -3.2 + Math.cos(i * 7) * 0.35;
  }
  steamGeometry.setAttribute('position', new T.BufferAttribute(steamPositions, 3));
  const steamMaterial = new T.ShaderMaterial({
    transparent: true, depthWrite: false,
    vertexShader: 'void main(){vec4 p=modelViewMatrix*vec4(position,1.);gl_Position=projectionMatrix*p;gl_PointSize=100./max(1.,-p.z);}',
    fragmentShader: 'void main(){float a=1.-smoothstep(.05,.5,length(gl_PointCoord-vec2(.5)));gl_FragColor=vec4(.92,.86,.73,a*.08);}',
  });
  const steam = new T.Points(steamGeometry, steamMaterial); steam.frustumCulled = false; scene.add(steam);
  let yaw = -0.1, pitch = 0.18, distance = 9.8, powered = false, time = 0, dragging, dragged = false, destination = null;
  const listeners = new AbortController(), signal = listeners.signal;
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const desired = new T.Vector3();
  const inputs = bindFactoryControls(() => !document.hidden);
  const raycaster = new T.Raycaster(), floor = new T.Plane(new T.Vector3(0, 1, 0), 0);
  camera.position.set(-4, 4.2, 14.6);
  const observer = new ResizeObserver(() => {
    const width = container.clientWidth, height = container.clientHeight;
    if (!width || !height) return;
    renderer.setSize(width, height, false); composer.setSize(width, height); camera.aspect = width / height;
    camera.fov = width < height ? 65 : 48; camera.updateProjectionMatrix();
  });
  observer.observe(container);
  const stopInput = () => { inputs.reset(); dragging = undefined; destination = null; };
  window.addEventListener('blur', stopInput, { signal });
  document.addEventListener('visibilitychange', stopInput, { signal });
  renderer.domElement.addEventListener('pointerdown', (event) => {
    renderer.domElement.focus({ preventScroll: true });
    dragged = false; dragging = [event.clientX, event.clientY]; renderer.domElement.setPointerCapture(event.pointerId);
  }, { signal });
  renderer.domElement.addEventListener('pointermove', (event) => {
    if (!dragging) return;
    const dx = event.clientX - dragging[0], dy = event.clientY - dragging[1];
    if (Math.hypot(dx, dy) > 3) dragged = true;
    yaw -= dx * 0.008;
    pitch = T.MathUtils.clamp(pitch + dy * 0.004, 0.05, 0.62);
    dragging = [event.clientX, event.clientY];
  }, { signal });
  renderer.domElement.addEventListener('pointerup', (event) => {
    if (!dragging) return;
    dragging = undefined;
    if (dragged) return;
    const bounds = renderer.domElement.getBoundingClientRect();
    raycaster.setFromCamera(new T.Vector2((event.clientX - bounds.left) / bounds.width * 2 - 1, 1 - (event.clientY - bounds.top) / bounds.height * 2), camera);
    const point = raycaster.ray.intersectPlane(floor, new T.Vector3());
    if (point) destination = new T.Vector3(T.MathUtils.clamp(point.x, -6.5, 6.5), 0, T.MathUtils.clamp(point.z, 0.8, 10));
  }, { signal });
  renderer.domElement.addEventListener('pointercancel', stopInput, { signal });
  renderer.domElement.addEventListener('wheel', (event) => {
    event.preventDefault(); distance = T.MathUtils.clamp(distance + event.deltaY * 0.012, 7, 19);
  }, { passive: false, signal });
  renderer.domElement.addEventListener('webglcontextlost', (event) => {
    event.preventDefault(); document.getElementById('loadError').hidden = false;
  }, { signal });
  function move(delta) {
    const input = inputs.movement(), length = Math.hypot(input.x, input.y);
    let dx = 0, dz = 0;
    if (length) {
      destination = null;
      const scale = Math.min(1, length) / length;
      dx = (input.x * Math.cos(yaw) + input.y * Math.sin(yaw)) * scale * 4.8 * delta;
      dz = (input.y * Math.cos(yaw) - input.x * Math.sin(yaw)) * scale * 9.6 * delta;
    } else if (destination) {
      const offset = destination.clone().sub(mim.root.position), remaining = offset.length();
      if (remaining < 0.08) { destination = null; return false; }
      offset.multiplyScalar(Math.min(remaining, delta * 6) / remaining);
      dx = offset.x; dz = offset.z;
    }
    const oldX = mim.root.position.x, oldZ = mim.root.position.z;
    mim.root.position.x = T.MathUtils.clamp(oldX + dx, -6.5, 6.5);
    mim.root.position.z = T.MathUtils.clamp(oldZ + dz, 0.8, 10);
    if (dx || dz) mim.root.rotation.y = Math.atan2(dx, dz);
    return Math.hypot(mim.root.position.x - oldX, mim.root.position.z - oldZ) > 0.001;
  }
  return {
    resetCamera() { yaw = -0.1; pitch = 0.18; distance = 9.8; stopInput(); },
    setConnections(connected, running) {
      powered = running;
      const resultOrder = [1, 2, 0];
      for (let row = 0; row < 3; row++) {
        machine.sockets.cause[row].material.emissiveIntensity = connected.has(row) ? 3 : 0.8;
        machine.sockets.result[row].material.emissiveIntensity = connected.has(resultOrder[row]) ? 3 : 0.8;
      }
      machine.indicator.emissiveIntensity = running ? 3 : connected.size * 0.4;
    },
    projectSocket(kind, row) {
      const point = machine.sockets[kind][row].getWorldPosition(new T.Vector3()).project(camera);
      return { x: (point.x + 1) * 50, y: (1 - point.y) * 50, visible: point.z > -1 && point.z < 1 && Math.abs(point.x) < 1 && Math.abs(point.y) < 1 };
    },
    projectPower() {
      const point = new T.Vector3(0, 0.8, -3).project(camera);
      return { x: (point.x + 1) * 50, y: (1 - point.y) * 50, visible: point.z > -1 && point.z < 1 && Math.abs(point.x) < 1 && Math.abs(point.y) < 1 };
    },
    render(delta) {
      yaw -= inputs.camera.x * 2.05 * delta;
      pitch = T.MathUtils.clamp(pitch + inputs.camera.y * 1.05 * delta, 0.05, 0.62);
      const moving = move(delta); time += delta;
      const animation = reduced.matches ? 0 : delta;
      mim.body.position.y = reduced.matches ? 0 : moving ? Math.abs(Math.sin(time * 10)) * 0.07 : Math.sin(time * 2) * 0.015;
      mim.limbs.forEach((limb, i) => { limb.rotation.x = moving && !reduced.matches ? Math.sin(time * 9 + (i % 2 === 0 ? 0 : Math.PI) + (i > 1 ? Math.PI : 0)) * 0.38 : 0; });
      const x = mim.root.position.x, z = mim.root.position.z;
      const zoom = distance * (camera.aspect < 1 ? 1.4 : 1);
      const orbit = Math.cos(pitch) * zoom;
      desired.set(x + Math.sin(yaw) * orbit, 2.4 + Math.sin(pitch) * zoom, z + Math.cos(yaw) * orbit);
      camera.position.lerp(desired, reduced.matches ? 1 : 1 - Math.exp(-delta * 5));
      camera.lookAt(x + 1.1, 2.4, z - 3);
      architecture.wheel.rotation.z += animation * 0.12;
      architecture.gears.forEach((gear, i) => { gear.rotation.z += animation * (powered ? 0.4 : 0.06) * (i % 2 ? -1 : 1); });
      machine.rotor.rotation.z += animation * (powered ? 3 : 0.08);
      architecture.waterfall.material.opacity = 0.62 + Math.sin(time * 3) * (reduced.matches ? 0 : 0.07);
      if (animation) {
        for (let i = 0; i < 120; i++) {
          steamPositions[i * 3 + 1] += animation * (powered ? 1.1 : 0.3);
          if (steamPositions[i * 3 + 1] > 7) steamPositions[i * 3 + 1] = 4;
        }
        steamGeometry.attributes.position.needsUpdate = true;
      }
      composer.render(delta);
    },
    dispose() {
      inputs.dispose(); listeners.abort(); observer.disconnect(); environment.dispose();
      const geometries = new Set(), materials = new Set(), textures = new Set();
      scene.traverse((object) => {
        if (object.geometry) geometries.add(object.geometry);
        for (const material of [object.material].flat().filter(Boolean)) materials.add(material);
      });
      for (const material of materials) {
        for (const value of Object.values(material)) if (value?.isTexture) textures.add(value);
        material.dispose();
      }
      textures.forEach((texture) => texture.dispose()); geometries.forEach((geometry) => geometry.dispose());
      for (const pass of composer.passes) pass.dispose?.();
      composer.dispose(); renderer.dispose(); renderer.domElement.remove();
    },
  };
}
