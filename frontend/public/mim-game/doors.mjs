import * as T from './vendor/three.module.js';
import { addMesh, roundedBox, sculptedLine } from './geometry.mjs';

function sun(parent,gold) {
  addMesh(parent,new T.TorusGeometry(.19,.03,10,36),gold);
  for(let index=0;index<12;index++) {
    const angle=index*Math.PI/6;
    const ray=addMesh(parent,new T.ConeGeometry(.034,.19,4),gold,[Math.sin(angle)*.35,Math.cos(angle)*.35,.01]);ray.rotation.z=-angle;
  }
}

function ankh(parent,gold) {
  const loop=addMesh(parent,new T.TorusGeometry(.15,.033,10,36),gold,[0,.23,0]);loop.scale.y=1.3;
  sculptedLine(parent,[[0,.08,0],[0,-.43,0]],gold,.035);
  sculptedLine(parent,[[-.24,-.06,0],[.24,-.06,0]],gold,.035);
}

function lotus(parent,gold) {
  for(const side of [-1,0,1]) sculptedLine(parent,[[0,-.31,0],[side*.3,.06,0],[side*.31,.35,0],[0,-.31,0]],gold,.03);
  sculptedLine(parent,[[-.38,-.3,0],[0,-.39,0],[.38,-.3,0]],gold,.025);
}

function pyramid(parent,gold) {
  sculptedLine(parent,[[-.41,-.29,0],[0,.4,0],[.41,-.29,0],[-.41,-.29,0]],gold,.033);
  sculptedLine(parent,[[0,.4,0],[.1,-.29,0]],gold,.022);
}

function medallion(parent,position,palette,symbol) {
  const ornament=new T.Group();ornament.position.set(...position);parent.add(ornament);
  addMesh(ornament,new T.CircleGeometry(.68,64),palette.navy,[0,0,-.015]);
  addMesh(ornament,new T.TorusGeometry(.68,.048,12,64),palette.gold);
  addMesh(ornament,new T.TorusGeometry(.58,.013,8,64),palette.gold,[0,0,.015]);
  symbol(ornament,palette.gold);
}

export function buildDoors(scene,palette) {
  const panels=[];const symbols=[sun,ankh,lotus,pyramid];
  for(const [index,sign] of [-1,1].entries()) {
    const pivot=new T.Group();pivot.position.set(sign*3.55,.1,-10.9);scene.add(pivot);
    addMesh(pivot,roundedBox([3.5,7.6,.32],.065),palette.navy,[-sign*1.75,3.8,0]);
    for(const x of [-sign*.13,-sign*3.36]) addMesh(pivot,roundedBox([.055,7.35,.06],.015),palette.gold,[x,3.8,.19]);
    for(const y of [.16,3.8,7.44]) addMesh(pivot,roundedBox([3.27,.055,.06],.015),palette.gold,[-sign*1.75,y,.19]);
    for(const [row,y] of [2.05,5.4].entries()) {
      medallion(pivot,[-sign*1.75,y,.23],palette,symbols[index+row*2]);
      for(const x of [-sign*.38,-sign*3.1]) for(const dy of [-1.25,1.25]) addMesh(pivot,new T.SphereGeometry(.055,12,8),palette.gold,[x,y+dy,.22]);
    }
    const handle=addMesh(pivot,new T.TorusGeometry(.13,.035,10,32),palette.gold,[-sign*.34,3.5,.24]);handle.scale.y=1.5;
    panels.push(pivot);
  }
  return panels;
}
