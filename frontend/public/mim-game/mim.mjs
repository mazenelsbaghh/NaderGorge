import * as T from './vendor/three.module.js';
import { addMesh, roundedBox, ellipsoid, sculptedLine } from './geometry.mjs';

function buildFace(body, palette) {
  addMesh(body, roundedBox([1.5, 1.32, 1.15], .46), palette.cream, [0, 2.2, 0]);
  const white = new T.MeshPhysicalMaterial({ color: 0xfff7e3, roughness: .3 });
  const eyes = new T.MeshPhysicalMaterial({ color: 0x071323, roughness: .12, clearcoat: 1 });
  const blush = new T.MeshStandardMaterial({ color: 0xe8b79e, roughness: .9 });
  for (const sign of [-1, 1]) {
    ellipsoid(body, [.237, .3, .08], white, [sign * .29, 2.17, .55]);
    ellipsoid(body, [.177, .235, .069], eyes, [sign * .28, 2.17, .611]);
    ellipsoid(body, [.052, .072, .013], white, [sign * .28 - .057, 2.26, .674]);
    ellipsoid(body, [.02, .027, .01], white, [sign * .28 + .053, 2.11, .679]);
    ellipsoid(body, [.115, .055, .012], blush, [sign * .46, 1.99, .562]);
    sculptedLine(body, [[sign*.18,2.55,.55],[sign*.28,2.58,.55],[sign*.37,2.54,.53]], palette.navy, .025);
    const stalk = new T.Group(); stalk.position.set(sign * .32, 2.8, -.1); stalk.rotation.z = -sign * .38; body.add(stalk);
    ellipsoid(stalk, [.12,.29,.115], palette.cream, [0,.17,0]);
    ellipsoid(stalk, [.14,.14,.13], palette.teal, [0,.4,0]);
  }
  sculptedLine(body, [[-.1,1.95,.58],[0,1.9,.599],[.1,1.95,.58]], palette.navy, .021);
}

function buildPack(body, palette) {
  addMesh(body, roundedBox([.78,.82,.32], .16), palette.teal, [0,1.25,-.39]);
  addMesh(body, roundedBox([.56,.38,.13], .09), palette.navy, [0,1.08,-.61]);
  addMesh(body, roundedBox([.64,.21,.13], .07), palette.teal, [0,1.52,-.58]);
  const thread = new T.MeshStandardMaterial({color:0x8bbcb4,roughness:.85});
  sculptedLine(body,[[-.22,1.17,-.68],[-.08,1.27,-.68],[.13,1.4,-.59],[-.05,1.5,-.66]],thread,.027);
  for (const sign of [-1,1]) {
    sculptedLine(body,[[sign*.26,1.5,.26],[sign*.32,1.76,-.05],[sign*.28,1.55,-.46]],palette.teal,.055);
    addMesh(body,roundedBox([.11,.15,.05],.02),palette.gold,[sign*.23,1.4,-.68]);
    addMesh(body,roundedBox([.08,.13,.04],.015),palette.gold,[sign*.3,1.36,.37]);
  }
}

function buildLeg(body, sign, palette) {
  const leg = new T.Group(); leg.position.set(sign * .25,.71,0); body.add(leg);
  ellipsoid(leg,[.205,.34,.22],palette.navy,[0,-.2,0]);
  addMesh(leg,roundedBox([.46,.29,.63],.12),palette.navy,[0,-.49,.1]);
  addMesh(leg,roundedBox([.48,.1,.65],.04),palette.teal,[0,-.62,.1]);
  for (const z of [.15,.23,.31]) addMesh(leg,roundedBox([.25,.035,.036],.015),palette.teal,[0,-.34,z]);
  return leg;
}

function buildArm(body, sign, palette) {
  const arm=new T.Group(); arm.position.set(sign*.49,1.52,0); arm.rotation.z=sign*.14; body.add(arm);
  ellipsoid(arm,[.18,.28,.18],palette.navy,[sign*.02,-.17,0]);
  ellipsoid(arm,[.175,.19,.19],palette.navy,[sign*.04,-.39,.06]);
  addMesh(arm,new T.CylinderGeometry(.175,.175,.13,24),palette.teal,[sign*.05,-.5,.1]);
  ellipsoid(arm,[.18,.2,.175],palette.cream,[sign*.05,-.64,.12]);
  return arm;
}

export function buildMim(palette) {
  const root=new T.Group(); const body=new T.Group(); root.add(body);
  ellipsoid(body,[.48,.59,.36],palette.navy,[0,1.18,0]);
  addMesh(body,new T.CylinderGeometry(.39,.42,.14,32),palette.teal,[0,1.69,0]);
  addMesh(body,roundedBox([.035,.62,.018],.008),palette.gold,[0,1.26,.366]);
  addMesh(body,roundedBox([.24,.16,.045],.04),palette.teal,[.21,1.24,.34]);
  buildFace(body,palette); buildPack(body,palette);
  const limbs=[buildLeg(body,-1,palette),buildLeg(body,1,palette),buildArm(body,-1,palette),buildArm(body,1,palette)];
  return {root,body,limbs};
}
