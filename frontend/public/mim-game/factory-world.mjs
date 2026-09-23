import * as T from './vendor/three.module.js';
import { RoomEnvironment } from './vendor/addons/environments/RoomEnvironment.js';
import { cinematicPipeline } from './lighting.mjs';
import { bindFactoryControls } from './factory-controls.mjs?v=3';
import { createFactoryCables } from './factory-cables.mjs?v=2';
import { createFactoryTraversal } from './factory-traversal.mjs?v=4';
import { buildMim } from './mim.mjs';
import { buildArchitecture, buildMachine, palette } from './factory-architecture.mjs';

export function createFactoryWorld(container, onCollect, onPad) {
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
  const cables = createFactoryCables(scene, machine.sockets);
  const traversal = createFactoryTraversal(scene);
  let progress = 0;
  let grappleTarget = null, landing = null, cellCollected = false, padCooldown = 0;
  const tetherPositions = new Float32Array(6);
  const tetherGeometry = new T.BufferGeometry();
  tetherGeometry.setAttribute('position', new T.BufferAttribute(tetherPositions, 3));
  const tether = new T.Line(tetherGeometry, new T.LineBasicMaterial({ color: 0xffd9a2, transparent: true, opacity: 0.85 }));
  tether.visible = false; scene.add(tether);
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
  let verticalSpeed = 0, dashTime = 0, dashCooldown = 0, orbitTimer = 0;
  const velocity = new T.Vector3(), heading = new T.Vector3(0, 0, -1);
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
  const cancelWalk = () => { destination = null; };
  const stopInput = () => { inputs.reset(); dragging = undefined; cancelWalk(); };
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
    if (Math.abs(dx) > 0.5) orbitTimer = 2;
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
    if (point) cancelWalk();
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
    dashCooldown = Math.max(0, dashCooldown - delta);
    padCooldown = Math.max(0, padCooldown - delta);
    orbitTimer = Math.max(0, orbitTimer - delta);
    if (landing) {
      landing.elapsed = Math.min(landing.duration, landing.elapsed + delta);
      const t = landing.elapsed / landing.duration;
      yaw += Math.atan2(Math.sin(-0.1 - yaw), Math.cos(-0.1 - yaw)) * (1 - Math.exp(-delta * 4));
      pitch += (0.18 - pitch) * (1 - Math.exp(-delta * 4));
      mim.root.position.lerpVectors(landing.start, landing.end, t * t * (3 - 2 * t));
      mim.root.position.y += Math.sin(Math.PI * t) * 0.9;
      if (t === 1) { mim.root.position.copy(landing.end); landing = null; velocity.set(0, 0, 0); verticalSpeed = 0; inputs.reset(); orbitTimer = 1.5; }
      return true;
    }
    const input = inputs.movement(), length = Math.hypot(input.x, input.y);
    if (inputs.consume('grapple')) {
      grappleTarget = grappleTarget ? null : traversal.target();
      if (grappleTarget) { cancelWalk(); velocity.multiplyScalar(0.35); verticalSpeed = Math.max(verticalSpeed, 2); }
    }
    const jumpPressed = inputs.consume('jump');
    if (grappleTarget && jumpPressed) { grappleTarget = null; verticalSpeed = 7.3; }
    if (grappleTarget) {
      const pull = grappleTarget.clone().sub(mim.root.position), distanceToCell = pull.length();
      pull.normalize().multiplyScalar(42 * delta);
      velocity.x += pull.x; velocity.z += pull.z; verticalSpeed += pull.y;
      if (length) { velocity.x += input.x * delta * 6; velocity.z += input.y * delta * 6; }
      const damping = Math.exp(-delta * 2.2);
      velocity.multiplyScalar(damping); verticalSpeed *= damping;
      const speed = Math.hypot(velocity.x, verticalSpeed, velocity.z);
      if (speed > 18) { velocity.multiplyScalar(18 / speed); verticalSpeed *= 18 / speed; }
      mim.root.position.x = T.MathUtils.clamp(mim.root.position.x + velocity.x * delta, -9, 9);
      mim.root.position.y = T.MathUtils.clamp(mim.root.position.y + verticalSpeed * delta, 0, 9);
      mim.root.position.z = T.MathUtils.clamp(mim.root.position.z + velocity.z * delta, -10, 13);
      if (distanceToCell < 1.35 && !cellCollected) {
        cellCollected = true; grappleTarget = null; traversal.claim(); onCollect(progress);
        landing = { start: mim.root.position.clone(), end: new T.Vector3(0, 0, 8), elapsed: 0, duration: 0.72 };
      }
      return true;
    }
    const grounded = mim.root.position.y <= 0.001;
    if (jumpPressed && grounded) verticalSpeed = 7.3;
    if (inputs.consume('dash') && dashCooldown === 0) {
      dashTime = 0.23; dashCooldown = 0.85;
      if (length) heading.set(input.x * Math.cos(yaw) + input.y * Math.sin(yaw), 0, input.y * Math.cos(yaw) - input.x * Math.sin(yaw)).normalize();
      velocity.copy(heading).multiplyScalar(19);
    }
    let dx = 0, dz = 0;
    if (length) {
      cancelWalk();
      const strength = Math.min(1, length) / length;
      heading.set(input.x * Math.cos(yaw) + input.y * Math.sin(yaw), 0, input.y * Math.cos(yaw) - input.x * Math.sin(yaw)).normalize();
      const speed = inputs.sprinting() ? 12 : 8.5;
      if (dashTime <= 0) {
        const blend = 1 - Math.exp(-delta * 15);
        velocity.x += (heading.x * speed * strength - velocity.x) * blend;
        velocity.z += (heading.z * speed * strength - velocity.z) * blend;
      }
    } else if (destination) {
      const offset = destination.clone().sub(mim.root.position), remaining = offset.length();
      if (remaining < 0.15) { destination = null; velocity.set(0, 0, 0); return false; }
      offset.multiplyScalar(Math.min(remaining, delta * 9) / remaining);
      dx = offset.x; dz = offset.z;
    } else if (dashTime <= 0) {
      velocity.multiplyScalar(Math.exp(-delta * 10));
    }
    dashTime = Math.max(0, dashTime - delta);
    if (!destination) { dx = velocity.x * delta; dz = velocity.z * delta; }
    const oldX = mim.root.position.x, oldZ = mim.root.position.z;
    mim.root.position.x = T.MathUtils.clamp(oldX + dx, -6.5, 6.5);
    mim.root.position.z = T.MathUtils.clamp(oldZ + dz, 0.8, 10);
    if (mim.root.position.x !== oldX || mim.root.position.z !== oldZ) {
      mim.root.rotation.y = Math.atan2(dx, dz);
    }
    if (mim.root.position.x === oldX) velocity.x = 0;
    if (mim.root.position.z === oldZ) velocity.z = 0;
    if (verticalSpeed || !grounded) {
      verticalSpeed -= 18 * delta;
      mim.root.position.y = Math.max(0, mim.root.position.y + verticalSpeed * delta);
      if (mim.root.position.y === 0) verticalSpeed = 0;
    }
    if (cellCollected && mim.root.position.y <= 0.001 && (length || destination || dashTime > 0) && padCooldown === 0) {
      const pad = traversal.padIndex(mim.root.position);
      if (pad !== -1) { padCooldown = 1; onPad(pad); }
    }
    return Math.hypot(mim.root.position.x - oldX, mim.root.position.z - oldZ) > 0.001;
  }
  return {
    rejectPad(index) { traversal.rejectPad(index); },
    resetCamera() { yaw = -0.1; pitch = 0.18; distance = 9.8; orbitTimer = 2; stopInput(); },
    resetGame() {
      stopInput(); grappleTarget = null; landing = null; velocity.set(0, 0, 0); verticalSpeed = 0; cellCollected = false;
      heading.set(0, 0, -1); dashTime = 0; dashCooldown = 0; padCooldown = 0;
      mim.root.position.set(-4, 0, 5); mim.root.rotation.y = 2.65;
      yaw = -0.1; pitch = 0.18; distance = 9.8; orbitTimer = 0;
    },
    setConnections(connected, running) {
      powered = running;
      if (progress !== connected.size) { progress = connected.size; cellCollected = false; traversal.setStage(progress); }
      cables.connect(connected);
      const resultOrder = [1, 2, 0];
      for (let row = 0; row < 3; row++) {
        machine.sockets.cause[row].material.emissiveIntensity = connected.has(row) ? 3 : 0.8;
        machine.sockets.result[row].material.emissiveIntensity = connected.has(resultOrder[row]) ? 3 : 0.8;
      }
      machine.indicator.emissiveIntensity = running ? 3 : connected.size * 0.4;
    },
    projectPad(index) {
      const point = traversal.padPosition(index).clone().add(new T.Vector3(0, 0.9, 0)).project(camera);
      return { x: (point.x + 1) * 50, y: (1 - point.y) * 50, visible: point.z > -1 && point.z < 1 && Math.abs(point.x) < 0.85 && Math.abs(point.y) < 0.75 };
    },
    render(delta) {
      if (Math.abs(inputs.camera.x) > 0.03) orbitTimer = 2;
      yaw -= inputs.camera.x * 2.7 * delta;
      pitch = T.MathUtils.clamp(pitch + inputs.camera.y * 1.05 * delta, 0.05, 0.62);
      const moving = move(delta); time += delta;
      if (moving && orbitTimer === 0 && inputs.movement().y < -0.6 && Math.hypot(velocity.x, velocity.z) > 3) {
        const behind = Math.atan2(-velocity.x, -velocity.z);
        yaw += Math.atan2(Math.sin(behind - yaw), Math.cos(behind - yaw)) * (1 - Math.exp(-delta * 1.4));
      }
      const animation = reduced.matches ? 0 : delta;
      mim.body.position.y = reduced.matches ? 0 : moving ? Math.abs(Math.sin(time * 10)) * 0.07 : Math.sin(time * 2) * 0.015;
      mim.limbs.forEach((limb, i) => { limb.rotation.x = moving && !reduced.matches ? Math.sin(time * 9 + (i % 2 === 0 ? 0 : Math.PI) + (i > 1 ? Math.PI : 0)) * 0.38 : 0; });
      cables.update(reduced.matches ? 0 : time);
      traversal.update(delta, time, reduced.matches);
      tether.visible = Boolean(grappleTarget);
      if (grappleTarget) {
        tetherPositions.set([mim.root.position.x, mim.root.position.y + 1.8, mim.root.position.z, grappleTarget.x, grappleTarget.y + 2.3, grappleTarget.z]);
        tetherGeometry.attributes.position.needsUpdate = true;
      }
      const x = mim.root.position.x, z = mim.root.position.z;
      const speed = Math.hypot(velocity.x, velocity.z);
      const zoom = (distance + (cellCollected ? 5 : 0) + Math.min(2, speed * 0.12)) * (camera.aspect < 1 ? 1.4 : 1);
      const orbit = Math.cos(pitch) * zoom;
      const followX = cellCollected ? x * 0.55 : x;
      desired.set(followX + Math.sin(yaw) * orbit, 2.2 + mim.root.position.y + Math.sin(pitch) * zoom, z + Math.cos(yaw) * orbit);
      if (powered) desired.set(0, 6.5, camera.aspect < 1 ? 23 : 17);
      else { desired.x = T.MathUtils.clamp(desired.x, -8.5, 8.5); desired.y = Math.min(desired.y, 7); desired.z = Math.max(desired.z, -10.5); }
      camera.position.lerp(desired, reduced.matches ? 1 : 1 - Math.exp(-delta * 5));
      camera.fov = T.MathUtils.lerp(camera.fov, powered ? 48 : (camera.aspect < 1 ? 65 : 48) + Math.min(7, speed * 0.35), 1 - Math.exp(-delta * 3));
      camera.updateProjectionMatrix();
      if (powered) camera.lookAt(0, 3, -3);
      else camera.lookAt(followX - Math.sin(yaw) * 2, 2.2 + mim.root.position.y, z - Math.cos(yaw) * 2);
      architecture.wheel.rotation.z += animation * 0.12;
      architecture.gears.forEach((gear, i) => { gear.rotation.z += animation * (powered ? 0.4 : 0.02 + progress * 0.08) * (i % 2 ? -1 : 1); });
      machine.rotor.rotation.z += animation * (powered ? 3 : progress * 0.4);
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
