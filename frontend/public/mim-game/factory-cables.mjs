import * as T from './vendor/three.module.js';

const SEGMENTS = 40, SIDES = 6;
function cableMesh(root, color) {
  const geometry = new T.BufferGeometry();
  const positions = new Float32Array((SEGMENTS + 1) * SIDES * 3), indices = [];
  for (let i = 0; i < SEGMENTS; i++) for (let j = 0; j < SIDES; j++) {
    const a = i * SIDES + j, b = i * SIDES + (j + 1) % SIDES;
    indices.push(a, b, a + SIDES, b, b + SIDES, a + SIDES);
  }
  geometry.setAttribute('position', new T.BufferAttribute(positions, 3).setUsage(T.DynamicDrawUsage));
  geometry.setIndex(indices);
  const material = new T.MeshStandardMaterial({ color, emissive: color, emissiveIntensity: 0.5, roughness: 0.4, metalness: 0.45 });
  const mesh = new T.Mesh(geometry, material); mesh.frustumCulled = false; mesh.visible = false; root.add(mesh);
  const bead = new T.Mesh(new T.SphereGeometry(0.12, 10, 8), new T.MeshBasicMaterial({ color: 0xffefbc }));
  root.add(bead); bead.visible = false;
  return { mesh, bead, positions, curve: new T.CatmullRomCurve3(Array.from({ length: 4 }, () => new T.Vector3())) };
}
function shapeCable(cable, start, end, lane) {
  const points = cable.curve.points;
  points[0].copy(start); points[3].copy(end);
  points[1].set(start.x, 0.18 + lane * 0.09, 1.1 + lane * 0.3);
  points[2].set(end.x, 0.18 + lane * 0.09, Math.max(end.z - 0.4, 1.1 + lane * 0.3));
  const center = new T.Vector3(), tangent = new T.Vector3(), normal = new T.Vector3(), binormal = new T.Vector3();
  for (let i = 0; i <= SEGMENTS; i++) {
    cable.curve.getPoint(i / SEGMENTS, center); center.y = Math.max(0.14 + lane * 0.06, center.y); cable.curve.getTangent(i / SEGMENTS, tangent);
    normal.set(0, 0, 1).cross(tangent).normalize(); binormal.crossVectors(tangent, normal);
    for (let j = 0; j < SIDES; j++) {
      const angle = j / SIDES * Math.PI * 2, offset = (i * SIDES + j) * 3;
      for (let axis = 0; axis < 3; axis++) cable.positions[offset + axis] = center.getComponent(axis) + 0.055 * (normal.getComponent(axis) * Math.cos(angle) + binormal.getComponent(axis) * Math.sin(angle));
    }
  }
  cable.mesh.geometry.attributes.position.needsUpdate = true;
  cable.mesh.geometry.computeVertexNormals();
}
export function createFactoryCables(scene, sockets, mim) {
  const cables = [0xffb34e, 0x65dbca, 0xe6bd7b].map((color) => cableMesh(scene, color));
  const hand = new T.Vector3(), start = new T.Vector3(), end = new T.Vector3();
  let carrying = null, completed = new Set(), flash = 0;
  return {
    carry(index) { carrying = index; },
    connect(indices) { completed = new Set(indices); },
    reject() { flash = 0.65; },
    update(delta, time) {
      flash = Math.max(0, flash - delta);
      scene.updateMatrixWorld(true);
      mim.limbs[3].localToWorld(hand.set(0, -0.62, 0.12));
      cables.forEach((cable, index) => {
        const done = completed.has(index), held = carrying === index;
        cable.mesh.visible = cable.bead.visible = done || held;
        if (!done && !held) return;
        sockets.cause[index].getWorldPosition(start);
        if (done) sockets.result[[2, 0, 1][index]].getWorldPosition(end); else end.copy(hand);
        shapeCable(cable, start, end, index);
        cable.mesh.material.emissiveIntensity = held && flash ? 3 : done ? 1.2 : 0.35;
        cable.mesh.material.emissive.setHex(held && flash ? 0xff3420 : done ? 0x42dac0 : 0xffa33c);
        cable.curve.getPoint((time * 0.35 + index * 0.3) % 1, cable.bead.position);
        cable.bead.position.y = Math.max(0.14 + index * 0.06, cable.bead.position.y);
      });
    },
  };
}
