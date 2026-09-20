import * as T from './vendor/three.module.js';

export function addMesh(parent, geometry, surface, position = [0, 0, 0]) {
  const shape = new T.Mesh(geometry, surface); shape.position.set(...position);
  shape.castShadow = true; shape.receiveShadow = true; parent.add(shape); return shape;
}

export function roundedBox(size, radius = .08) {
  const geometry = new T.BoxGeometry(...size, 8, 8, 8);
  const vertices = geometry.attributes.position; const normals = geometry.attributes.normal;
  const core = new T.Vector3(...size).multiplyScalar(.5).addScalar(-radius);
  const minimum = core.clone().negate();
  const point = new T.Vector3(); const nearest = new T.Vector3();
  for (let index = 0; index < vertices.count; index++) {
    point.fromBufferAttribute(vertices, index);
    nearest.copy(point).clamp(minimum, core);
    point.sub(nearest).normalize(); normals.setXYZ(index, point.x, point.y, point.z);
    point.multiplyScalar(radius).add(nearest);
    vertices.setXYZ(index, point.x, point.y, point.z);
  }
  return geometry;
}

export function ellipsoid(parent, size, surface, position) {
  const shape = addMesh(parent, new T.SphereGeometry(1, 32, 24), surface, position);
  shape.scale.set(...size); return shape;
}

export function sculptedLine(parent, coordinates, surface, radius = .035) {
  const curve = new T.CatmullRomCurve3(coordinates.map(point => new T.Vector3(...point)));
  return addMesh(parent, new T.TubeGeometry(curve, 32, radius, 8, false), surface);
}

export function bone(parent, ends, surface, radius = .07) {
  const from = new T.Vector3(...ends[0]); const to = new T.Vector3(...ends[1]);
  const shape = addMesh(parent, new T.CylinderGeometry(radius * .75, radius, from.distanceTo(to), 12), surface);
  shape.position.copy(from).add(to).multiplyScalar(.5);
  shape.quaternion.setFromUnitVectors(new T.Vector3(0, 1, 0), to.sub(from).normalize()); return shape;
}
