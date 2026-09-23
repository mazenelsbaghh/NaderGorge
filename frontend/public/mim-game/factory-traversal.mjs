import * as T from './vendor/three.module.js';

const stations = [
  new T.Vector3(-6, 6.2, 1),
  new T.Vector3(0, 6.2, -6),
  new T.Vector3(6, 6.2, 1),
];

export function createFactoryTraversal(scene) {
  const nodes = stations.map((position, index) => {
    const root = new T.Group(); root.position.copy(position); scene.add(root);
    const core = new T.Mesh(
      new T.IcosahedronGeometry(0.43, 1),
      new T.MeshStandardMaterial({ color: 0xffd89b, emissive: 0xff9b31, emissiveIntensity: 2.5, roughness: 0.22, metalness: 0.35 }),
    );
    root.add(core);
    const halo = new T.Mesh(
      new T.TorusGeometry(0.72, 0.055, 8, 40),
      new T.MeshBasicMaterial({ color: 0xffdf91 }),
    );
    root.add(halo);
    const fixture = new T.Mesh(
      new T.CylinderGeometry(0.15, 0.15, 2.1, 10),
      new T.MeshStandardMaterial({ color: 0x7e522e, metalness: 0.7, roughness: 0.38 }),
    );
    fixture.position.y = 2.7; root.add(fixture);
    const light = new T.PointLight(0xffba61, 9, 6); root.add(light);
    return { root, core, halo, light, index };
  });
  const padPositions = [-4.4, 0, 4.4].map((x) => new T.Vector3(x, 0, 3.3));
  const pads = padPositions.map((position) => {
    const root = new T.Group(); root.position.copy(position); scene.add(root);
    const ring = new T.Mesh(
      new T.TorusGeometry(1.05, 0.1, 10, 40),
      new T.MeshStandardMaterial({ color: 0x4dd9e6, emissive: 0x19aabe, emissiveIntensity: 1.5, metalness: 0.4, roughness: 0.25 }),
    );
    ring.rotation.x = Math.PI / 2; ring.position.y = 0.08; root.add(ring);
    const core = new T.Mesh(
      new T.CylinderGeometry(0.88, 0.88, 0.025, 32),
      new T.MeshBasicMaterial({ color: 0x1c8494, transparent: true, opacity: 0.28, depthWrite: false }),
    );
    core.position.y = 0.045; root.add(core);
    const light = new T.PointLight(0x41cbdf, 6, 4); light.position.y = 0.2; root.add(light);
    root.visible = false;
    return { root, ring, light };
  });
  let stage = 0, claimed = false, rejectedPad = -1, rejectTime = 0;
  return {
    target() { return stations[stage]?.clone() ?? null; },
    setStage(index) { stage = index; claimed = false; },
    claim() { claimed = true; },
    rejectPad(index) { rejectedPad = index; rejectTime = 0.55; },
    padPosition(index) { return padPositions[index]; },
    padIndex(position) { return padPositions.findIndex((pad) => Math.hypot(pad.x - position.x, pad.z - position.z) < 1.55); },
    update(delta, time, reducedMotion) {
      rejectTime = Math.max(0, rejectTime - delta);
      for (const node of nodes) {
        const active = node.index === stage && !claimed;
        node.root.visible = active;
        node.core.material.emissiveIntensity = active ? 2.5 : 0.12;
        node.light.intensity = active ? 9 : 0;
        node.halo.visible = active;
        node.halo.rotation.y = reducedMotion ? 0 : time * 0.75;
        node.halo.rotation.z = reducedMotion ? 0 : time * 0.4;
        node.root.position.y = stations[node.index].y + (active && !reducedMotion ? Math.sin(time * 2.5) * 0.14 : 0);
      }
      pads.forEach((pad, index) => {
        pad.root.visible = claimed;
        pad.ring.material.emissiveIntensity = claimed ? 1.2 + (reducedMotion ? 0 : Math.sin(time * 5) * 0.3) : 0;
        const rejected = index === rejectedPad && rejectTime > 0;
        pad.ring.material.emissive.setHex(rejected ? 0xff3425 : 0x19aabe);
        pad.light.color.setHex(rejected ? 0xff3425 : 0x41cbdf);
      });
    },
  };
}
