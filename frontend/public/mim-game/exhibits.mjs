import * as T from './vendor/three.module.js';
import { addMesh, roundedBox, ellipsoid, sculptedLine, bone } from './geometry.mjs';

export function pedestal(group, palette) {
  addMesh(group,roundedBox([2.25,.18,2.1],.04),palette.gold,[0,.09,0]);
  addMesh(group,roundedBox([2.07,1.28,1.92],.07),palette.navy,[0,.8,0]);
  addMesh(group,roundedBox([2.2,.16,2.06],.04),palette.gold,[0,1.5,0]);
  addMesh(group,roundedBox([2.25,.13,2.1],.04),palette.navy,[0,1.63,0]);
  for (const x of [-.92,.92]) addMesh(group,new T.BoxGeometry(.028,1.08,.02),palette.gold,[x,.83,.971]);
}

export function compass(group, palette) {
  const armillary=new T.Group(); armillary.position.set(0,2.6,0); group.add(armillary);
  for (const angle of [0,Math.PI/2,Math.PI/4]) {
    const ring=addMesh(armillary,new T.TorusGeometry(.7,.036,12,72),palette.gold);ring.rotation.y=angle;
  }
  const dial=addMesh(armillary,new T.CylinderGeometry(.55,.55,.04,64),palette.navy);dial.rotation.x=Math.PI/2;
  for(let index=0;index<16;index++) {
    const angle=index*Math.PI/8;
    const tick=addMesh(armillary,new T.BoxGeometry(.018,index%4===0?.15:.07,.025),palette.gold,[Math.sin(angle)*.47,Math.cos(angle)*.47,.04]);tick.rotation.z=-angle;
  }
  const needle=addMesh(armillary,new T.ConeGeometry(.09,.85,4),palette.gold,[0,.06,.1]);needle.rotation.z=-.5;
  ellipsoid(armillary,[.09,.09,.08],palette.teal,[0,0,.13]);
  addMesh(group,new T.CylinderGeometry(.08,.16,.35,24),palette.gold,[0,1.88,0]);
}

export function scales(group,palette) {
  addMesh(group,new T.CylinderGeometry(.4,.52,.15,32),palette.gold,[0,1.78,0]);
  addMesh(group,new T.CylinderGeometry(.06,.11,1.35,24),palette.gold,[0,2.42,0]);
  ellipsoid(group,[.12,.12,.12],palette.gold,[0,3.14,0]);
  sculptedLine(group,[[-.75,2.96,0],[0,3.08,0],[.75,2.96,0]],palette.gold,.04);
  for(const sign of [-1,1]) {
    for(const offset of [-.2,.2]) sculptedLine(group,[[sign*.67,2.96,0],[sign*.67+offset,2.45,0]],palette.gold,.015);
    ellipsoid(group,[.28,.055,.25],palette.gold,[sign*.67,2.43,0]);
  }
}

export function catStatue(group,palette) {
  const bronze=new T.MeshStandardMaterial({color:0x403930,metalness:.68,roughness:.35});
  const cat=new T.Group();cat.position.set(0,1.72,0);group.add(cat);
  ellipsoid(cat,[.4,.58,.34],bronze,[0,.56,0]);
  ellipsoid(cat,[.28,.5,.24],bronze,[0,.98,.04]);
  ellipsoid(cat,[.37,.31,.29],bronze,[0,1.43,.05]);
  for(const sign of [-1,1]) {
    const ear=addMesh(cat,new T.ConeGeometry(.18,.42,4),bronze,[sign*.23,1.76,.025]);ear.rotation.z=-sign*.13;
    ellipsoid(cat,[.12,.4,.13],bronze,[sign*.19,.4,.27]);
    ellipsoid(cat,[.14,.08,.21],bronze,[sign*.19,.08,.33]);
    ellipsoid(cat,[.075,.033,.025],palette.gold,[sign*.15,1.49,.321]);
  }
  const collar=addMesh(cat,new T.TorusGeometry(.25,.036,10,48),palette.gold,[0,1.13,.045]);collar.rotation.x=Math.PI/2;
  sculptedLine(cat,[[.3,.15,-.14],[.58,.17,-.08],[.53,.09,.42],[.06,.09,.51]],bronze,.085);
}

function skull(skeleton, surface) {
  const head=new T.Group();head.position.set(1.7,3.82,0);head.rotation.z=-.1;skeleton.add(head);
  addMesh(head,roundedBox([1.05,.51,.52],.18),surface,[.22,.06,0]);
  addMesh(head,roundedBox([.98,.12,.45],.045),surface,[.24,-.36,0]);
  const dark=new T.MeshStandardMaterial({color:0x42382c,roughness:1});
  for(const side of [-1,1]) {
    ellipsoid(head,[.16,.17,.035],dark,[-.06,.13,side*.25]);
    ellipsoid(head,[.075,.045,.02],dark,[.62,.065,side*.23]);
    for(let index=0;index<8;index++) {
      const tooth=addMesh(head,new T.ConeGeometry(.032,.14,8),surface,[-.09+index*.1,-.23,side*.19]);tooth.rotation.z=Math.PI;
      addMesh(head,new T.ConeGeometry(.028,.1,8),surface,[-.05+index*.1,-.27,side*.18]);
    }
  }
}

function skeletonLegs(skeleton,surface) {
  for(const side of [-1,1]) {
    bone(skeleton,[[0,2.5,side*.36],[-.35,1.65,side*.53]],surface,.16);
    ellipsoid(skeleton,[.18,.17,.18],surface,[-.35,1.65,side*.53]);
    bone(skeleton,[[-.35,1.65,side*.53],[.1,.44,side*.61]],surface,.09);
    for(const toe of [-.2,0,.2]) bone(skeleton,[[.1,.3,side*.61],[.7,.19,side*.61+toe]],surface,.05);
    bone(skeleton,[[1.07,2.83,side*.37],[1.48,2.52,side*.55]],surface,.06);
    bone(skeleton,[[1.48,2.52,side*.55],[1.77,2.61,side*.54]],surface,.045);
  }
}

export function dinosaur(scene,palette) {
  const skeleton=new T.Group();skeleton.position.set(-9,0,-3.8);skeleton.rotation.y=-.3;scene.add(skeleton);
  const ivory=new T.MeshStandardMaterial({color:0xcbb58d,roughness:.7});
  addMesh(skeleton,roundedBox([6.8,.24,3.1],.08),palette.navy,[-.7,.12,0]);
  const spine=[[-3.7,1.45,0],[-2.5,2.1,0],[-1.2,2.55,0],[0,2.8,0],[.9,3.05,0],[1.18,3.5,0],[1.5,3.82,0]];
  sculptedLine(skeleton,spine,ivory,.11);
  for(let index=0;index<12;index++) {
    const x=-1.35+index*.2;const height=2.7+.18*Math.sin(index*.2);const size=.56*Math.sin((index+2)/15*Math.PI);
    ellipsoid(skeleton,[.11,.12,.15],ivory,[x,height,0]);
    for(const side of [-1,1]) sculptedLine(skeleton,[[x,height,0],[x-.06,height-.18,side*size],[x+.08,height-.66,side*size*.85],[x+.22,height-.84,side*.12]],ivory,.036);
  }
  skeletonLegs(skeleton,ivory);skull(skeleton,ivory);
}
