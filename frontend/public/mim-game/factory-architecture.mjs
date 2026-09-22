import * as T from './vendor/three.module.js';
import { addMesh, roundedBox, sculptedLine } from './geometry.mjs';

const metal = (color, roughness = 0.4) => new T.MeshStandardMaterial({ color, metalness: 0.7, roughness });
export const palette = {
  copper: metal(0xb77842), brass: metal(0xd6a65d), iron: metal(0x284951),
  dark: metal(0x182527), floor: metal(0x706050, 0.43), wood: new T.MeshStandardMaterial({ color: 0x926039, roughness: 0.9 }),
  cream: new T.MeshStandardMaterial({ color: 0xffe2b5, roughness: 0.85 }),
  navy: new T.MeshStandardMaterial({ color: 0x142c48, roughness: 0.8 }),
  teal: new T.MeshStandardMaterial({ color: 0x168e9c, roughness: 0.55 }), gold: metal(0xc9a35e),
};
const box = (root, size, mat, pos) => addMesh(root, new T.BoxGeometry(...size), mat, pos);
const cylinder = (root, radius, height, pos, mat = palette.copper) => addMesh(root, new T.CylinderGeometry(radius, radius, height, 24), mat, pos);
function ring(root, radius, thickness, pos, mat = palette.brass) {
  return addMesh(root, new T.TorusGeometry(radius, thickness, 8, 40), mat, pos);
}
function bolts(root, radius, pos, count = 12) {
  for (let i = 0; i < count; i++) {
    const a = i / count * Math.PI * 2;
    addMesh(root, new T.CylinderGeometry(0.07, 0.07, 0.075, 6), palette.brass,
      [pos[0] + Math.sin(a) * radius, pos[1] + Math.cos(a) * radius, pos[2]]).rotation.x = Math.PI / 2;
  }
}
function brickTexture() {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 512;
  const context = canvas.getContext('2d'); context.fillStyle = '#58483c'; context.fillRect(0, 0, 512, 512);
  for (let row = 0; row < 8; row++) for (let column = -1; column < 5; column++) {
    const shade = 30 + ((row * 13 + column * 7 + 50) % 12);
    context.fillStyle = `hsl(18 34% ${shade}%)`;
    context.fillRect(column * 128 + row % 2 * 64 + 3, row * 64 + 3, 122, 58);
    context.fillStyle = '#ffffff0c'; context.fillRect(column * 128 + row % 2 * 64 + 4, row * 64 + 4, 120, 2);
  }
  const texture = new T.CanvasTexture(canvas); texture.colorSpace = T.SRGBColorSpace;
  texture.wrapS = texture.wrapT = T.RepeatWrapping; texture.repeat.set(9, 4);
  return texture;
}
function railing(root, x, z, length) {
  for (let i = 0; i <= length; i += 1.4) cylinder(root, 0.045, 1.2, [x + i, 6.1, z], palette.brass);
  for (const y of [5.9, 6.7]) {
    const rail = cylinder(root, 0.055, length, [x + length / 2, y, z], palette.brass); rail.rotation.z = Math.PI / 2;
  }
}
function windowFrame(root, x, z) {
  const glow = new T.MeshStandardMaterial({ color: 0xe7d8b1, emissive: 0xffd197, emissiveIntensity: 0.75 });
  box(root, [3.1, 4.1, 0.12], glow, [x, 9.2, z]);
  for (const dx of [-1.6, -0.55, 0.55, 1.6]) box(root, [0.1, 4.3, 0.2], palette.iron, [x + dx, 9.2, z + 0.12]);
  for (const y of [7.1, 8.5, 9.9, 11.3]) box(root, [3.3, 0.1, 0.2], palette.iron, [x, y, z + 0.12]);
}
function roof(root) {
  for (const sign of [-1, 1]) {
    const panel = box(root, [14, 0.18, 33], palette.wood, [sign * 6.5, 13.8, 1.5]);
    panel.rotation.z = -sign * 0.28;
  }
  for (const z of [-12, -5, 2, 9]) {
    box(root, [26, 0.22, 0.28], palette.iron, [0, 12, z]);
    for (const sign of [-1, 1]) {
      const beam = box(root, [14, 0.22, 0.32], palette.iron, [sign * 6.2, 13.8, z]); beam.rotation.z = -sign * 0.28;
      box(root, [0.25, 11.5, 0.3], palette.iron, [sign * 12.5, 5.75, z]);
    }
  }
}
function floorPlates(root) {
  for (let x = -12; x < 13; x += 3) for (let z = -12; z < 17; z += 3) {
    box(root, [2.96, 0.12, 2.96], palette.floor, [x, -0.07, z]);
    for (const dx of [-1.3, 1.3]) for (const dz of [-1.3, 1.3]) cylinder(root, 0.035, 0.018, [x + dx, 0, z + dz], palette.brass);
  }
  for (const x of [-7, 7]) {
    box(root, [0.45, 0.04, 28], palette.dark, [x, 0.015, 2]);
    for (let z = -12; z < 16; z += 0.32) box(root, [0.42, 0.035, 0.06], palette.iron, [x, 0.04, z]);
  }
}
function crate(root, x, y, z, scale = 1) {
  const group = new T.Group(); group.position.set(x, y, z); group.scale.setScalar(scale); root.add(group);
  box(group, [1.4, 1.4, 1.4], palette.wood, [0, 0.7, 0]);
  for (const a of [-0.58, 0.58]) {
    box(group, [0.14, 1.5, 1.5], palette.copper, [a, 0.7, 0]);
    box(group, [1.5, 0.14, 1.5], palette.copper, [0, a + 0.7, 0]);
  }
  const brace = box(group, [0.15, 1.7, 0.1], palette.wood, [0, 0.7, 0.77]); brace.rotation.z = -Math.PI / 4;
}
function boiler(root, x, z) {
  cylinder(root, 1.25, 4.5, [x, 2.65, z]);
  const cap = addMesh(root, new T.SphereGeometry(1.25, 24, 16), palette.copper, [x, 4.9, z]); cap.scale.y = 0.48;
  for (const y of [0.55, 1.1, 4.4]) ring(root, 1.26, 0.1, [x, y, z], palette.iron).rotation.x = Math.PI / 2;
  cylinder(root, 0.24, 3.4, [x, 6.3, z]);
  sculptedLine(root, [[x, 7.8, z], [x, 8.3, z], [x + 2, 8.3, z], [x + 2.2, 8, z]], palette.copper, 0.2);
  ring(root, 0.36, 0.1, [x, 3.4, z + 1.23]);
  const dial = cylinder(root, 0.32, 0.08, [x, 3.4, z + 1.26], palette.cream); dial.rotation.x = Math.PI / 2;
  const needle = box(root, [0.025, 0.4, 0.03], palette.dark, [x + 0.05, 3.46, z + 1.33]); needle.rotation.z = -0.6;
}
export function cog(root, radius, position) {
  const group = new T.Group(); group.position.set(...position); root.add(group);
  ring(group, radius * 0.78, radius * 0.18, [0, 0, 0], palette.copper);
  const hub = cylinder(group, radius * 0.25, 0.4, [0, 0, 0], palette.iron); hub.rotation.x = Math.PI / 2;
  for (let i = 0; i < 16; i++) {
    const angle = i / 16 * Math.PI * 2;
    const tooth = box(group, [radius * 0.25, radius * 0.32, 0.35], palette.copper, [Math.sin(angle) * radius, Math.cos(angle) * radius, 0]); tooth.rotation.z = -angle;
  }
  for (let i = 0; i < 6; i++) box(group, [radius * 0.13, radius * 1.6, 0.24], palette.copper, [0, 0, 0]).rotation.z = i * Math.PI / 3;
  bolts(group, radius * 0.29, [0, 0, 0.26], 8);
  return group;
}
function waterwheel(root) {
  const wheel = new T.Group(); wheel.position.set(-11, 3.3, 1); wheel.rotation.y = 0.48; root.add(wheel);
  for (const z of [-0.6, 0.6]) {
    ring(wheel, 3, 0.18, [0, 0, z], palette.wood);
    ring(wheel, 2.85, 0.06, [0, 0, z + 0.1], palette.iron);
    for (let i = 0; i < 8; i++) box(wheel, [0.22, 6, 0.18], palette.wood, [0, 0, z]).rotation.z = i * Math.PI / 4;
  }
  for (let i = 0; i < 16; i++) {
    const a = i / 16 * Math.PI * 2;
    const paddle = box(wheel, [0.9, 0.18, 1.5], palette.wood, [Math.sin(a) * 2.9, Math.cos(a) * 2.9, 0]); paddle.rotation.z = -a;
  }
  return wheel;
}
function outside(root) {
  const water = new T.MeshStandardMaterial({ color: 0x509d9b, roughness: 0.18, metalness: 0.35 });
  box(root, [10, 0.1, 34], water, [-16, -0.3, 1]);
  const rock = new T.MeshStandardMaterial({ color: 0x989987, roughness: 1 });
  const leaf = new T.MeshStandardMaterial({ color: 0x526d38, roughness: 1 });
  for (let i = 0; i < 9; i++) {
    const boulder = addMesh(root, new T.DodecahedronGeometry(1), rock, [-23, 1.5, i * 4 - 18]); boulder.scale.set(3, 3 + i % 3, 3);
    addMesh(root, new T.IcosahedronGeometry(2.4, 1), leaf, [-23, 5 + i % 3, i * 4 - 18]);
  }
  const waterfall = box(root, [1, 5, 1.3], new T.MeshStandardMaterial({ color: 0xc3e0d4, transparent: true, opacity: 0.65, roughness: 0.15 }), [-12.5, 2.4, -0.6]);
  return waterfall;
}
function weatherMaterials() {
  const canvas = document.createElement('canvas'); canvas.width = canvas.height = 256;
  const context = canvas.getContext('2d'), pixels = context.createImageData(256, 256);
  for (let i = 0; i < pixels.data.length; i += 4) {
    const tone = 125 + Math.sin(i * 13.37) * 22 + Math.sin(i * 0.031) * 12;
    pixels.data.set([tone, tone, tone, 255], i);
  }
  context.putImageData(pixels, 0, 0);
  const texture = new T.CanvasTexture(canvas); texture.wrapS = texture.wrapT = T.RepeatWrapping;
  for (const key of ['copper', 'brass', 'iron', 'floor', 'wood']) {
    palette[key].bumpMap = texture; palette[key].bumpScale = key === 'wood' ? 0.08 : 0.025;
    palette[key].roughnessMap = texture;
  }
}
export function buildArchitecture(scene) {
  weatherMaterials();
  const root = new T.Group(); scene.add(root);
  box(root, [55, 0.2, 65], palette.floor, [0, -0.2, 5]);
  floorPlates(root);
  const bricks = new T.MeshStandardMaterial({ map: brickTexture(), roughness: 0.95 });
  box(root, [27, 14, 0.6], bricks, [0, 7, -13]);
  box(root, [0.5, 14, 31], bricks, [13.5, 7, 1]);
  for (const x of [-10, -5, 0, 5, 10]) windowFrame(root, x, -12.6);
  roof(root);
  box(root, [26, 0.25, 2.2], palette.iron, [0, 5.4, -9.5]);
  railing(root, -12.5, -8.4, 25);
  for (const x of [-12, 12]) {
    box(root, [2, 0.25, 21], palette.iron, [x, 5.4, 1]);
    for (let z = -8; z < 11; z += 1.5) cylinder(root, 0.045, 1.2, [x - Math.sign(x), 6.1, z], palette.brass);
    for (const y of [5.9, 6.7]) cylinder(root, 0.055, 20, [x - Math.sign(x), y, 1], palette.brass).rotation.x = Math.PI / 2;
  }
  for (let i = 0; i < 18; i++) box(root, [2, 0.18, 0.55], palette.iron, [10, 0.3 * i, 4 - i * 0.55]);
  boiler(root, -7.5, -9); boiler(root, 8, -9);
  const gears = [cog(root, 2.5, [0, 8.1, -11.8]), cog(root, 1.5, [3.6, 6.7, -11.6]), cog(root, 1.35, [-3.6, 6, -11.6])];
  for (const [x, y, z, size] of [[-8, 0, 5, 1.5], [-9, 2.1, 5, 1], [-10, 0, 8, 1.8], [9, 0, 5, 1.3], [10, 1.8, 5, 0.9], [-6, 0, -8, 1]]) crate(root, x, y, z, size);
  for (const x of [-6, 3, 9]) {
    cylinder(root, 0.035, 3, [x, 10.5, -4], palette.dark);
    addMesh(root, new T.ConeGeometry(0.7, 0.4, 24, 1, true), palette.copper, [x, 9, -4]);
    const lamp = new T.PointLight(0xffbe73, 28, 15); lamp.position.set(x, 8.7, -4); root.add(lamp);
  }
  const waterfall = outside(root), wheel = waterwheel(root);
  return { gears, wheel, waterfall };
}
function socket(root, x, y, color) {
  const material = new T.MeshStandardMaterial({ color, emissive: color, emissiveIntensity: 0.8, metalness: 0.3, roughness: 0.25 });
  const opening = cylinder(root, 0.42, 0.23, [x, y, -2.85], palette.dark); opening.rotation.x = Math.PI / 2;
  ring(root, 0.47, 0.12, [x, y, -2.65]);
  const glow = ring(root, 0.39, 0.038, [x, y, -2.51], material);
  bolts(root, 0.63, [x, y, -2.8], 8);
  return glow;
}
export function buildMachine(scene) {
  const root = new T.Group(); scene.add(root);
  cylinder(root, 3.1, 0.18, [0, 0.15, -3], palette.copper);
  ring(root, 2.9, 0.07, [0, 0.25, -3]).rotation.x = Math.PI / 2;
  box(root, [2.3, 0.8, 1.6], palette.iron, [0, 0.6, -3]);
  const body = cylinder(root, 1.25, 0.9, [0, 2, -3]); body.rotation.x = Math.PI / 2;
  ring(root, 1.23, 0.14, [0, 2, -2.5]); bolts(root, 1.08, [0, 2, -2.32]);
  const rotor = new T.Group(); rotor.position.set(0, 2, -2.45); root.add(rotor);
  for (let i = 0; i < 8; i++) {
    const a = i / 8 * Math.PI * 2;
    const blade = addMesh(rotor, roundedBox([0.42, 0.7, 0.13], 0.09), palette.teal, [Math.sin(a) * 0.63, Math.cos(a) * 0.63, 0]); blade.rotation.z = -a + 0.35;
  }
  const hub = cylinder(rotor, 0.3, 0.25, [0, 0, 0.13], palette.brass); hub.rotation.x = Math.PI / 2;
  const sockets = { cause: [], result: [] };
  for (const x of [-4, 4]) {
    addMesh(root, roundedBox([1.8, 4.5, 0.6], 0.18), palette.iron, [x, 2.3, -3.3]);
    box(root, [2.1, 0.3, 1.4], palette.copper, [x, 0.2, -3.3]);
    for (let row = 0; row < 3; row++) {
      const y = 3.75 - row * 1.25;
      sockets[x > 0 ? 'cause' : 'result'].push(socket(root, x, y, x > 0 ? 0xff962a : 0x39d9ed));
      sculptedLine(root, [[x - Math.sign(x) * 0.8, y, -3.3], [x - Math.sign(x) * 1.5, y, -3.3], [x - Math.sign(x) * 1.8, y - 0.2, -3.3], [Math.sign(x) * 1.5, 1.7 + row * 0.3, -3.3], [Math.sign(x) * 1.2, 1.7 + row * 0.3, -3.3]], palette.copper, 0.16);
    }
  }
  cylinder(root, 0.3, 1.2, [0, 3.6, -3.2]);
  const indicator = new T.MeshStandardMaterial({ color: 0xffb43d, emissive: 0xff9e24, emissiveIntensity: 0.1 });
  for (let i = 0; i < 7; i++) box(root, [0.18, 0.3, 0.08], indicator, [-0.75 + i * 0.25, 0.65, -2.15]);
  return { rotor, sockets, indicator };
}
