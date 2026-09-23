import * as T from './vendor/three.module.js';
import { roundedBox } from './geometry.mjs';

function add(parent, geometry, surface, position) {
  const mesh = new T.Mesh(geometry, surface);
  mesh.position.set(...position);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  parent.add(mesh);
  return mesh;
}

function inscription(text, width, color = '#112b40') {
  const canvas = document.createElement('canvas');
  canvas.width = 1024;
  canvas.height = 300;
  const ctx = canvas.getContext('2d');
  ctx.fillStyle = color;
  ctx.fillRect(0, 0, 1024, 300);
  ctx.strokeStyle = '#c9aa68';
  ctx.lineWidth = 10;
  ctx.strokeRect(8, 8, 1008, 284);
  ctx.font = 'bold 88px Tahoma';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.direction = 'rtl';
  ctx.fillStyle = '#fff0d4';
  const lines = [];
  let line = '';
  for (const word of text.split(' ')) {
    const candidate = `${line} ${word}`.trim();
    if (line && ctx.measureText(candidate).width > 910) {
      lines.push(line);
      line = word;
    } else line = candidate;
  }
  lines.push(line);
  lines.forEach((part, index) =>
    ctx.fillText(part, 512, 150 + (index - (lines.length - 1) / 2) * 92)
  );
  const texture = new T.CanvasTexture(canvas);
  texture.colorSpace = T.SRGBColorSpace;
  const surface = new T.MeshBasicMaterial({ map: texture });
  const sign = new T.Mesh(
    new T.PlaneGeometry(width, (width * 300) / 1024),
    surface
  );
  sign.userData.ownsSurface = true;
  return sign;
}

export function buildAdventureMechanism(scene, palette) {
  const root = new T.Group();
  scene.add(root);
  root.position.set(0, 0, 3);
  root.visible = false;
  const stone = new T.MeshStandardMaterial({
    color: 0xb5a58b,
    map: palette.stone.map,
    roughness: 0.55,
  });
  const brass = new T.MeshStandardMaterial({
    color: 0xb99454,
    metalness: 0.7,
    roughness: 0.3,
  });
  const active = new T.MeshStandardMaterial({
    color: 0x4cffff,
    emissive: 0x21c5c8,
    emissiveIntensity: 0.8,
  });
  let targets = [],
    rotors = [],
    pulse = 0;

  function clear() {
    root.traverse((mesh) => {
      mesh.geometry?.dispose();
      if (mesh.userData.ownsSurface) {
        mesh.material.map.dispose();
        mesh.material.dispose();
      }
    });
    root.clear();
    targets = [];
    rotors = [];
  }
  function plaque(text, position, options = {}) {
    const sign = inscription(
      text,
      options.width ?? 3.5,
      options.done ? '#18574f' : options.selected ? '#386660' : '#112b40'
    );
    sign.position.set(...position);
    root.add(sign);
    if (options.action) {
      sign.userData.action = options.action;
      targets.push({
        mesh: sign,
        label: text,
        action: options.action,
        selected: options.selected,
      });
    }
    return sign;
  }
  function plinth(x, z) {
    add(root, roundedBox([2.8, 0.35, 1.8], 0.08), stone, [x, 0.25, z]);
    add(root, roundedBox([2.6, 0.12, 1.65], 0.04), brass, [x, 0.49, z]);
  }
  function order(state) {
    const selected = state.order;
    for (let index = 0; index < 3; index++) {
      const x = (1 - index) * 3.8;
      plinth(x, -3);
      const choiceIndex = (index + 1) % 3;
      const chosen = selected.includes(choiceIndex);
      add(root, roundedBox([2.5, 0.75, 1.4], 0.06), chosen ? active : stone, [
        x,
        chosen ? 0.8 : 1.1,
        -3,
      ]);
      plaque(state.puzzle.items[choiceIndex], [x, 2.3, -2.25], {
        done: chosen,
        action: chosen ? null : { kind: 'order', index: choiceIndex },
      });
      const slot = selected[index];
      add(
        root,
        roundedBox([3.2, 0.22, 1.3], 0.04),
        slot === undefined ? palette.navy : brass,
        [x, 0.25, 1]
      );
      plaque(
        slot === undefined
          ? `موضع ${index + 1}`
          : `${index + 1}. ${state.puzzle.items[slot]}`,
        [x, 0.94, 1.6],
        { width: 3.1, done: slot !== undefined }
      );
    }
    for (const z of [-0.1, 0.25, 0.6])
      add(root, new T.BoxGeometry(10.8, 0.05, 0.04), brass, [0, 0.15, z]);
  }
  function match(state) {
    const count = state.puzzle.pairs.length;
    add(root, new T.CylinderGeometry(2.3, 2.6, 0.3, 64), stone, [0, 0.2, -0.6]);
    const rotor = add(
      root,
      new T.TorusGeometry(0.85, 0.08, 16, 48),
      brass,
      [0, 2, -0.5]
    );
    rotors.push(rotor);
    for (let index = 0; index < count; index++) {
      const y = 3.6 - index * 1.15,
        right = (index + 1) % count;
      const matched = state.pairs.includes(index),
        resultMatched = state.pairs.includes(right);
      plaque(state.puzzle.pairs[index][0], [4, y, -1], {
        width: 3.55,
        done: matched,
        selected: state.selected === index,
        action: matched ? null : { kind: 'cause', index },
      });
      plaque(state.puzzle.pairs[right][1], [-4, y, -1], {
        width: 3.55,
        done: resultMatched,
        action:
          resultMatched || state.selected === null
            ? null
            : { kind: 'result', index: right },
      });
      add(root, new T.SphereGeometry(0.13, 16, 12), matched ? active : brass, [
        2.15,
        y,
        -0.8,
      ]);
      add(
        root,
        new T.SphereGeometry(0.13, 16, 12),
        resultMatched ? active : brass,
        [-2.15, y, -0.8]
      );
      if (matched) {
        const resultRow = (index - 1 + count) % count;
        const curve = new T.CubicBezierCurve3(
          new T.Vector3(2.15, y, -0.8),
          new T.Vector3(0.8, y, -0.3),
          new T.Vector3(-0.8, 3.6 - resultRow * 1.15, -0.3),
          new T.Vector3(-2.15, 3.6 - resultRow * 1.15, -0.8)
        );
        add(
          root,
          new T.TubeGeometry(curve, 30, 0.055, 8, false),
          active,
          [0, 0, 0]
        );
      }
    }
  }
  function sort(state) {
    state.puzzle.items.forEach((name, index) => {
      const x = (1 - index) * 3.8;
      add(
        root,
        new T.DodecahedronGeometry(0.42 + index * 0.06),
        state.categories[index] >= 0 ? brass : palette.teal,
        [x, 2.2, -3]
      );
      plaque(name, [x, 3.15, -2.7], {
        width: 3.5,
        selected: state.selected === index,
        done: state.categories[index] >= 0,
        action: { kind: 'sample', index },
      });
      plinth(x, 1);
      add(root, roundedBox([2.6, 1.1, 1.5], 0.06), stone, [x, 1, 1]);
      const assigned = state.categories
        .map((category, i) => (category === index ? i : -1))
        .filter((i) => i >= 0);
      assigned.forEach((sample, j) =>
        add(root, new T.DodecahedronGeometry(0.23), brass, [
          x + (j - 1) * 0.45,
          1.8,
          1,
        ])
      );
      plaque(state.puzzle.categories[index], [x, 1.05, 1.81], {
        width: 2.9,
        action: state.selected === null ? null : { kind: 'category', index },
      });
    });
  }
  const builders = { order, match, sort };
  return {
    show(state) {
      clear();
      root.visible = true;
      builders[state.puzzle.type](state);
    },
    hide() {
      root.visible = false;
    },
    hit(raycaster) {
      return raycaster
        .intersectObject(root, true)
        .find((hit) => hit.object.userData.action)?.object.userData.action;
    },
    targets(camera) {
      if (!root.visible) return [];
      root.updateMatrixWorld(true);
      return targets.map((target) => {
        const center = target.mesh
          .getWorldPosition(new T.Vector3())
          .project(camera);
        const geometry = target.mesh.geometry.parameters;
        const left = new T.Vector3(-geometry.width / 2, geometry.height / 2, 0)
          .applyMatrix4(target.mesh.matrixWorld)
          .project(camera);
        const right = new T.Vector3(geometry.width / 2, -geometry.height / 2, 0)
          .applyMatrix4(target.mesh.matrixWorld)
          .project(camera);
        return {
          ...target,
          mesh: undefined,
          x: (center.x + 1) * 50,
          y: (1 - center.y) * 50,
          width: Math.abs(right.x - left.x) * 50,
          height: Math.abs(right.y - left.y) * 50,
          visible: center.z < 1,
        };
      });
    },
    success() {
      pulse = 1.5;
    },
    update(delta) {
      rotors.forEach((rotor) => {
        rotor.rotation.z += delta * 0.7;
      });
      pulse = Math.max(0, pulse - delta);
      active.emissiveIntensity = pulse ? 1.1 : 0.65;
    },
    dispose() {
      clear();
      stone.dispose();
      brass.dispose();
      active.dispose();
      scene.remove(root);
    },
  };
}

export function buildRestoration(scene, palette) {
  const group = new T.Group();
  scene.add(group);
  const bridge = new T.Group();
  bridge.position.set(-6, 0, -1);
  group.add(bridge);
  for (let i = 0; i < 6; i++)
    add(bridge, new T.BoxGeometry(1.65, 0.14, 0.35), palette.gold, [
      0,
      0.12,
      i * 0.4,
    ]);
  const rotor = add(
    group,
    new T.TorusGeometry(0.8, 0.07, 12, 48),
    palette.gold,
    [0, 2, -5]
  );
  const lamp = new T.PointLight(0x73e5cb, 0, 8);
  lamp.position.set(0, 3, -5);
  group.add(lamp);
  const crystal = add(
    group,
    new T.OctahedronGeometry(0.5),
    palette.teal,
    [6, 3.6, -4]
  );
  let badge;
  return {
    badge(body, earned) {
      if (!badge)
        badge = add(
          body,
          new T.OctahedronGeometry(0.13),
          palette.gold,
          [0.23, 1.28, 0.42]
        );
      badge.visible = Boolean(earned);
    },
    update(completed, delta) {
      bridge.visible = completed >= 1;
      rotor.visible = completed >= 2;
      crystal.visible = completed >= 3;
      lamp.intensity = completed >= 2 ? 12 : 0;
      if (completed >= 2) rotor.rotation.z += delta;
    },
  };
}
