import * as T from './vendor/three.module.js';

export const destinations = [
  { id: 'valley', title: 'وادي التحوّل', description: 'نهر بين الجبال، وجسر تبنيه بترتيب رحلة الصخر.', sky: 0x9dcbd0, fog: 0xb5ced0, accent: '#92dab6' },
  { id: 'factory', title: 'مصنع الأسباب', description: 'تروس ومواسير تنتظر توصيل السبب بنتيجته.', sky: 0x263941, fog: 0x435355, accent: '#f2bd70' },
  { id: 'cave', title: 'كهف البلّورات', description: 'استكشف المنجم، واجمع العيّنات في صناديقها.', sky: 0x101f2b, fog: 0x1e3542, accent: '#99dce7' },
];
const surface = (color, options = {}) => new T.MeshStandardMaterial({ color, roughness: 0.8, ...options });
function add(root, geometry, material, position) {
  const shape = new T.Mesh(geometry, material);
  shape.position.set(...position);
  shape.castShadow = true;
  shape.receiveShadow = true;
  root.add(shape);
  return shape;
}
const block = (root, size, material, position) => add(root, new T.BoxGeometry(...size), material, position);
function rock(root, material, position, scale) {
  const shape = add(root, new T.DodecahedronGeometry(1, 0), material, position);
  shape.scale.set(...scale);
  shape.rotation.set(0.2, position[0] * 0.8, 0.15);
  return shape;
}
function tree(root, x, z, palette) {
  add(root, new T.CylinderGeometry(0.15, 0.25, 2.4, 6), palette.trunk, [x, 1.2, z]);
  for (let tier = 0; tier < 3; tier++)
    add(root, new T.ConeGeometry(1.3 - tier * 0.25, 2.2, 7), palette.leaf, [x, 2.2 + tier * 0.9, z]);
}
function valley(root) {
  const grass = surface(0x628c67), stone = surface(0x779391), wood = surface(0x86664b);
  block(root, [34, 0.8, 19], grass, [0, -0.45, 8.5]);
  block(root, [34, 1.3, 15], grass, [0, -0.3, -13.5]);
  const water = block(root, [34, 0.12, 5], surface(0x278b9b, { metalness: 0.3, roughness: 0.2 }), [0, -0.4, -3.5]);
  const foam = surface(0xc5eae4);
  const ripples = Array.from({ length: 14 }, (_, i) => block(root, [1.3 + (i % 3), 0.025, 0.035], foam, [-15 + i * 2.2, -0.31, -2.4 - (i % 3)]));
  for (let i = 0; i < 12; i++) {
    const x = (i - 5.5) * 3.8;
    rock(root, stone, [x, 3.8, -17 - i % 3], [3.6, 6 + i % 4, 3.2]);
    rock(root, stone, [i % 2 ? -13 : 13, 0.8, -8 + i * 2], [2, 1.8, 2]);
  }
  const palette = { trunk: wood, leaf: surface(0x285e49) };
  for (let i = 0; i < 10; i++) tree(root, i % 2 ? -10 : 10, -10 + Math.floor(i / 2) * 5, palette);
  const bridge = new T.Group(); root.add(bridge);
  for (let i = 0; i < 13; i++) block(bridge, [3, 0.14, 0.4], wood, [0, 0.05, -6 + i * 0.43]);
  for (const x of [-1.55, 1.55]) {
    for (const z of [-6, -3.5, -1]) block(root, [0.12, 1.3, 0.12], wood, [x, 0.55, z]);
    block(root, [0.08, 0.08, 5.2], wood, [x, 1, -3.5]);
  }
  return (time, completed) => {
    bridge.visible = completed >= 1;
    water.position.y = -0.4 + Math.sin(time * 0.7) * 0.025;
    ripples.forEach((ripple, i) => { ripple.position.x = -16 + ((i * 2.2 + time * 0.6) % 32); });
  };
}
function gear(root, material, x, y, radius) {
  const group = new T.Group(); group.position.set(x, y, -8.7); root.add(group);
  add(group, new T.TorusGeometry(radius, radius * 0.19, 8, 24), material, [0, 0, 0]);
  for (let i = 0; i < 12; i++) {
    const angle = i * Math.PI / 6;
    const tooth = block(group, [radius * 0.28, radius * 0.45, 0.35], material, [Math.sin(angle) * radius, Math.cos(angle) * radius, 0]);
    tooth.rotation.z = -angle;
  }
  for (let i = 0; i < 3; i++) block(group, [0.15, radius * 1.8, 0.2], material, [0, 0, 0]).rotation.z = i * Math.PI / 3;
  return group;
}
function factory(root) {
  const iron = surface(0x384d55, { metalness: 0.65 }), copper = surface(0xbc8150, { metalness: 0.7 }), concrete = surface(0x647274);
  block(root, [32, 0.6, 30], concrete, [0, -0.35, 1]);
  block(root, [32, 11, 0.6], iron, [0, 5.5, -10]);
  for (const x of [-14, -7, 7, 14]) {
    block(root, [0.45, 10, 0.45], copper, [x, 5, -9]);
    block(root, [0.4, 0.4, 25], iron, [x, 9, 1]);
  }
  const glow = surface(0xffc679, { emissive: 0xffac4c, emissiveIntensity: 1.2 });
  for (const x of [-11, 0, 11]) block(root, [3.5, 2, 0.1], glow, [x, 7.8, -9.6]);
  for (const x of [-10, 10]) {
    add(root, new T.CylinderGeometry(1.5, 1.5, 5, 16), copper, [x, 2.5, -3]);
    for (const y of [1, 4]) add(root, new T.TorusGeometry(1.53, 0.08, 6, 24), iron, [x, y, -3]).rotation.x = Math.PI / 2;
    for (const z of [1, 4, 7]) block(root, [2, 1.2, 1.8], iron, [x, 0.6, z]);
    const pipe = add(root, new T.CylinderGeometry(0.2, 0.2, 10, 10), copper, [x, 6, -4]);
    pipe.rotation.x = Math.PI / 2;
  }
  const gears = [gear(root, copper, -4, 4.5, 2), gear(root, copper, 0, 3.8, 1.6), gear(root, copper, 3.5, 5, 1.9)];
  return (time, completed) => gears.forEach((rotor, i) => { rotor.rotation.z = time * (completed >= 2 ? 0.4 : 0.08) * (i % 2 ? -1 : 1); });
}
function cave(root) {
  const stone = surface(0x364e5b), floor = surface(0x536575), timber = surface(0x836747);
  block(root, [34, 0.6, 32], floor, [0, -0.35, 1]);
  for (let i = 0; i < 16; i++) {
    const angle = i / 15 * Math.PI;
    rock(root, stone, [Math.cos(angle) * 14, 3.5, -3 - Math.sin(angle) * 12], [3.2, 6 + i % 3, 3]);
  }
  for (const x of [-11, 11]) {
    for (const z of [-7, 0, 7]) rock(root, stone, [x, 0.7, z], [2.3, 1.5, 2.4]);
  }
  const crystals = [surface(0x69bac6, { emissive: 0x297a89, emissiveIntensity: 0.55, metalness: 0.35 }), surface(0xbad4b3, { emissive: 0x497959, emissiveIntensity: 0.35 })];
  for (let i = 0; i < 24; i++) {
    const x = i % 2 ? -8 - (i % 3) : 8 + (i % 3), z = -10 + Math.floor(i / 2) * 1.7;
    const crystal = add(root, new T.CylinderGeometry(0, 0.55, 2 + i % 4, 5), crystals[i % 2], [x, 1.2, z]);
    crystal.rotation.z = (i % 3 - 1) * 0.3;
  }
  for (const z of [-8, -12]) {
    for (const x of [-3.6, 3.6]) block(root, [0.35, 5, 0.4], timber, [x, 2.5, z]);
    block(root, [7.6, 0.4, 0.4], timber, [0, 5, z]);
  }
  for (const x of [-1, 1]) block(root, [0.07, 0.07, 12], crystals[0], [x, 0.03, -8]);
  for (let i = 0; i < 12; i++) block(root, [2.5, 0.08, 0.22], timber, [0, -0.02, -14 + i]);
  const light = new T.PointLight(0x8cdae2, 55, 25); light.position.set(0, 5, -5); root.add(light);
  return () => {};
}
export function buildEnvironments(scene) {
  const worlds = [valley, factory, cave].map((build) => {
    const root = new T.Group(); scene.add(root); root.visible = false;
    return { root, animate: build(root) };
  });
  let active = -1, time = 0;
  return {
    show(index) {
      active = index;
      worlds.forEach(({ root }, i) => { root.visible = i === index; });
      const theme = destinations[index];
      scene.background.set(theme?.sky ?? 0x192736);
      scene.fog.color.set(theme?.fog ?? 0x263442);
    },
    update(delta, completed) {
      time += delta;
      if (active >= 0) worlds[active].animate(time, completed);
    },
  };
}
